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
/// Defines a provider which can interpret a routed launcher query.
/// </summary>
/// <remarks>
/// Providers may power prefix-based modes such as calculator or unprefixed modes such as app and file search.
/// </remarks>
public interface IQueryProvider
{
    string Prefix { get; }

    QueryProviderHelpEntry HelpEntry { get; }

    Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken);
}
