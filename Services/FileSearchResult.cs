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
using G33kSeek.Models;

namespace G33kSeek.Services;

/// <summary>
/// Represents visible and total file matches for a launcher query.
/// </summary>
/// <remarks>
/// This lets the UI show a capped result list while still reporting how many file matches were actually found.
/// </remarks>
internal sealed class FileSearchResult
{
    public FileSearchResult(IReadOnlyList<IndexedFile> visibleFiles, int totalMatchCount, IReadOnlyList<IndexedFile> matchingFiles = null)
    {
        if (totalMatchCount < 0)
            throw new ArgumentOutOfRangeException(nameof(totalMatchCount));

        VisibleFiles = visibleFiles ?? [];
        TotalMatchCount = totalMatchCount;
        MatchingFiles = matchingFiles ?? VisibleFiles;
    }

    public IReadOnlyList<IndexedFile> VisibleFiles { get; }

    public int TotalMatchCount { get; }

    public IReadOnlyList<IndexedFile> MatchingFiles { get; }
}
