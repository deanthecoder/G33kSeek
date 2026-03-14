// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DTC.Core;
using DTC.Core.Extensions;
using G33kSeek.Models;

namespace G33kSeek.Services;

/// <summary>
/// Provides cached application discovery and lookup for no-prefix launcher queries.
/// </summary>
/// <remarks>
/// This keeps application search fast by scanning macOS and Windows application sources in the background and serving matches from cached entries.
/// </remarks>
internal sealed class ApplicationSearchService : SearchServiceBase
{
    private const int WatcherBufferSize = 64 * 1024;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FullRefreshInterval = TimeSpan.FromHours(1);

    private readonly ApplicationSearchSettings m_settings;
    private readonly bool m_isMacOS;
    private readonly bool m_isWindows;
    private readonly Func<IReadOnlyList<WindowsStartApp>> m_windowsStartAppsAccessor;
    private readonly Func<IReadOnlyList<IndexedApplication>> m_discoverApplicationsOverride;
    private readonly bool m_enableWatchers;
    private readonly object m_watchStateLock = new();
    private readonly Dictionary<string, RootWatchState> m_rootWatchStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<DirectoryInfo> m_macApplicationRoots;
    private readonly IReadOnlyList<DirectoryInfo> m_windowsApplicationRoots;
    private List<IndexedApplication> m_cachedApplications;
    private DateTime m_lastRefreshUtc;
    private DateTime m_lastFullRefreshUtc;

    public ApplicationSearchService()
        : this(new ApplicationSearchSettings(), null, null, OperatingSystem.IsMacOS(), OperatingSystem.IsWindows(), null)
    {
    }

    private ApplicationSearchService(
        ApplicationSearchSettings settings,
        IEnumerable<DirectoryInfo> macApplicationRoots,
        IEnumerable<DirectoryInfo> windowsApplicationRoots,
        bool isMacOS,
        bool isWindows,
        Func<IReadOnlyList<WindowsStartApp>> windowsStartAppsAccessor,
        Func<IReadOnlyList<IndexedApplication>> discoverApplicationsOverride = null)
    {
        m_settings = settings;
        m_isMacOS = isMacOS;
        m_isWindows = isWindows;
        m_windowsStartAppsAccessor = windowsStartAppsAccessor ?? GetWindowsStartApps;
        m_discoverApplicationsOverride = discoverApplicationsOverride;
        m_enableWatchers = true;
        m_macApplicationRoots = macApplicationRoots?.ToArray() ??
                                GetConfiguredOrDefaultMacRoots(settings);
        m_windowsApplicationRoots = windowsApplicationRoots?.ToArray() ??
                                    GetConfiguredOrDefaultWindowsRoots(settings);
        m_cachedApplications = settings?.CachedApplications?.ToList() ?? [];
        m_lastRefreshUtc = settings?.LastApplicationRefreshUtc ?? DateTime.MinValue;
        m_lastFullRefreshUtc = m_lastRefreshUtc;
        ResetWatchers();
    }

    internal ApplicationSearchService(
        IEnumerable<DirectoryInfo> macApplicationRoots,
        IEnumerable<DirectoryInfo> windowsApplicationRoots,
        IEnumerable<IndexedApplication> cachedApplications,
        bool isMacOS,
        bool isWindows,
        DateTime? lastRefreshUtc = null,
        Func<IReadOnlyList<WindowsStartApp>> windowsStartAppsAccessor = null,
        Func<IReadOnlyList<IndexedApplication>> discoverApplicationsOverride = null,
        bool enableWatchers = false)
    {
        m_settings = null;
        m_isMacOS = isMacOS;
        m_isWindows = isWindows;
        m_windowsStartAppsAccessor = windowsStartAppsAccessor ?? GetWindowsStartApps;
        m_discoverApplicationsOverride = discoverApplicationsOverride;
        m_enableWatchers = enableWatchers;
        m_macApplicationRoots = macApplicationRoots?.ToArray() ?? [];
        m_windowsApplicationRoots = windowsApplicationRoots?.ToArray() ?? [];
        m_cachedApplications = cachedApplications?.ToList() ?? [];
        m_lastRefreshUtc = lastRefreshUtc ?? DateTime.MinValue;
        m_lastFullRefreshUtc = m_lastRefreshUtc;
        ResetWatchers();
    }

    public async Task<IReadOnlyList<IndexedApplication>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query) || !IsSupportedPlatform())
            return [];

        if (m_cachedApplications.Count == 0)
            await RefreshAsync(cancellationToken);

        return RankMatches(query, m_cachedApplications);
    }

    public override void Dispose()
    {
        lock (m_watchStateLock)
            DisposeWatchers(m_rootWatchStates, rootWatchState => rootWatchState.Watcher);

        base.Dispose();
    }

    public Task WarmAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupportedPlatform())
            return Task.CompletedTask;

        if (m_cachedApplications.Count > 0 && DateTime.UtcNow - m_lastRefreshUtc <= RefreshInterval)
            return Task.CompletedTask;

        return RefreshAsync(cancellationToken);
    }

    internal Task RefreshNowAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(cancellationToken, forceRefresh: true);

    internal async Task RefreshAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        if (!IsSupportedPlatform())
            return;

        await m_refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && !NeedsRefresh())
                return;

            SetIsRefreshing(true);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                Logger.Instance.Info($"Refreshing application index across {(m_isMacOS ? m_macApplicationRoots.Count : m_windowsApplicationRoots.Count)} root(s).");
                var dirtyRootStates = CaptureAndClearDirtyRootStates();
                Logger.Instance.Info(BuildDirtyStateSummary(dirtyRootStates));

                if (!forceRefresh &&
                    m_cachedApplications.Count > 0 &&
                    !ShouldPerformFullRefresh(dirtyRootStates))
                {
                    m_lastRefreshUtc = DateTime.UtcNow;
                    Logger.Instance.Info($"Application index refresh skipped in {stopwatch.ElapsedMilliseconds:N0} ms. Watcher reported no relevant changes.");
                    return;
                }

                var discoveredApplications = await Task.Run(DiscoverApplications, cancellationToken);
                m_cachedApplications = discoveredApplications.ToList();
                m_lastRefreshUtc = DateTime.UtcNow;
                m_lastFullRefreshUtc = m_lastRefreshUtc;
                SaveIfPersistent();
                Logger.Instance.Info($"Application index refresh completed in {stopwatch.ElapsedMilliseconds:N0} ms. Indexed {m_cachedApplications.Count:N0} applications.");
            }
            finally
            {
                SetIsRefreshing(false);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Instance.Warn("Application index refresh cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Instance.Exception("Application index refresh failed.", ex);
            throw;
        }
        finally
        {
            m_refreshLock.Release();
        }
    }

    private void SaveIfPersistent()
    {
        if (m_settings == null)
            return;

        m_settings.CachedApplications = m_cachedApplications.ToList();
        m_settings.LastApplicationRefreshUtc = m_lastRefreshUtc;
        m_settings.Save();
    }

    private bool IsSupportedPlatform() => m_isMacOS || m_isWindows;

    private static IReadOnlyList<DirectoryInfo> GetConfiguredOrDefaultMacRoots(ApplicationSearchSettings settings)
    {
        var configuredRoots = settings?.MacApplicationRoots?.Where(root => root != null).ToArray();
        return configuredRoots is { Length: > 0 } ? configuredRoots : GetDefaultMacApplicationRoots();
    }

    private static IReadOnlyList<DirectoryInfo> GetConfiguredOrDefaultWindowsRoots(ApplicationSearchSettings settings)
    {
        var configuredRoots = settings?.WindowsApplicationRoots?.Where(root => root != null).ToArray();
        return configuredRoots is { Length: > 0 } ? configuredRoots : GetDefaultWindowsApplicationRoots();
    }

    private static IReadOnlyList<DirectoryInfo> GetDefaultMacApplicationRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            new DirectoryInfo("/Applications"),
            new DirectoryInfo("/System/Applications"),
            new DirectoryInfo(Path.Combine(userProfile, "Applications"))
        ];
    }

    private static IReadOnlyList<DirectoryInfo> GetDefaultWindowsApplicationRoots()
    {
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs)
        }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new DirectoryInfo(path))
            .ToArray();
    }

    internal void MarkPathDirty(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(fullPath));

        MarkPathDirtyCore(fullPath);
    }

    private IReadOnlyList<IndexedApplication> DiscoverApplications()
    {
        if (m_discoverApplicationsOverride != null)
            return m_discoverApplicationsOverride();

        if (m_isMacOS)
            return DiscoverMacApplications();

        if (m_isWindows)
            return DiscoverWindowsApplications();

        return [];
    }

    private bool NeedsRefresh()
    {
        return m_lastRefreshUtc == DateTime.MinValue || DateTime.UtcNow - m_lastRefreshUtc > RefreshInterval;
    }

    private bool ShouldPerformFullRefresh(IReadOnlyDictionary<string, DirtyRootState> dirtyRootStates)
    {
        if (!m_enableWatchers)
            return true;

        if (DateTime.UtcNow - m_lastFullRefreshUtc > FullRefreshInterval)
            return true;

        if (dirtyRootStates == null || dirtyRootStates.Count == 0)
            return true;

        return dirtyRootStates.Any(pair => pair.Value?.IsDirty == true);
    }

    private IReadOnlyList<IndexedApplication> DiscoverMacApplications()
    {
        return m_macApplicationRoots
            .Where(root => root?.Exists() == true)
            .SelectMany(DiscoverMacApplicationBundles)
            .GroupBy(app => app.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                {
                    var bundleDirectory = group.First();
                    var displayName = Path.GetFileNameWithoutExtension(bundleDirectory.Name);
                    return new IndexedApplication
                    {
                        DisplayName = displayName,
                        LaunchKind = ApplicationLaunchKind.OpenPath,
                        SearchName = Normalize(displayName),
                        BundleDirectory = bundleDirectory
                    };
                })
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<DirectoryInfo> DiscoverMacApplicationBundles(DirectoryInfo root)
    {
        var topLevelDirectories = root.TryGetDirs(searchOption: SearchOption.TopDirectoryOnly);
        foreach (var directory in topLevelDirectories)
        {
            if (directory.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                yield return directory;
                continue;
            }

            foreach (var nestedDirectory in directory.TryGetDirs(searchOption: SearchOption.TopDirectoryOnly))
            {
                if (nestedDirectory.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    yield return nestedDirectory;
            }
        }
    }

    private IReadOnlyList<IndexedApplication> DiscoverWindowsApplications()
    {
        return m_windowsApplicationRoots
            .Where(root => root?.Exists() == true)
            .SelectMany(DiscoverWindowsShortcuts)
            .Select(
                shortcutFile =>
                {
                    var displayName = Path.GetFileNameWithoutExtension(shortcutFile.Name);
                    return new IndexedApplication
                    {
                        DisplayName = displayName,
                        LaunchKind = ApplicationLaunchKind.OpenPath,
                        SearchName = Normalize(displayName),
                        ShortcutFile = shortcutFile
                    };
                })
            .Concat(DiscoverWindowsStartApps())
            .GroupBy(app => app.SearchName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(app => app.ShortcutFile != null).First())
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<IndexedApplication> DiscoverWindowsStartApps()
    {
        return m_windowsStartAppsAccessor()
            .Where(app => !string.IsNullOrWhiteSpace(app?.Name) && !string.IsNullOrWhiteSpace(app.AppId))
            .Select(
                app => new IndexedApplication
                {
                    DisplayName = app.Name,
                    LaunchKind = ApplicationLaunchKind.WindowsShellApp,
                    SearchName = Normalize(app.Name),
                    AppUserModelId = app.AppId
                })
            .ToArray();
    }

    private static IEnumerable<FileInfo> DiscoverWindowsShortcuts(DirectoryInfo root)
    {
        return root.TryGetFiles("*.lnk", SearchOption.AllDirectories)
            .Concat(root.TryGetFiles("*.appref-ms", SearchOption.AllDirectories))
            .Where(file => !string.IsNullOrWhiteSpace(file?.Name))
            .DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<IndexedApplication> RankMatches(string query, IEnumerable<IndexedApplication> applications)
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return [];

        return applications
            .Select(app => new { Application = app, Score = Score(normalizedQuery, app.SearchName) })
            .Where(result => result.Score < int.MaxValue)
            .OrderBy(result => result.Score)
            .ThenBy(result => result.Application.DisplayName.Length)
            .ThenBy(result => result.Application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .Select(result => result.Application)
            .ToArray();
    }

    private static int Score(string normalizedQuery, string searchName)
    {
        var acronym = BuildAcronym(searchName);

        if (searchName == normalizedQuery)
            return 0;

        if (searchName.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return 10;

        if (searchName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(word => word.StartsWith(normalizedQuery, StringComparison.Ordinal)))
        {
            return 20;
        }

        var index = searchName.IndexOf(normalizedQuery, StringComparison.Ordinal);
        if (index >= 0)
            return 30 + index;

        if (acronym == normalizedQuery)
            return 200;

        if (acronym.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return 210;

        return int.MaxValue;
    }

    private static string BuildAcronym(string searchName)
    {
        return new string(
            (searchName ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word[0])
                .ToArray());
    }

    private static string Normalize(string text)
    {
        return new string(
            (text ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
                .ToArray());
    }

    private static IReadOnlyList<WindowsStartApp> GetWindowsStartApps()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        try
        {
            using var process = Process.Start(
                new ProcessStartInfo("powershell.exe", "-NoProfile -Command \"Get-StartApps | Select-Object Name, AppID | ConvertTo-Json -Compress\"")
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });

            if (process == null)
                return [];

            var output = process.StandardOutput.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(true);
                return [];
            }

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return [];

            using var document = JsonDocument.Parse(output);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => [CreateWindowsStartApp(document.RootElement)],
                JsonValueKind.Array => document.RootElement
                    .EnumerateArray()
                    .Where(element => element.ValueKind == JsonValueKind.Object)
                    .Select(CreateWindowsStartApp)
                    .Where(app => !string.IsNullOrWhiteSpace(app.Name) && !string.IsNullOrWhiteSpace(app.AppId))
                    .ToArray(),
                _ => []
            };
        }
        catch
        {
            return [];
        }
    }

    private static WindowsStartApp CreateWindowsStartApp(JsonElement element)
    {
        return new WindowsStartApp(
            element.TryGetProperty("Name", out var nameElement) ? nameElement.GetString() : string.Empty,
            element.TryGetProperty("AppID", out var appIdElement) ? appIdElement.GetString() : string.Empty);
    }

    private IEnumerable<DirectoryInfo> GetWatchedRoots()
    {
        return m_isMacOS
            ? m_macApplicationRoots
            : m_isWindows
                ? m_windowsApplicationRoots
                : [];
    }

    private void ResetWatchers()
    {
        lock (m_watchStateLock)
        {
            DisposeWatchers(m_rootWatchStates, rootWatchState => rootWatchState.Watcher);

            foreach (var rootDirectory in GetWatchedRoots().Where(root => root?.Exists() == true))
            {
                var normalizedRootPath = NormalizeRootPath(rootDirectory.FullName);
                m_rootWatchStates[normalizedRootPath] = new RootWatchState(
                    m_enableWatchers ? CreateWatcher(rootDirectory) : null);
            }
        }
    }

    private FileSystemWatcher CreateWatcher(DirectoryInfo rootDirectory)
    {
        return CreateWatcher(
            rootDirectory,
            WatcherBufferSize,
            MarkPathDirtyCore,
            MarkAllRootsDirty);
    }

    private IReadOnlyDictionary<string, DirtyRootState> CaptureAndClearDirtyRootStates()
    {
        lock (m_watchStateLock)
        {
            var dirtyRootStates = new Dictionary<string, DirtyRootState>(StringComparer.OrdinalIgnoreCase);
            foreach (var (rootPath, rootWatchState) in m_rootWatchStates)
            {
                dirtyRootStates[rootPath] = new DirtyRootState(rootWatchState.IsDirty);
                rootWatchState.IsDirty = false;
            }

            return dirtyRootStates;
        }
    }

    private void MarkPathDirtyCore(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return;

        var normalizedPath = NormalizeRootPath(fullPath);
        lock (m_watchStateLock)
        {
            foreach (var (rootPath, rootWatchState) in m_rootWatchStates)
            {
                if (!IsPathCoveredByRoot(normalizedPath, rootPath))
                    continue;

                rootWatchState.IsDirty = true;
                return;
            }
        }
    }

    private void MarkAllRootsDirty(Exception exception)
    {
        lock (m_watchStateLock)
        {
            foreach (var rootWatchState in m_rootWatchStates.Values)
                rootWatchState.IsDirty = true;
        }

        Logger.Instance.Warn($"Application watcher fell back to a full refresh: {exception?.Message}");
    }

    private static string BuildDirtyStateSummary(IReadOnlyDictionary<string, DirtyRootState> dirtyRootStates)
    {
        if (dirtyRootStates == null || dirtyRootStates.Count == 0)
            return "Application watcher hints: no roots are being tracked.";

        var dirtyRoots = dirtyRootStates
            .Where(pair => pair.Value?.IsDirty == true)
            .Select(pair => Path.GetFileName(pair.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Take(3)
            .ToArray();
        var dirtyRootCount = dirtyRootStates.Count(pair => pair.Value?.IsDirty == true);

        if (dirtyRootCount == 0)
            return $"Application watcher hints: {dirtyRootStates.Count:N0} roots tracked, no changes flagged.";

        return dirtyRoots.Length == 0
            ? $"Application watcher hints: {dirtyRootStates.Count:N0} roots tracked, {dirtyRootCount:N0} dirty."
            : $"Application watcher hints: {dirtyRootStates.Count:N0} roots tracked, {dirtyRootCount:N0} dirty. Samples: {string.Join(", ", dirtyRoots)}.";
    }

    internal sealed record WindowsStartApp(string Name, string AppId);

    private sealed class RootWatchState
    {
        public RootWatchState(FileSystemWatcher watcher)
        {
            Watcher = watcher;
        }

        public FileSystemWatcher Watcher { get; }

        public bool IsDirty { get; set; }
    }

    private sealed record DirtyRootState(bool IsDirty);
}
