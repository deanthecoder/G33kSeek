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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DTC.Core.Extensions;
using G33kSeek.Models;

namespace G33kSeek.Services;

/// <summary>
/// Provides cached application discovery and lookup for no-prefix launcher queries.
/// </summary>
/// <remarks>
/// This keeps application search fast by scanning macOS and Windows application sources in the background and serving matches from cached entries.
/// </remarks>
internal sealed class ApplicationSearchService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim m_refreshLock = new(1, 1);
    private readonly ApplicationSearchSettings m_settings;
    private readonly bool m_isMacOS;
    private readonly bool m_isWindows;
    private readonly IReadOnlyList<DirectoryInfo> m_macApplicationRoots;
    private readonly IReadOnlyList<DirectoryInfo> m_windowsApplicationRoots;
    private List<IndexedApplication> m_cachedApplications;
    private DateTime m_lastRefreshUtc;

    public ApplicationSearchService()
        : this(new ApplicationSearchSettings(), null, null, OperatingSystem.IsMacOS(), OperatingSystem.IsWindows())
    {
    }

    private ApplicationSearchService(
        ApplicationSearchSettings settings,
        IEnumerable<DirectoryInfo> macApplicationRoots,
        IEnumerable<DirectoryInfo> windowsApplicationRoots,
        bool isMacOS,
        bool isWindows)
    {
        m_settings = settings;
        m_isMacOS = isMacOS;
        m_isWindows = isWindows;
        m_macApplicationRoots = macApplicationRoots?.ToArray() ??
                                GetConfiguredOrDefaultMacRoots(settings);
        m_windowsApplicationRoots = windowsApplicationRoots?.ToArray() ??
                                    GetConfiguredOrDefaultWindowsRoots(settings);
        m_cachedApplications = settings?.CachedApplications?.ToList() ?? [];
        m_lastRefreshUtc = settings?.LastApplicationRefreshUtc ?? DateTime.MinValue;
    }

    internal ApplicationSearchService(
        IEnumerable<DirectoryInfo> macApplicationRoots,
        IEnumerable<DirectoryInfo> windowsApplicationRoots,
        IEnumerable<IndexedApplication> cachedApplications,
        bool isMacOS,
        bool isWindows,
        DateTime? lastRefreshUtc = null)
    {
        m_settings = null;
        m_isMacOS = isMacOS;
        m_isWindows = isWindows;
        m_macApplicationRoots = macApplicationRoots?.ToArray() ?? [];
        m_windowsApplicationRoots = windowsApplicationRoots?.ToArray() ?? [];
        m_cachedApplications = cachedApplications?.ToList() ?? [];
        m_lastRefreshUtc = lastRefreshUtc ?? DateTime.MinValue;
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
        else if (DateTime.UtcNow - m_lastRefreshUtc > RefreshInterval)
            _ = RefreshAsync(CancellationToken.None);

        return RankMatches(query, m_cachedApplications);
    }

    public Task WarmAsync(CancellationToken cancellationToken = default)
    {
        if (!IsSupportedPlatform())
            return Task.CompletedTask;

        if (m_cachedApplications.Count > 0 && DateTime.UtcNow - m_lastRefreshUtc <= RefreshInterval)
            return Task.CompletedTask;

        return RefreshAsync(cancellationToken);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!IsSupportedPlatform())
            return;

        await m_refreshLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var discoveredApplications = await Task.Run(DiscoverApplications, cancellationToken);
            m_cachedApplications = discoveredApplications.ToList();
            m_lastRefreshUtc = DateTime.UtcNow;
            SaveIfPersistent();
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

    private IReadOnlyList<IndexedApplication> DiscoverApplications()
    {
        if (m_isMacOS)
            return DiscoverMacApplications();

        if (m_isWindows)
            return DiscoverWindowsApplications();

        return [];
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
            .GroupBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(
                group =>
                {
                    var shortcutFile = group.First();
                    var displayName = Path.GetFileNameWithoutExtension(shortcutFile.Name);
                    return new IndexedApplication
                    {
                        DisplayName = displayName,
                        SearchName = Normalize(displayName),
                        ShortcutFile = shortcutFile
                    };
                })
            .OrderBy(app => app.DisplayName, StringComparer.OrdinalIgnoreCase)
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
        return index >= 0 ? 100 + index : int.MaxValue;
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
}
