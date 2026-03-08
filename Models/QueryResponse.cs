// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.Generic;

namespace G33kSeek.Models;

/// <summary>
/// Contains the rows and status text produced by a query provider.
/// </summary>
/// <remarks>
/// Providers can return a single computed value or a filtered list of actionable results through the same response shape.
/// </remarks>
public sealed class QueryResponse
{
    public QueryResponse(
        IReadOnlyList<QueryResult> results,
        string statusText = null)
    {
        Results = results ?? [];
        StatusText = statusText ?? string.Empty;
    }

    public IReadOnlyList<QueryResult> Results { get; }

    public string StatusText { get; }
}
