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
internal sealed class FileSearchService
{
    private const int CurrentCacheFormatVersion = 6;
    private const int MaxVisibleResults = 25;
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

    private readonly SemaphoreSlim m_refreshLock = new(1, 1);
    private readonly FileSearchSettings m_settings;
    private readonly List<DirectoryInfo> m_searchRoots;
    private readonly Func<CancellationToken, (IReadOnlyList<IndexedFile> Files, int ScannedDirectoryCount, int SkippedDirectoryCount)> m_discoverFilesOverride;
    private bool m_hasExplicitSearchRoots;
    private List<IndexedDirectorySnapshot> m_cachedDirectorySnapshots;
    private List<IndexedFile> m_cachedFiles;
    private DateTime m_lastRefreshUtc;
    private bool m_isRefreshing;
    private FileRefreshMetrics m_lastRefreshMetrics = FileRefreshMetrics.Empty;

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
    }

    internal FileSearchService(
        IEnumerable<DirectoryInfo> searchRoots,
        IEnumerable<IndexedFile> cachedFiles,
        DateTime? lastRefreshUtc = null,
        Func<CancellationToken, (IReadOnlyList<IndexedFile> Files, int ScannedDirectoryCount, int SkippedDirectoryCount)> discoverFilesOverride = null,
        IEnumerable<IndexedDirectorySnapshot> cachedDirectorySnapshots = null)
    {
        m_settings = null;
        m_discoverFilesOverride = discoverFilesOverride;
        m_searchRoots = searchRoots?.ToList() ?? [];
        m_hasExplicitSearchRoots = m_searchRoots.Count > 0;
        m_cachedDirectorySnapshots = PrepareDirectorySnapshots(cachedDirectorySnapshots);
        m_cachedFiles = PrepareIndexedFiles(cachedFiles) is { Count: > 0 } preparedCachedFiles
            ? preparedCachedFiles
            : FlattenSnapshots(m_cachedDirectorySnapshots);
        m_lastRefreshUtc = lastRefreshUtc ?? DateTime.MinValue;
    }

    public async Task<FileSearchResult> SearchAsync(string query, CancellationToken cancellationToken)
    {
        if (query == null)
            throw new ArgumentNullException(nameof(query));

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
            return new FileSearchResult([], 0);

        if (m_cachedFiles.Count == 0)
            await RefreshAsync(cancellationToken);
        else if (DateTime.UtcNow - m_lastRefreshUtc > RefreshInterval)
            _ = RefreshAsync(CancellationToken.None);

        return RankMatches(query, m_cachedFiles);
    }

    internal bool IsRefreshing => m_isRefreshing;

    internal event EventHandler RefreshStateChanged;

    internal FileRefreshMetrics LastRefreshMetrics => m_lastRefreshMetrics;

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

        await m_refreshLock.WaitAsync(cancellationToken);
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

            await RefreshCoreAsync(cancellationToken);
            return FileSearchRootAddStatus.Added;
        }
        finally
        {
            m_refreshLock.Release();
        }
    }

    internal Task RefreshNowAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(cancellationToken, forceRefresh: true);

    private async Task RefreshAsync(CancellationToken cancellationToken, bool forceRefresh = false)
    {
        await m_refreshLock.WaitAsync(cancellationToken);
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
            m_refreshLock.Release();
        }
    }

    private FileSearchSettings.CachePersistenceMetrics SaveIfPersistent()
    {
        if (m_settings == null)
            return null;

        return m_settings.PersistCache(
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

    private FileDiscoverySnapshot DiscoverFiles(CancellationToken cancellationToken)
    {
        if (m_discoverFilesOverride != null)
        {
            var overrideResult = m_discoverFilesOverride(cancellationToken);
            return new FileDiscoverySnapshot(
                overrideResult.Files,
                [],
                overrideResult.ScannedDirectoryCount,
                overrideResult.ScannedDirectoryCount,
                0,
                overrideResult.SkippedDirectoryCount);
        }

        var visitedDirectoryCount = 0;
        var rebuiltDirectoryCount = 0;
        var reusedDirectoryCount = 0;
        var skippedDirectoryCount = 0;
        var existingSnapshotsByPath = m_cachedDirectorySnapshots
            .Where(snapshot => snapshot?.Directory != null)
            .ToDictionary(snapshot => snapshot.Directory.FullName, StringComparer.OrdinalIgnoreCase);
        var refreshedSnapshots = new List<IndexedDirectorySnapshot>();

        foreach (var rootDirectory in m_searchRoots
                     .Where(root => root?.Exists() == true)
                     .DistinctBy(root => root.FullName, StringComparer.OrdinalIgnoreCase))
        {
            existingSnapshotsByPath.TryGetValue(rootDirectory.FullName, out var existingSnapshot);
            var refreshedSnapshot = RefreshDirectorySnapshot(
                rootDirectory,
                existingSnapshot,
                cancellationToken,
                ref visitedDirectoryCount,
                ref rebuiltDirectoryCount,
                ref reusedDirectoryCount,
                ref skippedDirectoryCount);
            if (refreshedSnapshot != null)
                refreshedSnapshots.Add(refreshedSnapshot);
        }

        return new FileDiscoverySnapshot(
            FlattenSnapshots(refreshedSnapshots),
            refreshedSnapshots,
            visitedDirectoryCount,
            rebuiltDirectoryCount,
            reusedDirectoryCount,
            skippedDirectoryCount);
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

        var discoverStopwatch = Stopwatch.StartNew();
        var snapshot = await Task.Run(() => DiscoverFiles(cancellationToken), cancellationToken);
        discoverStopwatch.Stop();

        var cacheBuildStopwatch = Stopwatch.StartNew();
        m_cachedFiles = snapshot.Files.ToList();
        m_cachedDirectorySnapshots = snapshot.DirectorySnapshots.ToList();
        cacheBuildStopwatch.Stop();

        m_lastRefreshUtc = DateTime.UtcNow;
        var persistenceMetrics = SaveIfPersistent();
        m_lastRefreshMetrics = new FileRefreshMetrics(
            snapshot.VisitedDirectoryCount,
            snapshot.RebuiltDirectoryCount,
            snapshot.ReusedDirectoryCount,
            snapshot.SkippedDirectoryCount);

        var persistenceSummary = persistenceMetrics == null
            ? "Persistence skipped."
            : $"Persistence: serialize {persistenceMetrics.SerializeDuration.TotalMilliseconds:N0} ms, compress {persistenceMetrics.CompressDuration.TotalMilliseconds:N0} ms, save {persistenceMetrics.SaveDuration.TotalMilliseconds:N0} ms, payload {persistenceMetrics.SerializedCharacterCount:N0} chars -> {persistenceMetrics.CompressedByteCount:N0} bytes.";

        Logger.Instance.Info(
            $"File index refresh completed in {stopwatch.ElapsedMilliseconds:N0} ms. Discover {discoverStopwatch.ElapsedMilliseconds:N0} ms, cache build {cacheBuildStopwatch.ElapsedMilliseconds:N0} ms. Indexed {snapshot.Files.Count:N0} items from {snapshot.VisitedDirectoryCount:N0} visited directories ({snapshot.RebuiltDirectoryCount:N0} rebuilt, {snapshot.ReusedDirectoryCount:N0} reused) and skipped {snapshot.SkippedDirectoryCount:N0} excluded directories. {persistenceSummary}");
    }

    private bool NeedsRefresh()
    {
        return m_lastRefreshUtc == DateTime.MinValue || DateTime.UtcNow - m_lastRefreshUtc > RefreshInterval;
    }

    private void SetIsRefreshing(bool isRefreshing)
    {
        if (m_isRefreshing == isRefreshing)
            return;

        m_isRefreshing = isRefreshing;
        RefreshStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ReplaceSearchRoots(IEnumerable<DirectoryInfo> directories)
    {
        m_searchRoots.Clear();
        m_searchRoots.AddRange(directories.Where(directory => directory != null));
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

    private static bool IsPathCoveredByRoot(string candidatePath, string rootPath)
    {
        var normalizedCandidatePath = NormalizeRootPath(candidatePath);
        var normalizedRootPath = NormalizeRootPath(rootPath);

        return normalizedCandidatePath.Equals(normalizedRootPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidatePath.StartsWith($"{normalizedRootPath}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRootPath(string path)
    {
        return Path.GetFullPath(path ?? string.Empty)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static FileSearchResult RankMatches(string query, IEnumerable<IndexedFile> files)
    {
        var normalizedQuery = Normalize(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
            return new FileSearchResult([], 0);

        var totalMatchCount = 0;
        var bestMatches = new List<(IndexedFile File, FileSortKey SortKey)>(MaxVisibleResults);

        foreach (var file in files)
        {
            if (!file.SearchText.Contains(normalizedQuery, StringComparison.Ordinal))
                continue;

            var sortKey = BuildSortKey(normalizedQuery, file);
            if (sortKey == null)
                continue;

            totalMatchCount++;
            InsertIntoBestMatches(bestMatches, (file, sortKey));
        }

        return new FileSearchResult(bestMatches.Select(match => match.File).ToArray(), totalMatchCount);
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

    private IndexedDirectorySnapshot RefreshDirectorySnapshot(
        DirectoryInfo directory,
        IndexedDirectorySnapshot existingSnapshot,
        CancellationToken cancellationToken,
        ref int visitedDirectoryCount,
        ref int rebuiltDirectoryCount,
        ref int reusedDirectoryCount,
        ref int skippedDirectoryCount)
    {
        if (directory?.Exists() != true)
            return null;

        cancellationToken.ThrowIfCancellationRequested();
        directory.Refresh();
        visitedDirectoryCount++;

        var currentLastWriteTimeUtc = directory.LastWriteTimeUtc;
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
                    {
                        skippedDirectoryCount++;
                        continue;
                    }

                    IndexedDirectorySnapshot existingChildSnapshot = null;
                    existingChildrenByPath?.TryGetValue(childDirectory.FullName, out existingChildSnapshot);
                    var refreshedChild = RefreshDirectorySnapshot(
                        childDirectory,
                        existingChildSnapshot,
                        cancellationToken,
                        ref visitedDirectoryCount,
                        ref rebuiltDirectoryCount,
                        ref reusedDirectoryCount,
                        ref skippedDirectoryCount);
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
                {
                    skippedDirectoryCount++;
                    continue;
                }

                directEntries.Add(CreateIndexedDirectory(childDirectory));
                IndexedDirectorySnapshot existingChildSnapshot = null;
                existingChildrenByPath?.TryGetValue(childDirectory.FullName, out existingChildSnapshot);
                var refreshedChild = RefreshDirectorySnapshot(
                    childDirectory,
                    existingChildSnapshot,
                    cancellationToken,
                    ref visitedDirectoryCount,
                    ref rebuiltDirectoryCount,
                    ref reusedDirectoryCount,
                    ref skippedDirectoryCount);
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
        int ReusedDirectoryCount,
        int SkippedDirectoryCount);

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
        indexedFile.NormalizedDisplayName = Normalize(indexedFile.DisplayName);
        indexedFile.DisplayNameWords = SplitWords(indexedFile.NormalizedDisplayName);
        indexedFile.PathSegments = SplitPathSegments(fullPath);
        indexedFile.NormalizedPathSegments = indexedFile.PathSegments
            .Select(Normalize)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();
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
        int ReusedDirectoryCount,
        int SkippedDirectoryCount)
    {
        public static FileRefreshMetrics Empty { get; } = new(0, 0, 0, 0);
    }
}
