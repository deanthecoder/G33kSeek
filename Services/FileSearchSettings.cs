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

    public List<IndexedDirectorySnapshot> DirectorySnapshots
    {
        get
        {
            if (DirectorySnapshotsData.Length == 0)
                return [];

            return JsonConvert.DeserializeObject<List<IndexedDirectorySnapshot>>(DirectorySnapshotsData.DecompressToString(), SerializerSettings) ?? [];
        }
        set
        {
            var serialized = JsonConvert.SerializeObject(value ?? [], Formatting.None, SerializerSettings);
            DirectorySnapshotsData = serialized.Compress();
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
        var serializeStopwatch = Stopwatch.StartNew();
        var serializedCache = JsonConvert.SerializeObject(directorySnapshots ?? [], Formatting.None, SerializerSettings);
        serializeStopwatch.Stop();

        var compressStopwatch = Stopwatch.StartNew();
        var compressedCache = serializedCache.Compress();
        compressStopwatch.Stop();

        SearchRoots = hasExplicitSearchRoots ? (searchRoots?.ToList() ?? []) : [];
        DirectorySnapshotsData = compressedCache;
        LastFileRefreshUtc = lastRefreshUtc;
        CacheFormatVersion = cacheFormatVersion;

        Save();
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
}
