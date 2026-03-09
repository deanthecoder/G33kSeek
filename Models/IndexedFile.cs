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
using System.IO;

namespace G33kSeek.Models;

/// <summary>
/// Represents a cached file-system entry that can be searched and opened.
/// </summary>
/// <remarks>
/// Used by file search so no-prefix queries can stay fast without hitting the disk on every keystroke.
/// </remarks>
internal sealed class IndexedFile
{
    public string DisplayName { get; set; } = string.Empty;

    public string SearchText { get; set; } = string.Empty;

    public string NormalizedDisplayName { get; set; } = string.Empty;

    public string[] DisplayNameWords { get; set; } = [];

    public FileInfo File { get; set; }

    public DirectoryInfo Directory { get; set; }

    public bool IsDirectory => Directory != null;

    public string Subtitle => IsDirectory ? Directory?.Parent?.FullName ?? string.Empty : File?.DirectoryName ?? string.Empty;

    public string FullPath => IsDirectory ? Directory?.FullName ?? string.Empty : File?.FullName ?? string.Empty;

    public string[] PathSegments { get; set; } = [];

    public string[] NormalizedPathSegments { get; set; } = [];

    public QueryActionDescriptor CreatePrimaryAction()
    {
        if (IsDirectory)
        {
            if (Directory?.Exists != true)
                throw new InvalidOperationException($"Indexed directory '{DisplayName}' does not exist.");

            return new QueryActionDescriptor(
                QueryActionKind.OpenPath,
                Directory.FullName,
                successMessage: $"Opening {DisplayName}.");
        }

        if (File?.Exists != true)
            throw new InvalidOperationException($"Indexed file '{DisplayName}' does not exist.");

        return new QueryActionDescriptor(
            QueryActionKind.OpenPath,
            File.FullName,
            successMessage: $"Opening {DisplayName}.");
    }
}
