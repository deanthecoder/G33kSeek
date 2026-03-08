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
using System.Threading;
using System.Threading.Tasks;
using G33kSeek.Models;

namespace G33kSeek.Providers;

/// <summary>
/// Provides the default unprefixed launcher experience.
/// </summary>
/// <remarks>
/// This keeps the no-prefix path wired through the same engine even before app and file search are implemented.
/// </remarks>
public sealed class DefaultQueryProvider : IQueryProvider
{
    public string Prefix => string.Empty;

    public QueryProviderHelpEntry HelpEntry =>
        new("App and file search", "Start typing with no prefix to find apps and files.", "rider");

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();

        var query = request.ProviderQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(
                new QueryResponse(
                    [],
                    "Type an app or file name, or use =2+2, ? for help, > for commands."));
        }

        return Task.FromResult(
            new QueryResponse(
                [
                    new QueryResult(
                        "Application and file search is next.",
                        $"No-prefix query received: {query}",
                        "Soon")
                ],
                "The engine is live. Default search provider implementation comes next."));
    }
}
