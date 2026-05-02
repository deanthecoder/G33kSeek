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
using DTC.Core.Extensions;
using DTC.Core.JsonConverters;
using DTC.Core.Settings;
using G33kSeek.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace G33kSeek.Services;

/// <summary>
/// Persists file search roots and the cached file index for the launcher.
/// </summary>
/// <remarks>
/// This stores the no-prefix file cache in compressed form so large Documents trees do not bloat settings unnecessarily.
/// </remarks>
internal sealed class FileSearchSettings : UserSettingsBase
{
    private static readonly JsonSerializerSettings SerializerSettings = CreateSerializerSettings();

    protected override string SettingsFileName => "file-search-settings.json";

    public List<DirectoryInfo> SearchRoots
    {
        get => Get<List<DirectoryInfo>>() ?? [];
        set => Set(value ?? []);
    }

    public byte[] DirectorySnapshotsData
    {
        get => Get<byte[]>() ?? [];
        set => Set(value ?? []);
    }

    public List<byte[]> DirectorySnapshotChunks
    {
        get => Get<List<byte[]>>() ?? [];
        set => Set(value ?? []);
    }

    public List<IndexedDirectorySnapshot> DirectorySnapshots
    {
        get => LoadDirectorySnapshots(DirectorySnapshotChunks, DirectorySnapshotsData);
        set
        {
            DirectorySnapshotChunks = CreateDirectorySnapshotChunks(value ?? []);
            DirectorySnapshotsData = [];
        }
    }

    public DateTime LastFileRefreshUtc
    {
        get => Get<DateTime>();
        set => Set(value);
    }

    public int CacheFormatVersion
    {
        get => Get<int>();
        set => Set(value);
    }

    protected override void ApplyDefaults()
    {
        SearchRoots = [];
        DirectorySnapshotsData = [];
        DirectorySnapshotChunks = [];
        LastFileRefreshUtc = DateTime.MinValue;
        CacheFormatVersion = 0;
    }

    internal void PersistCache(
        IReadOnlyList<DirectoryInfo> searchRoots,
        bool hasExplicitSearchRoots,
        IReadOnlyList<IndexedDirectorySnapshot> directorySnapshots,
        DateTime lastRefreshUtc,
        int cacheFormatVersion)
    {
        SearchRoots = hasExplicitSearchRoots ? searchRoots?.ToList() ?? [] : [];
        DirectorySnapshotChunks = CreateDirectorySnapshotChunks(directorySnapshots ?? []);
        DirectorySnapshotsData = [];
        LastFileRefreshUtc = lastRefreshUtc;
        CacheFormatVersion = cacheFormatVersion;

        Save();
    }

    internal static List<byte[]> CreateDirectorySnapshotChunks(IReadOnlyList<IndexedDirectorySnapshot> directorySnapshots)
    {
        var chunks = new List<byte[]>();
        foreach (var rootSnapshot in directorySnapshots ?? [])
        {
            if (rootSnapshot?.Directory == null)
                continue;

            chunks.Add(SerializeChunk(
                new DirectorySnapshotChunk
                {
                    RootDirectory = rootSnapshot.Directory,
                    IsRoot = true,
                    Snapshot = new IndexedDirectorySnapshot
                    {
                        Directory = rootSnapshot.Directory,
                        LastWriteTimeUtc = rootSnapshot.LastWriteTimeUtc,
                        Entries = rootSnapshot.Entries ?? [],
                        Children = []
                    }
                }));

            foreach (var childSnapshot in rootSnapshot.Children ?? [])
            {
                if (childSnapshot?.Directory == null)
                    continue;

                chunks.Add(SerializeChunk(
                    new DirectorySnapshotChunk
                    {
                        RootDirectory = rootSnapshot.Directory,
                        Snapshot = childSnapshot
                    }));
            }
        }

        return chunks;
    }

    internal static List<IndexedDirectorySnapshot> LoadDirectorySnapshots(
        IReadOnlyList<byte[]> directorySnapshotChunks,
        byte[] legacyDirectorySnapshotsData = null)
    {
        if (directorySnapshotChunks is { Count: > 0 })
            return LoadDirectorySnapshotsFromChunks(directorySnapshotChunks);

        if (legacyDirectorySnapshotsData is not { Length: > 0 })
            return [];

        return JsonConvert.DeserializeObject<List<IndexedDirectorySnapshot>>(
            legacyDirectorySnapshotsData.DecompressToString(),
            SerializerSettings) ?? [];
    }

    private static List<IndexedDirectorySnapshot> LoadDirectorySnapshotsFromChunks(IReadOnlyList<byte[]> directorySnapshotChunks)
    {
        var rootSnapshots = new List<IndexedDirectorySnapshot>();
        var rootSnapshotsByPath = new Dictionary<string, IndexedDirectorySnapshot>(StringComparer.OrdinalIgnoreCase);
        var childChunks = new List<DirectorySnapshotChunk>();

        foreach (var chunkData in directorySnapshotChunks)
        {
            var chunk = DeserializeChunk(chunkData);
            if (chunk?.Snapshot?.Directory == null)
                continue;

            if (!chunk.IsRoot)
            {
                childChunks.Add(chunk);
                continue;
            }

            var rootSnapshot = chunk.Snapshot;
            rootSnapshot.Children ??= [];
            rootSnapshot.Children.Clear();
            rootSnapshots.Add(rootSnapshot);
            rootSnapshotsByPath[rootSnapshot.Directory.FullName] = rootSnapshot;
        }

        foreach (var childChunk in childChunks)
        {
            var rootPath = childChunk.RootDirectory?.FullName;
            if (rootPath != null && rootSnapshotsByPath.TryGetValue(rootPath, out var rootSnapshot))
            {
                rootSnapshot.Children.Add(childChunk.Snapshot);
                continue;
            }

            rootSnapshots.Add(childChunk.Snapshot);
        }

        return rootSnapshots;
    }

    private static byte[] SerializeChunk(DirectorySnapshotChunk chunk) =>
        JsonConvert.SerializeObject(chunk, Formatting.None, SerializerSettings).Compress();

    private static DirectorySnapshotChunk DeserializeChunk(byte[] chunkData)
    {
        if (chunkData == null || chunkData.Length == 0)
            return null;

        return JsonConvert.DeserializeObject<DirectorySnapshotChunk>(
            chunkData.DecompressToString(),
            SerializerSettings);
    }

    private static JsonSerializerSettings CreateSerializerSettings() =>
        new()
        {
            Converters =
            [
                new FileInfoConverter(),
                new DirectoryInfoConverter(),
                new StringEnumConverter()
            ]
        };

    private sealed class DirectorySnapshotChunk
    {
        public DirectoryInfo RootDirectory { get; set; }

        public bool IsRoot { get; set; }

        public IndexedDirectorySnapshot Snapshot { get; set; }
    }
}
