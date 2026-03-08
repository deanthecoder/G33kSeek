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
/// Describes how a query provider should appear in launcher help.
/// </summary>
/// <remarks>
/// Keeping help metadata on providers allows the in-app help list to stay current as new modes are added.
/// </remarks>
public sealed class QueryProviderHelpEntry
{
    public QueryProviderHelpEntry(string title, string description, string example)
    {
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
        Example = example ?? string.Empty;
    }

    public string Title { get; }

    public string Description { get; }

    public string Example { get; }
}
