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
    private const int CurrentCacheFormatVersion = 4;
    private const int MaxVisibleResults = 25;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
    private static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".hg",
        ".svn",
        ".idea",
        ".vs",
        "bin",
        "obj",
        "node_modules",
        "packages"
    };

    private readonly SemaphoreSlim m_refreshLock = new(1, 1);
    private readonly FileSearchSettings m_settings;
    private readonly IReadOnlyList<DirectoryInfo> m_searchRoots;
    private List<IndexedFile> m_cachedFiles;
    private DateTime m_lastRefreshUtc;

    public FileSearchService()
        : this(new FileSearchSettings(), null, null)
    {
    }

    private FileSearchService(
        FileSearchSettings settings,
        IEnumerable<DirectoryInfo> searchRoots,
        IEnumerable<IndexedFile> cachedFiles)
    {
        m_settings = settings;
        m_searchRoots = searchRoots?.ToArray() ?? GetConfiguredOrDefaultRoots(settings);
        var isCompatibleCache = settings?.CacheFormatVersion == CurrentCacheFormatVersion;
        if (settings != null && !isCompatibleCache)
        {
            Logger.Instance.Info($"Discarding file index cache format v{settings.CacheFormatVersion:N0}; expected v{CurrentCacheFormatVersion:N0}.");
        }

        m_cachedFiles = PrepareIndexedFiles(cachedFiles ?? (isCompatibleCache ? settings?.CachedFiles : null));
        m_lastRefreshUtc = isCompatibleCache ? settings?.LastFileRefreshUtc ?? DateTime.MinValue : DateTime.MinValue;
    }

    internal FileSearchService(
        IEnumerable<DirectoryInfo> searchRoots,
        IEnumerable<IndexedFile> cachedFiles,
        DateTime? lastRefreshUtc = null)
    {
        m_settings = null;
        m_searchRoots = searchRoots?.ToArray() ?? [];
        m_cachedFiles = PrepareIndexedFiles(cachedFiles);
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

    public Task WarmAsync(CancellationToken cancellationToken = default)
    {
        if (m_cachedFiles.Count > 0 && DateTime.UtcNow - m_lastRefreshUtc <= RefreshInterval)
            return Task.CompletedTask;

        return RefreshAsync(cancellationToken);
    }

    internal async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await m_refreshLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            Logger.Instance.Info($"Refreshing file index across {m_searchRoots.Count} root(s).");

            var snapshot = await Task.Run(() => DiscoverFiles(cancellationToken), cancellationToken);
            m_cachedFiles = snapshot.Files.ToList();
            m_lastRefreshUtc = DateTime.UtcNow;
            SaveIfPersistent();

            Logger.Instance.Info(
                $"File index refresh completed in {stopwatch.ElapsedMilliseconds:N0} ms. Indexed {snapshot.Files.Count:N0} items from {snapshot.ScannedDirectoryCount:N0} directories and skipped {snapshot.SkippedDirectoryCount:N0} excluded directories.");
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

    private void SaveIfPersistent()
    {
        if (m_settings == null)
            return;

        m_settings.CachedFiles = m_cachedFiles.ToList();
        m_settings.LastFileRefreshUtc = m_lastRefreshUtc;
        m_settings.CacheFormatVersion = CurrentCacheFormatVersion;
        m_settings.Save();
    }

    private static IReadOnlyList<DirectoryInfo> GetConfiguredOrDefaultRoots(FileSearchSettings settings)
    {
        var configuredRoots = settings?.SearchRoots?.Where(root => root != null).ToArray();
        return configuredRoots is { Length: > 0 } ? configuredRoots : GetDefaultSearchRoots(OperatingSystem.IsWindows());
    }

    internal static IReadOnlyList<DirectoryInfo> GetDefaultSearchRoots(
        bool isWindows = false,
        Func<Environment.SpecialFolder, string> folderPathAccessor = null)
    {
        folderPathAccessor ??= Environment.GetFolderPath;

        var roots = new[]
        {
            folderPathAccessor(Environment.SpecialFolder.MyDocuments),
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
        var discoveredFiles = new List<IndexedFile>();
        var scannedDirectoryCount = 0;
        var skippedDirectoryCount = 0;
        var pendingDirectories = new Stack<DirectoryInfo>(
            m_searchRoots
                .Where(root => root?.Exists() == true)
                .DistinctBy(root => root.FullName, StringComparer.OrdinalIgnoreCase)
                .Reverse());

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var currentDirectory = pendingDirectories.Pop();
            scannedDirectoryCount++;

            DirectoryInfo[] childDirectories;
            FileInfo[] childFiles;
            try
            {
                childDirectories = currentDirectory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).ToArray();
                childFiles = currentDirectory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                if (!ShouldIncludeDirectory(childDirectory))
                {
                    skippedDirectoryCount++;
                    continue;
                }

                discoveredFiles.Add(CreateIndexedDirectory(childDirectory));
                pendingDirectories.Push(childDirectory);
            }

            foreach (var childFile in childFiles)
            {
                if (!ShouldIncludeFile(childFile))
                    continue;

                discoveredFiles.Add(CreateIndexedFile(childFile));
            }
        }

        return new FileDiscoverySnapshot(discoveredFiles, scannedDirectoryCount, skippedDirectoryCount);
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
                .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
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

    private sealed record FileDiscoverySnapshot(
        IReadOnlyList<IndexedFile> Files,
        int ScannedDirectoryCount,
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
}
