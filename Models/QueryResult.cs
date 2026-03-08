// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kSeek.Models;

/// <summary>
/// Represents a single row displayed in the launcher results list.
/// </summary>
/// <remarks>
/// The same row model supports informational output such as calculations and navigable result lists such as file search.
/// </remarks>
public sealed class QueryResult
{
    public QueryResult(
        string title,
        string subtitle = null,
        string trailingText = null,
        QueryActionDescriptor primaryAction = null)
    {
        Title = title ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        TrailingText = trailingText ?? string.Empty;
        PrimaryAction = primaryAction;
    }

    public string Title { get; }

    public string Subtitle { get; }

    public string TrailingText { get; }

    public QueryActionDescriptor PrimaryAction { get; }
}
