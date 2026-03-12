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
using System.Threading;
using System.Threading.Tasks;
using DTC.Core;
using DTC.Core.Extensions;
using G33kSeek.Models;

namespace G33kSeek.Services;

/// <summary>
/// Provides cached file discovery and lookup for no-prefix launcher queries.
/// </summary>
/// <remarks>
/// This keeps file search fast by indexing configured roots in the background and serving matches from an in-memory cache.
/// </remarks>
internal sealed class FileSearchService : SearchServiceBase
{
    private const int CurrentCacheFormatVersion = 6;
    private const int MaxVisibleResults = 25;
    private const int WatcherBufferSize = 64 * 1024;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".hg",
        ".svn",
        ".idea",
        ".vs",
        "obj",
        "node_modules",
        "packages"
    };

    private readonly FileSearchSettings m_settings;
    private readonly List<DirectoryInfo> m_searchRoots;
    private readonly Func<CancellationToken, (IReadOnlyList<IndexedFile> Files, int ScannedDirectoryCount, int SkippedDirectoryCount)> m_discoverFilesOverride;
    private readonly bool m_enableWatchers;
    private readonly object m_searchCacheLock = new();
    private readonly object m_watchStateLock = new();
    private readonly Dictionary<string, RootWatchState> m_rootWatchStates = new(StringComparer.OrdinalIgnoreCase);
    private bool m_hasExplicitSearchRoots;
    private List<IndexedDirectorySnapshot> m_cachedDirectorySnapshots;
    private List<IndexedFile> m_cachedFiles;
    private DateTime m_lastRefreshUtc;
    private FileRefreshMetrics m_lastRefreshMetrics = FileRefreshMetrics.Empty;
    private string m_lastSearchQuery = string.Empty;
    private IReadOnlyList<IndexedFile> m_lastSearchMatches = [];

    public FileSearchService()
        : this(new FileSearchSettings(), null, null)
    {
    }

    private FileSearchService(
        FileSearchSettings settings,
        IEnumerable<DirectoryInfo> searchRoots,
        IEnumerable<IndexedFile> cachedFiles,
        Func<CancellationToken, (IReadOnlyList<IndexedFile> Files, int ScannedDirectoryCount, int SkippedDirectoryCount)> discoverFilesOverride = null)
    {
        m_settings = settings;
        m_discoverFilesOverride = discoverFilesOverride;
        m_enableWatchers = true;
        var configuredRoots = settings?.SearchRoots?.Where(root => root != null).ToArray() ?? [];
        m_hasExplicitSearchRoots = configuredRoots.Length > 0;
        m_searchRoots = (searchRoots?.ToArray() ?? (m_hasExplicitSearchRoots ? configuredRoots : GetDefaultSearchRoots(OperatingSystem.IsWindows())))
            .ToList();
        var isCompatibleCache = settings?.CacheFormatVersion == CurrentCacheFormatVersion;
        if (settings != null && !isCompatibleCache)
        {
            Logger.Instance.Info($"Discarding file index cache format v{settings.CacheFormatVersion:N0}; expected v{CurrentCacheFormatVersion:N0}.");
        }

        m_cachedDirectorySnapshots = PrepareDirectorySnapshots(isCompatibleCache ? settings.DirectorySnapshots : null);
        m_cachedFiles = PrepareIndexedFiles(cachedFiles) is { Count: > 0 } preparedCachedFiles
            ? preparedCachedFiles
            : FlattenSnapshots(m_cachedDirectorySnapshots);
        m_lastRefreshUtc = isCompatibleCache ? settings.LastFileRefreshUtc : DateTime.MinValue;
        ResetWatchers();
    }

    internal FileSearchService(
        IEnumerable<DirectoryInfo> searchRoots,
        IEnumerable<IndexedFile> cachedFiles,
        DateTime? lastRefreshUtc = null,
        Func<CancellationToken, (IReadOnlyList<IndexedFile> Files, int ScannedDirectoryCount, int SkippedDirectoryCount)> discoverFilesOverride = null,
        IEnumerable<IndexedDirectorySnapshot> cachedDirectorySnapshots = null,
        bool enableWatchers = false)
    {
        m_settings = null;
        m_discoverFilesOverride = discoverFilesOverride;
        m_enableWatchers = enableWatchers;
        m_searchRoots = searchRoots?.ToList() ?? [];
        m_hasExplicitSearchRoots = m_searchRoots.Count > 0;
        m_cachedDirectorySnapshots = PrepareDirectorySnapshots(cachedDirectorySnapshots);
        m_cachedFiles = PrepareIndexedFiles(cachedFiles) is { Count: > 0 } preparedCachedFiles
            ? preparedCachedFiles
            : FlattenSnapshots(m_cachedDirectorySnapshots);
        m_lastRefreshUtc = lastRefreshUtc ?? DateTime.MinValue;
        ResetWatchers();
    }

    public async Task<FileSearchResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return new FileSearchResult([], 0);

        if (m_cachedFiles.Count == 0)
            await RefreshAsync(cancellationToken);
        else if (DateTime.UtcNow - m_lastRefreshUtc > RefreshInterval)
            _ = RefreshAsync(CancellationToken.None);

        var previousMatchSource = GetCandidateSearchSet(normalizedQuery);
        var result = RankMatches(normalizedQuery, previousMatchSource);
        RememberSearchResult(normalizedQuery, result.MatchingFiles);
        return result;
    }

    internal FileRefreshMetrics LastRefreshMetrics => m_lastRefreshMetrics;

    /// <summary>
    /// Releases any active watchers owned by the file index.
    /// </summary>
    /// <remarks>
    /// Watchers are optional in tests, but in the real app they need to be torn down on exit so they do not outlive the launcher process.
    /// </remarks>
    public override void Dispose()
    {
        lock (m_watchStateLock)
            DisposeWatchers(m_rootWatchStates, rootWatchState => rootWatchState.Watcher);

        base.Dispose();
    }

    public Task WarmAsync(CancellationToken cancellationToken = default)
    {
        if (m_cachedFiles.Count > 0 && DateTime.UtcNow - m_lastRefreshUtc <= RefreshInterval)
            return Task.CompletedTask;

        return RefreshAsync(cancellationToken);
    }

    internal async Task<FileSearchRootAddStatus> AddSearchRootAsync(DirectoryInfo directory, CancellationToken cancellationToken = default)
    {
        if (directory == null)
            throw new ArgumentNullException(nameof(directory));

        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!directory.Exists)
                return FileSearchRootAddStatus.Unavailable;

            if (IsCoveredByExistingRoot(directory, m_searchRoots))
                return FileSearchRootAddStatus.AlreadyCovered;

            m_hasExplicitSearchRoots = true;
            m_searchRoots.Add(directory);
            ReplaceSearchRoots(ConsolidateRoots(m_searchRoots));
            ResetWatchers();

            await RefreshCoreAsync(cancellationToken);
            return FileSearchRootAddStatus.Added;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    internal Task RefreshNowAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(cancellationToken, forceRefresh: true);

    private async Task RefreshAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && !NeedsRefresh())
                return;

            SetIsRefreshing(true);
            try
            {
                await RefreshCoreAsync(cancellationToken);
            }
            finally
            {
                SetIsRefreshing(false);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Instance.Warn("File index refresh cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            Logger.Instance.Exception("File index refresh failed.", ex);
            throw;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private void SaveIfPersistent()
    {
        m_settings?.PersistCache(
            m_searchRoots,
            m_hasExplicitSearchRoots,
            m_cachedDirectorySnapshots,
            m_lastRefreshUtc,
            CurrentCacheFormatVersion);
    }

    internal static IReadOnlyList<DirectoryInfo> GetDefaultSearchRoots(
        bool isWindows = false,
        Func<Environment.SpecialFolder, string> folderPathAccessor = null)
    {
        folderPathAccessor ??= Environment.GetFolderPath;
        var homePath = folderPathAccessor(Environment.SpecialFolder.UserProfile);

        var roots = new[]
        {
            folderPathAccessor(Environment.SpecialFolder.MyDocuments),
            folderPathAccessor(Environment.SpecialFolder.MyPictures),
            string.IsNullOrWhiteSpace(homePath) ? null : Path.Combine(homePath, "Downloads"),
            isWindows ? folderPathAccessor(Environment.SpecialFolder.CommonDocuments) : null
        }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.ToDir())
            .DistinctBy(directory => directory.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return roots;
    }

    private FileDiscoverySnapshot DiscoverFiles(CancellationToken cancellationToken, IReadOnlyDictionary<string, DirtyRootState> dirtyRootStates)
    {
        if (m_discoverFilesOverride != null)
        {
            var overrideResult = m_discoverFilesOverride(cancellationToken);
            return new FileDiscoverySnapshot(
                overrideResult.Files,
                [],
                overrideResult.ScannedDirectoryCount,
                overrideResult.ScannedDirectoryCount,
                0);
        }

        var visitedDirectoryCount = 0;
        var rebuiltDirectoryCount = 0;
        var reusedDirectoryCount = 0;
        var existingSnapshotsByPath = m_cachedDirectorySnapshots
            .Where(snapshot => snapshot?.Directory != null)
            .ToDictionary(snapshot => snapshot.Directory.FullName, StringComparer.OrdinalIgnoreCase);
        var refreshedSnapshots = new List<IndexedDirectorySnapshot>();

        foreach (var rootDirectory in m_searchRoots
                     .Where(root => root?.Exists() == true)
                     .DistinctBy(root => root.FullName, StringComparer.OrdinalIgnoreCase))
        {
            existingSnapshotsByPath.TryGetValue(rootDirectory.FullName, out var existingSnapshot);
            DirtyRootState dirtyRootState = null;
            dirtyRootStates?.TryGetValue(NormalizeRootPath(rootDirectory.FullName), out dirtyRootState);
            var refreshedSnapshot = RefreshDirectorySnapshot(
                rootDirectory,
                existingSnapshot,
                dirtyRootState,
                depth: 0,
                cancellationToken,
                ref visitedDirectoryCount,
                ref rebuiltDirectoryCount,
                ref reusedDirectoryCount);
            if (refreshedSnapshot != null)
                refreshedSnapshots.Add(refreshedSnapshot);
        }

        return new FileDiscoverySnapshot(
            FlattenSnapshots(refreshedSnapshots),
            refreshedSnapshots,
            visitedDirectoryCount,
            rebuiltDirectoryCount,
            reusedDirectoryCount);
    }

    private static bool ShouldIncludeDirectory(DirectoryInfo directory)
    {
        if (directory?.Exists() != true)
            return false;

        if (ExcludedDirectoryNames.Contains(directory.Name))
            return false;

        if (directory.Name.StartsWith(".", StringComparison.Ordinal))
            return false;

        return !HasHiddenOrLinkedAttributes(directory.Attributes);
    }

    private static bool ShouldIncludeFile(FileInfo file)
    {
        if (file?.Exists() != true)
            return false;

        if (file.Name.StartsWith(".", StringComparison.Ordinal))
            return false;

        return !HasHiddenOrLinkedAttributes(file.Attributes);
    }

    private static bool HasHiddenOrLinkedAttributes(FileAttributes attributes)
    {
        return attributes.HasFlag(FileAttributes.Hidden) ||
               attributes.HasFlag(FileAttributes.ReparsePoint);
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();
        Logger.Instance.Info($"Refreshing file index across {m_searchRoots.Count} root(s).");
        var dirtyRootStates = CaptureAndClearDirtyRootStates();
        Logger.Instance.Info(BuildDirtyStateSummary(dirtyRootStates));

        var discoverStopwatch = Stopwatch.StartNew();
        var snapshot = await Task.Run(() => DiscoverFiles(cancellationToken, dirtyRootStates), cancellationToken);
        discoverStopwatch.Stop();

        var cacheBuildStopwatch = Stopwatch.StartNew();
        m_cachedFiles = snapshot.Files.ToList();
        m_cachedDirectorySnapshots = snapshot.DirectorySnapshots.ToList();
        ResetSearchCache();
        cacheBuildStopwatch.Stop();

        m_lastRefreshUtc = DateTime.UtcNow;
        SaveIfPersistent();
        m_lastRefreshMetrics = new FileRefreshMetrics(
            snapshot.VisitedDirectoryCount,
            snapshot.RebuiltDirectoryCount,
            snapshot.ReusedDirectoryCount);
        Logger.Instance.Info(
            $"File index refresh completed in {stopwatch.ElapsedMilliseconds:N0} ms. Discover {discoverStopwatch.ElapsedMilliseconds:N0} ms. Indexed {snapshot.Files.Count:N0} items from {snapshot.VisitedDirectoryCount:N0} visited directories ({snapshot.RebuiltDirectoryCount:N0} rebuilt, {snapshot.ReusedDirectoryCount:N0} reused).");
    }

    private bool NeedsRefresh()
    {
        return m_lastRefreshUtc == DateTime.MinValue || DateTime.UtcNow - m_lastRefreshUtc > RefreshInterval;
    }

    private void ReplaceSearchRoots(IEnumerable<DirectoryInfo> directories)
    {
        m_searchRoots.Clear();
        m_searchRoots.AddRange(directories.Where(directory => directory != null));
    }

    internal void MarkPathDirty(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(fullPath));

        MarkPathDirtyCore(fullPath);
    }

    private static IReadOnlyList<DirectoryInfo> ConsolidateRoots(IEnumerable<DirectoryInfo> roots)
    {
        var consolidatedRoots = new List<DirectoryInfo>();
        foreach (var root in roots
                     .Where(directory => directory != null)
                     .OrderBy(directory => NormalizeRootPath(directory.FullName).Length))
        {
            if (IsCoveredByExistingRoot(root, consolidatedRoots))
                continue;

            consolidatedRoots.RemoveAll(existingRoot => IsPathCoveredByRoot(existingRoot.FullName, root.FullName));
            consolidatedRoots.Add(root);
        }

        return consolidatedRoots;
    }

    private static bool IsCoveredByExistingRoot(DirectoryInfo candidateRoot, IEnumerable<DirectoryInfo> existingRoots)
    {
        return existingRoots.Any(existingRoot => IsPathCoveredByRoot(candidateRoot.FullName, existingRoot.FullName));
    }

    private static FileSearchResult RankMatches(string normalizedQuery, IEnumerable<IndexedFile> files)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return new FileSearchResult([], 0);

        var queryCharacterMask = BuildCharacterMask(normalizedQuery);
        var totalMatchCount = 0;
        var matchingFiles = new List<IndexedFile>();
        var bestMatches = new List<(IndexedFile File, FileSortKey SortKey)>(MaxVisibleResults);

        foreach (var file in files)
        {
            if ((file.SearchTextCharacterMask & queryCharacterMask) != queryCharacterMask)
            {
                continue;
            }

            if (!file.SearchText.Contains(normalizedQuery, StringComparison.Ordinal))
                continue;

            var sortKey = BuildSortKey(normalizedQuery, file);
            if (sortKey == null)
                continue;

            totalMatchCount++;
            matchingFiles.Add(file);
            InsertIntoBestMatches(bestMatches, (file, sortKey));
        }

        return new FileSearchResult(bestMatches.Select(match => match.File).ToArray(), totalMatchCount, matchingFiles);
    }

    private static FileSortKey BuildSortKey(string normalizedQuery, IndexedFile file)
    {
        var displayName = file.NormalizedDisplayName;
        var displayNameIndex = displayName.IndexOf(normalizedQuery, StringComparison.Ordinal);
        var segmentScore = GetPathSegmentMatchScore(normalizedQuery, file, out var segmentDistanceFromLeaf);
        var typePriority = displayName == normalizedQuery && !file.IsDirectory ? 0 : file.IsDirectory ? 1 : 0;

        if (displayName == normalizedQuery)
            return new FileSortKey(0, typePriority, 0, segmentScore, segmentDistanceFromLeaf);

        if (displayName.StartsWith(normalizedQuery, StringComparison.Ordinal))
            return new FileSortKey(1, typePriority, 0, segmentScore, segmentDistanceFromLeaf);

        if (file.DisplayNameWords.Any(word => word.StartsWith(normalizedQuery, StringComparison.Ordinal)))
        {
            return new FileSortKey(2, typePriority, 0, segmentScore, segmentDistanceFromLeaf);
        }

        if (displayNameIndex >= 0)
            return new FileSortKey(3, typePriority, displayNameIndex, segmentScore, segmentDistanceFromLeaf);

        if (segmentScore > 0)
            return new FileSortKey(4, typePriority, int.MaxValue, segmentScore, segmentDistanceFromLeaf);

        return null;
    }

    private static void InsertIntoBestMatches(
        List<(IndexedFile File, FileSortKey SortKey)> bestMatches,
        (IndexedFile File, FileSortKey SortKey) candidate)
    {
        var insertIndex = 0;
        while (insertIndex < bestMatches.Count && Compare(candidate, bestMatches[insertIndex]) >= 0)
            insertIndex++;

        if (insertIndex >= MaxVisibleResults)
            return;

        bestMatches.Insert(insertIndex, candidate);
        if (bestMatches.Count > MaxVisibleResults)
            bestMatches.RemoveAt(bestMatches.Count - 1);
    }

    private static int Compare(
        (IndexedFile File, FileSortKey SortKey) left,
        (IndexedFile File, FileSortKey SortKey) right)
    {
        var comparison = left.SortKey.MatchTier.CompareTo(right.SortKey.MatchTier);
        if (comparison != 0)
            return comparison;

        comparison = left.SortKey.TypePriority.CompareTo(right.SortKey.TypePriority);
        if (comparison != 0)
            return comparison;

        comparison = left.SortKey.DisplayNameIndex.CompareTo(right.SortKey.DisplayNameIndex);
        if (comparison != 0)
            return comparison;

        comparison = right.SortKey.PathSegmentMatchScore.CompareTo(left.SortKey.PathSegmentMatchScore);
        if (comparison != 0)
            return comparison;

        comparison = left.SortKey.SegmentDistanceFromLeaf.CompareTo(right.SortKey.SegmentDistanceFromLeaf);
        if (comparison != 0)
            return comparison;

        comparison = left.File.DisplayName.Length.CompareTo(right.File.DisplayName.Length);
        if (comparison != 0)
            return comparison;

        return StringComparer.OrdinalIgnoreCase.Compare(left.File.DisplayName, right.File.DisplayName);
    }

    private static string Normalize(string text)
    {
        return new string(
            (text ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) || character == '.')
                .ToArray());
    }

    private static ulong BuildCharacterMask(string normalizedText)
    {
        var mask = 0UL;
        foreach (var character in normalizedText ?? string.Empty)
            mask |= GetCharacterBit(character);

        return mask;
    }

    private static ulong GetCharacterBit(char character)
    {
        return character switch
        {
            >= 'a' and <= 'z' => 1UL << (character - 'a'),
            >= '0' and <= '9' => 1UL << (26 + character - '0'),
            '.' => 1UL << 36,
            ' ' => 1UL << 37,
            _ => 1UL << 38
        };
    }

    private static int GetPathSegmentMatchScore(string normalizedQuery, IndexedFile file, out int segmentDistanceFromLeaf)
    {
        segmentDistanceFromLeaf = int.MaxValue;
        var pathSegments = file.NormalizedPathSegments;
        var bestScore = 0;

        for (var index = 0; index < pathSegments.Length; index++)
        {
            var normalizedSegment = pathSegments[index];
            if (string.IsNullOrWhiteSpace(normalizedSegment))
                continue;

            var segmentScore = normalizedSegment == normalizedQuery
                ? 3
                : normalizedSegment.StartsWith(normalizedQuery, StringComparison.Ordinal)
                    ? 2
                    : normalizedSegment.Contains(normalizedQuery, StringComparison.Ordinal)
                        ? 1
                        : 0;

            if (segmentScore <= 0)
                continue;

            var distance = pathSegments.Length - 1 - index;
            if (segmentScore > bestScore || (segmentScore == bestScore && distance < segmentDistanceFromLeaf))
            {
                bestScore = segmentScore;
                segmentDistanceFromLeaf = distance;
            }
        }

        return bestScore;
    }

    private static IndexedDirectorySnapshot RefreshDirectorySnapshot(
        DirectoryInfo directory,
        IndexedDirectorySnapshot existingSnapshot,
        DirtyRootState dirtyRootState,
        int depth,
        CancellationToken cancellationToken,
        ref int visitedDirectoryCount,
        ref int rebuiltDirectoryCount,
        ref int reusedDirectoryCount)
    {
        if (directory?.Exists() != true)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        directory.Refresh();
        visitedDirectoryCount++;

        var currentLastWriteTimeUtc = directory.LastWriteTimeUtc;
        if (CanReuseWholeSnapshotWithoutTraversal(directory, existingSnapshot, dirtyRootState, depth, currentLastWriteTimeUtc))
        {
            reusedDirectoryCount++;
            return existingSnapshot;
        }

        var isUnchanged = existingSnapshot != null &&
                          existingSnapshot.Directory?.FullName.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase) == true &&
                          existingSnapshot.LastWriteTimeUtc == currentLastWriteTimeUtc;

        var existingChildrenByPath = existingSnapshot?.Children?
            .Where(child => child?.Directory != null)
            .ToDictionary(child => child.Directory.FullName, StringComparer.OrdinalIgnoreCase);

        if (isUnchanged)
        {
            reusedDirectoryCount++;
            var refreshedChildren = new List<IndexedDirectorySnapshot>();
            try
            {
                foreach (var childDirectory in directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    if (!ShouldIncludeDirectory(childDirectory))
                        continue;

                    if (TryReuseCleanTopLevelBucket(
                            childDirectory,
                            existingChildrenByPath,
                            dirtyRootState,
                            depth,
                            ref reusedDirectoryCount,
                            out var reusedChildSnapshot))
                    {
                        refreshedChildren.Add(reusedChildSnapshot);
                        continue;
                    }

                    IndexedDirectorySnapshot existingChildSnapshot = null;
                    existingChildrenByPath?.TryGetValue(childDirectory.FullName, out existingChildSnapshot);
                    var refreshedChild = RefreshDirectorySnapshot(
                        childDirectory,
                        existingChildSnapshot,
                        dirtyRootState,
                        depth + 1,
                        cancellationToken,
                        ref visitedDirectoryCount,
                        ref rebuiltDirectoryCount,
                        ref reusedDirectoryCount);
                    if (refreshedChild != null)
                        refreshedChildren.Add(refreshedChild);
                }
            }
            catch
            {
                return null;
            }

            return new IndexedDirectorySnapshot
            {
                Directory = directory,
                LastWriteTimeUtc = currentLastWriteTimeUtc,
                Entries = existingSnapshot.Entries ?? [],
                Children = refreshedChildren
            };
        }

        rebuiltDirectoryCount++;
        var directEntries = new List<IndexedFile>();
        var childSnapshots = new List<IndexedDirectorySnapshot>();

        try
        {
            foreach (var childDirectory in directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                if (!ShouldIncludeDirectory(childDirectory))
                    continue;

                directEntries.Add(CreateIndexedDirectory(childDirectory));

                if (TryReuseCleanTopLevelBucket(
                        childDirectory,
                        existingChildrenByPath,
                        dirtyRootState,
                        depth,
                        ref reusedDirectoryCount,
                        out var reusedChildSnapshot))
                {
                    childSnapshots.Add(reusedChildSnapshot);
                    continue;
                }

                IndexedDirectorySnapshot existingChildSnapshot = null;
                existingChildrenByPath?.TryGetValue(childDirectory.FullName, out existingChildSnapshot);
                var refreshedChild = RefreshDirectorySnapshot(
                    childDirectory,
                    existingChildSnapshot,
                    dirtyRootState,
                    depth + 1,
                    cancellationToken,
                    ref visitedDirectoryCount,
                    ref rebuiltDirectoryCount,
                    ref reusedDirectoryCount);
                if (refreshedChild != null)
                    childSnapshots.Add(refreshedChild);
            }

            foreach (var childFile in directory.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                if (!ShouldIncludeFile(childFile))
                    continue;

                directEntries.Add(CreateIndexedFile(childFile));
            }
        }
        catch
        {
            return null;
        }

        return new IndexedDirectorySnapshot
        {
            Directory = directory,
            LastWriteTimeUtc = currentLastWriteTimeUtc,
            Entries = directEntries,
            Children = childSnapshots
        };
    }

    private sealed record FileDiscoverySnapshot(
        IReadOnlyList<IndexedFile> Files,
        IReadOnlyList<IndexedDirectorySnapshot> DirectorySnapshots,
        int VisitedDirectoryCount,
        int RebuiltDirectoryCount,
        int ReusedDirectoryCount);

    private sealed record FileSortKey(
        int MatchTier,
        int TypePriority,
        int DisplayNameIndex,
        int PathSegmentMatchScore,
        int SegmentDistanceFromLeaf);

    private static List<IndexedFile> PrepareIndexedFiles(IEnumerable<IndexedFile> cachedFiles)
    {
        if (cachedFiles == null)
            return [];

        var preparedFiles = new List<IndexedFile>();
        foreach (var cachedFile in cachedFiles.Where(file => file != null))
        {
            PopulateSearchMetadata(cachedFile);
            preparedFiles.Add(cachedFile);
        }

        return preparedFiles;
    }

    private static List<IndexedDirectorySnapshot> PrepareDirectorySnapshots(IEnumerable<IndexedDirectorySnapshot> cachedDirectorySnapshots)
    {
        if (cachedDirectorySnapshots == null)
            return [];

        var preparedSnapshots = new List<IndexedDirectorySnapshot>();
        foreach (var cachedDirectorySnapshot in cachedDirectorySnapshots.Where(snapshot => snapshot?.Directory != null))
        {
            cachedDirectorySnapshot.Entries = PrepareIndexedFiles(cachedDirectorySnapshot.Entries);
            cachedDirectorySnapshot.Children = PrepareDirectorySnapshots(cachedDirectorySnapshot.Children);
            preparedSnapshots.Add(cachedDirectorySnapshot);
        }

        return preparedSnapshots;
    }

    private static List<IndexedFile> FlattenSnapshots(IEnumerable<IndexedDirectorySnapshot> snapshots)
    {
        var flattenedFiles = new List<IndexedFile>();
        foreach (var snapshot in snapshots ?? [])
            AddSnapshotEntries(snapshot, flattenedFiles);

        return flattenedFiles;
    }

    private static void AddSnapshotEntries(IndexedDirectorySnapshot snapshot, List<IndexedFile> flattenedFiles)
    {
        if (snapshot == null)
            return;

        if (snapshot.Entries != null)
            flattenedFiles.AddRange(snapshot.Entries);

        if (snapshot.Children == null)
            return;

        foreach (var childSnapshot in snapshot.Children)
            AddSnapshotEntries(childSnapshot, flattenedFiles);
    }

    private static IndexedFile CreateIndexedDirectory(DirectoryInfo directory)
    {
        var indexedFile = new IndexedFile
        {
            DisplayName = directory.Name,
            Directory = directory
        };
        PopulateSearchMetadata(indexedFile);
        return indexedFile;
    }

    private static IndexedFile CreateIndexedFile(FileInfo file)
    {
        var indexedFile = new IndexedFile
        {
            DisplayName = file.Name,
            File = file
        };
        PopulateSearchMetadata(indexedFile);
        return indexedFile;
    }

    private static void PopulateSearchMetadata(IndexedFile indexedFile)
    {
        var fullPath = indexedFile.FullPath;
        indexedFile.SearchText = Normalize($"{indexedFile.DisplayName} {fullPath} {indexedFile.SearchText}");
        indexedFile.SearchTextCharacterMask = BuildCharacterMask(indexedFile.SearchText);
        indexedFile.NormalizedDisplayName = Normalize(indexedFile.DisplayName);
        indexedFile.DisplayNameWords = SplitWords(indexedFile.NormalizedDisplayName);
        indexedFile.PathSegments = SplitPathSegments(fullPath);
        indexedFile.NormalizedPathSegments = indexedFile.PathSegments
            .Select(Normalize)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
    }

    private IReadOnlyList<IndexedFile> GetCandidateSearchSet(string normalizedQuery)
    {
        lock (m_searchCacheLock)
        {
            if (!string.IsNullOrWhiteSpace(m_lastSearchQuery) &&
                normalizedQuery.Length > m_lastSearchQuery.Length &&
                normalizedQuery.StartsWith(m_lastSearchQuery, StringComparison.Ordinal))
            {
                return m_lastSearchMatches;
            }
        }

        return m_cachedFiles;
    }

    private void RememberSearchResult(string normalizedQuery, IReadOnlyList<IndexedFile> matchingFiles)
    {
        lock (m_searchCacheLock)
        {
            m_lastSearchQuery = normalizedQuery ?? string.Empty;
            m_lastSearchMatches = matchingFiles ?? [];
        }
    }

    private void ResetSearchCache()
    {
        lock (m_searchCacheLock)
        {
            m_lastSearchQuery = string.Empty;
            m_lastSearchMatches = [];
        }
    }

    private static string[] SplitWords(string normalizedText)
    {
        return normalizedText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string[] SplitPathSegments(string fullPath)
    {
        return fullPath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
    }

    internal sealed record FileRefreshMetrics(
        int VisitedDirectoryCount,
        int RebuiltDirectoryCount,
        int ReusedDirectoryCount)
    {
        public static FileRefreshMetrics Empty { get; } = new(0, 0, 0);
    }

    /// <summary>
    /// Determines whether an existing snapshot can be reused without even enumerating child directories.
    /// </summary>
    /// <remarks>
    /// Root directories can be reused wholesale when the watcher has not marked any top-level buckets or root entries dirty.
    /// First-level directories can also be reused wholesale when their bucket has not been marked dirty.
    /// Deeper levels are handled by normal recursive traversal.
    /// </remarks>
    private static bool CanReuseWholeSnapshotWithoutTraversal(
        DirectoryInfo directory,
        IndexedDirectorySnapshot existingSnapshot,
        DirtyRootState dirtyRootState,
        int depth,
        DateTime currentLastWriteTimeUtc)
    {
        if (existingSnapshot == null ||
            existingSnapshot.Directory?.FullName.Equals(directory.FullName, StringComparison.OrdinalIgnoreCase) != true ||
            existingSnapshot.LastWriteTimeUtc != currentLastWriteTimeUtc)
        {
            return false;
        }

        if (depth == 0)
        {
            return dirtyRootState is { IsWholeRootDirty: false, AreRootEntriesDirty: false } &&
                   dirtyRootState.DirtyTopLevelDirectories.Count == 0;
        }

        if (depth == 1)
            return dirtyRootState is { IsWholeRootDirty: false } && !dirtyRootState.DirtyTopLevelDirectories.Contains(directory.Name);

        return false;
    }

    /// <summary>
    /// Reuses a top-level child snapshot when the watcher says that bucket stayed clean.
    /// </summary>
    /// <remarks>
    /// This is the key optimization for large roots such as a source tree: a build in one repository should not force every sibling repository to be revisited.
    /// </remarks>
    private static bool TryReuseCleanTopLevelBucket(
        DirectoryInfo childDirectory,
        IReadOnlyDictionary<string, IndexedDirectorySnapshot> existingChildrenByPath,
        DirtyRootState dirtyRootState,
        int depth,
        ref int reusedDirectoryCount,
        out IndexedDirectorySnapshot reusedChildSnapshot)
    {
        reusedChildSnapshot = null;
        if (depth != 0 ||
            dirtyRootState is not { IsWholeRootDirty: false } ||
            dirtyRootState.DirtyTopLevelDirectories.Contains(childDirectory.Name) ||
            existingChildrenByPath == null ||
            !existingChildrenByPath.TryGetValue(childDirectory.FullName, out var existingChildSnapshot))
        {
            return false;
        }

        reusedDirectoryCount++;
        reusedChildSnapshot = existingChildSnapshot;
        return true;
    }

    /// <summary>
    /// Rebuilds the watcher set so it matches the current search roots.
    /// </summary>
    /// <remarks>
    /// A watcher is created per root, not per directory. Dirty tracking is handled by recording the first directory segment beneath that root.
    /// </remarks>
    private void ResetWatchers()
    {
        lock (m_watchStateLock)
        {
            DisposeWatchers(m_rootWatchStates, rootWatchState => rootWatchState.Watcher);

            foreach (var rootDirectory in m_searchRoots.Where(root => root?.Exists() == true))
            {
                var normalizedRootPath = NormalizeRootPath(rootDirectory.FullName);
                m_rootWatchStates[normalizedRootPath] = new RootWatchState(
                    m_enableWatchers ? CreateWatcher(rootDirectory) : null);
            }
        }
    }

    /// <summary>
    /// Creates a recursive watcher for one search root.
    /// </summary>
    /// <remarks>
    /// Watcher events are treated as hints only. They do not directly mutate the index; instead they mark buckets dirty so the next refresh can limit its work.
    /// </remarks>
    private FileSystemWatcher CreateWatcher(DirectoryInfo rootDirectory)
    {
        return CreateWatcher(
            rootDirectory,
            WatcherBufferSize,
            MarkPathDirtyCore,
            exception => MarkRootDirtyForWatcherError(rootDirectory.FullName, exception));
    }

    /// <summary>
    /// Captures the current dirty-state hints and clears them for the next refresh cycle.
    /// </summary>
    /// <remarks>
    /// Refresh works against a stable snapshot of watcher state so file events arriving mid-refresh are deferred to the following cycle.
    /// </remarks>
    private IReadOnlyDictionary<string, DirtyRootState> CaptureAndClearDirtyRootStates()
    {
        lock (m_watchStateLock)
        {
            var dirtyRootStates = new Dictionary<string, DirtyRootState>(StringComparer.OrdinalIgnoreCase);
            foreach (var (rootPath, rootWatchState) in m_rootWatchStates)
            {
                dirtyRootStates[rootPath] = new DirtyRootState(
                    rootWatchState.IsWholeRootDirty,
                    rootWatchState.AreRootEntriesDirty,
                    [.. rootWatchState.DirtyTopLevelDirectories]);

                rootWatchState.IsWholeRootDirty = false;
                rootWatchState.AreRootEntriesDirty = false;
                rootWatchState.DirtyTopLevelDirectories.Clear();
            }

            return dirtyRootStates;
        }
    }

    /// <summary>
    /// Marks the affected root bucket for a changed path.
    /// </summary>
    /// <remarks>
    /// Root-level files are tracked separately, while nested changes are collapsed to the first directory segment under the root.
    /// That keeps the bookkeeping cheap while still avoiding a full-root rebuild for unrelated sibling trees.
    /// </remarks>
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

                var relativePath = normalizedPath.Length == rootPath.Length
                    ? string.Empty
                    : normalizedPath[(rootPath.Length + 1)..];

                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    rootWatchState.AreRootEntriesDirty = true;
                    return;
                }

                if (ShouldIgnoreDirtyPath(relativePath))
                    return;

                var firstSeparatorIndex = relativePath.IndexOf(Path.DirectorySeparatorChar);
                if (firstSeparatorIndex < 0)
                {
                    rootWatchState.AreRootEntriesDirty = true;
                    rootWatchState.DirtyTopLevelDirectories.Add(relativePath);
                    return;
                }

                rootWatchState.DirtyTopLevelDirectories.Add(relativePath[..firstSeparatorIndex]);
                return;
            }
        }
    }

    private static bool ShouldIgnoreDirtyPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return false;

        var pathSegments = relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(segment => !string.IsNullOrWhiteSpace(segment));

        foreach (var pathSegment in pathSegments)
        {
            if (ExcludedDirectoryNames.Contains(pathSegment))
                return true;

            if (pathSegment.StartsWith(".", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Falls back to a full-root refresh when watcher delivery becomes unreliable.
    /// </summary>
    /// <remarks>
    /// File watchers can overflow or error under heavy churn. In that case we stop trusting bucket-level hints for that root until the next refresh rebuilds from disk.
    /// </remarks>
    private void MarkRootDirtyForWatcherError(string rootPath, Exception exception)
    {
        var normalizedRootPath = NormalizeRootPath(rootPath);
        lock (m_watchStateLock)
        {
            if (m_rootWatchStates.TryGetValue(normalizedRootPath, out var rootWatchState))
            {
                rootWatchState.IsWholeRootDirty = true;
                rootWatchState.AreRootEntriesDirty = true;
                rootWatchState.DirtyTopLevelDirectories.Clear();
            }
        }

        Logger.Instance.Warn($"File watcher fell back to a full refresh for {rootPath}: {exception?.Message}");
    }

    private static string BuildDirtyStateSummary(IReadOnlyDictionary<string, DirtyRootState> dirtyRootStates)
    {
        if (dirtyRootStates == null || dirtyRootStates.Count == 0)
            return "File watcher hints: no roots are being tracked.";

        var trackedRootCount = dirtyRootStates.Count;
        var dirtyRootCount = 0;
        var fullRootFallbackCount = 0;
        var dirtySamples = new List<string>();

        foreach (var (rootPath, dirtyRootState) in dirtyRootStates)
        {
            if (dirtyRootState == null)
                continue;

            var hasChanges = dirtyRootState.IsWholeRootDirty ||
                             dirtyRootState.AreRootEntriesDirty ||
                             dirtyRootState.DirtyTopLevelDirectories.Count > 0;
            if (!hasChanges)
                continue;

            dirtyRootCount++;
            if (dirtyRootState.IsWholeRootDirty)
                fullRootFallbackCount++;

            if (dirtySamples.Count >= 3)
                continue;

            var rootLabel = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            rootLabel = string.IsNullOrWhiteSpace(rootLabel) ? rootPath : rootLabel;

            if (dirtyRootState.IsWholeRootDirty)
            {
                dirtySamples.Add($"{rootLabel} (full refresh)");
                continue;
            }

            if (dirtyRootState.AreRootEntriesDirty)
                dirtySamples.Add($"{rootLabel} (root entries)");

            foreach (var bucketName in dirtyRootState.DirtyTopLevelDirectories.OrderBy(name => name))
            {
                if (dirtySamples.Count >= 3)
                    break;

                dirtySamples.Add($"{rootLabel}/{bucketName}");
            }
        }

        if (dirtyRootCount == 0)
            return $"File watcher hints: {trackedRootCount:N0} roots tracked, no changes flagged.";

        var summary = $"{trackedRootCount:N0} roots tracked, {dirtyRootCount:N0} dirty.";
        if (fullRootFallbackCount > 0)
        {
            summary += $" Full refresh required for {fullRootFallbackCount:N0} root(s) after watcher error.";
        }

        if (dirtySamples.Count == 0)
            return $"File watcher hints: {summary}";

        return $"File watcher hints: {summary} Samples: {string.Join(", ", dirtySamples)}.";
    }

    private sealed class RootWatchState
    {
        public RootWatchState(FileSystemWatcher watcher)
        {
            Watcher = watcher;
        }

        public FileSystemWatcher Watcher { get; }

        public bool IsWholeRootDirty { get; set; }

        public bool AreRootEntriesDirty { get; set; }

        public HashSet<string> DirtyTopLevelDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record DirtyRootState(
        bool IsWholeRootDirty,
        bool AreRootEntriesDirty,
        HashSet<string> DirtyTopLevelDirectories);
}
