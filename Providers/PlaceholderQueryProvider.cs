// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Threading;
using System.Threading.Tasks;
using G33kSeek.Models;

namespace G33kSeek.Providers;

/// <summary>
/// Returns a lightweight placeholder row for modes that are routed but not yet implemented.
/// </summary>
/// <remarks>
/// This keeps prefix routing visible in the UI while the provider backlog is still being built out.
/// </remarks>
public sealed class PlaceholderQueryProvider : IQueryProvider
{
    private readonly string m_title;
    private readonly string m_description;

    public PlaceholderQueryProvider(string prefix, string title, string description)
    {
        Prefix = prefix ?? string.Empty;
        m_title = title ?? string.Empty;
        m_description = description ?? string.Empty;
    }

    public string Prefix { get; }

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = request?.ProviderQuery?.Trim() ?? string.Empty;
        var subtitle = string.IsNullOrWhiteSpace(query)
            ? m_description
            : $"{m_description} Query: {query}";

        return Task.FromResult(
            new QueryResponse(
                [
                    new QueryResult(m_title, subtitle, Prefix)
                ],
                $"{m_title} will be implemented in a later slice."));
    }
}
