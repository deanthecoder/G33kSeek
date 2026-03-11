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

namespace G33kSeek.Models;

/// <summary>
/// Represents a cached directory subtree used for incremental file-index refreshes.
/// </summary>
/// <remarks>
/// Each snapshot stores one directory's immediate entries and nested child snapshots so unchanged directories can be reused without rebuilding their full subtree.
/// </remarks>
internal sealed class IndexedDirectorySnapshot
{
    public DirectoryInfo Directory { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }

    public List<IndexedFile> Entries { get; set; } = [];

    public List<IndexedDirectorySnapshot> Children { get; set; } = [];
}
