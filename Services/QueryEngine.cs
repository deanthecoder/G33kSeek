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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using G33kSeek.Models;
using QueryProvider = G33kSeek.Providers.IQueryProvider;

namespace G33kSeek.Services;

/// <summary>
/// Routes raw launcher input to the correct query provider.
/// </summary>
/// <remarks>
/// Prefix-based modes and default no-prefix search both flow through this engine so the UI can stay provider-agnostic.
/// </remarks>
public sealed class QueryEngine
{
    private readonly IReadOnlyList<QueryProvider> m_prefixedProviders;
    private readonly QueryProvider m_defaultProvider;

    public QueryEngine(IEnumerable<QueryProvider> providers)
    {
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));

        var providerList = providers.ToList();
        m_defaultProvider = providerList.SingleOrDefault(provider => string.IsNullOrEmpty(provider.Prefix));
        m_prefixedProviders = providerList
            .Where(provider => !string.IsNullOrEmpty(provider.Prefix))
            .OrderByDescending(provider => provider.Prefix.Length)
            .ToArray();
    }

    public Task<QueryResponse> QueryAsync(string rawQuery, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = rawQuery ?? string.Empty;
        var provider = ResolveProvider(normalizedQuery, out var providerQuery, out var prefix);
        if (provider == null)
            return Task.FromResult(new QueryResponse([], "No query provider is configured for that input."));

        return provider.QueryAsync(new QueryRequest(normalizedQuery, providerQuery, prefix), cancellationToken);
    }

    private QueryProvider ResolveProvider(string rawQuery, out string providerQuery, out string prefix)
    {
        foreach (var provider in m_prefixedProviders)
        {
            if (!rawQuery.StartsWith(provider.Prefix, StringComparison.Ordinal))
                continue;

            prefix = provider.Prefix;
            providerQuery = rawQuery[prefix.Length..].Trim();
            return provider;
        }

        prefix = string.Empty;
        providerQuery = rawQuery.Trim();
        return m_defaultProvider;
    }
}
