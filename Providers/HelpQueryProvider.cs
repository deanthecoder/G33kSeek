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

namespace G33kSeek.Providers;

/// <summary>
/// Provides in-app help for the launcher query system.
/// </summary>
/// <remarks>
/// This gives users a quick way to discover modes and examples without leaving the launcher.
/// </remarks>
public sealed class HelpQueryProvider : IQueryProvider
{
    private readonly Func<IReadOnlyList<QueryProviderHelpEntry>> m_helpEntriesAccessor;

    public HelpQueryProvider(Func<IReadOnlyList<QueryProviderHelpEntry>> helpEntriesAccessor)
    {
        m_helpEntriesAccessor = helpEntriesAccessor ?? throw new ArgumentNullException(nameof(helpEntriesAccessor));
    }

    public string Prefix => "?";

    public QueryProviderHelpEntry HelpEntry =>
        new("Help and examples", "Use ? for help, or ?\"search text\" to search the web.", "?");

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();
        var helpTopics = GetHelpTopics();

        var query = request.ProviderQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(
                new QueryResponse(
                    helpTopics,
                    "Help: try an app name, =2+2, > for commands, keep typing after ? to filter help, or use ?\"search text\" for web search."));
        }

        if (TryCreateWebSearchResponse(query, out var webSearchResponse))
            return Task.FromResult(webSearchResponse);

        var filteredTopics = helpTopics
            .Where(result => Matches(result, query))
            .ToArray();

        if (filteredTopics.Length == 0)
        {
            return Task.FromResult(
                new QueryResponse(
                [
                    new QueryResult(
                        "No help topics matched.",
                        $"Nothing matched \"{query}\". Try app, calc, command, AI, content, or URL.",
                        "?")
                ],
                    "Help: no topics matched. Try a broader search term."));
        }

        return Task.FromResult(
                new QueryResponse(
                    filteredTopics,
                $"Help: showing {filteredTopics.Length} topic{(filteredTopics.Length == 1 ? string.Empty : "s")} for \"{query}\"."));
    }

    private static bool TryCreateWebSearchResponse(string query, out QueryResponse response)
    {
        if (query.Length < 2 || query[0] != '"' || query[^1] != '"')
        {
            response = null;
            return false;
        }

        var searchText = query[1..^1].Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            response = null;
            return false;
        }

        var searchUri = $"https://www.google.com/search?q={Uri.EscapeDataString(searchText)}";
        response = new QueryResponse(
            [
                new QueryResult(
                    searchText,
                    "Search the web with Google",
                    "Web",
                    new QueryActionDescriptor(
                        QueryActionKind.OpenUri,
                        searchUri,
                        successMessage: "Opening Google search."))
            ],
            "Web search ready. Press Enter to open it.");
        return true;
    }

    private IReadOnlyList<QueryResult> GetHelpTopics()
    {
        var helpEntries = m_helpEntriesAccessor.Invoke() ?? [];
        return helpEntries
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .Select(entry => new QueryResult(entry.Title, entry.Description, entry.Example))
            .ToArray();
    }

    private static bool Matches(QueryResult result, string query)
    {
        return result.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               result.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               result.TrailingText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
