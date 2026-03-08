// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO;

namespace G33kSeek.Models;

/// <summary>
/// Represents a cached application entry that can be searched and launched.
/// </summary>
/// <remarks>
/// Used by application search so queries can be answered from a lightweight in-memory index.
/// </remarks>
internal sealed class IndexedApplication
{
    public string DisplayName { get; set; } = string.Empty;

    public string SearchName { get; set; } = string.Empty;

    public DirectoryInfo BundleDirectory { get; set; }
}
