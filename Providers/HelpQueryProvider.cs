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
    private static readonly IReadOnlyList<QueryResult> HelpTopics =
    [
        new(
            "App and file search",
            "Start typing with no prefix to find apps and files.",
            "rider"),
        new(
            "Calculator",
            "Use = to evaluate maths expressions. Enter copies the result.",
            "=sin(pi/2)"),
        new(
            "Commands",
            "Use > for launcher commands as they are added.",
            ">"),
        new(
            "AI prompts",
            "Use @ for AI-focused queries once that provider lands.",
            "@summarise this text"),
        new(
            "Content search",
            "Use ?? to search inside files when grep support is added.",
            "??TODO"),
        new(
            "Direct URLs",
            "Planned: typing http://, https://, or www. should open a URL directly.",
            "https://avaloniaui.net")
    ];

    public string Prefix => "?";

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
                    HelpTopics,
                    "Help: try an app name, =2+2, > for commands, or keep typing after ? to filter help."));
        }

        var filteredTopics = HelpTopics
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

    private static bool Matches(QueryResult result, string query)
    {
        return result.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               result.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               result.TrailingText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
