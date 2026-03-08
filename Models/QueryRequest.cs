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
/// Represents a user query after it has been routed to a provider.
/// </summary>
/// <remarks>
/// The raw query is preserved while providers also receive the prefix-stripped query text that they should interpret.
/// </remarks>
public sealed class QueryRequest
{
    public QueryRequest(string rawQuery, string providerQuery, string prefix)
    {
        RawQuery = rawQuery ?? string.Empty;
        ProviderQuery = providerQuery ?? string.Empty;
        Prefix = prefix ?? string.Empty;
    }

    public string RawQuery { get; }

    public string ProviderQuery { get; }

    public string Prefix { get; }

    public bool HasPrefix => Prefix.Length > 0;
}
