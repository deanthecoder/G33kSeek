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
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using G33kSeek.Models;
using G33kSeek.Services;

namespace G33kSeek.Providers;

/// <summary>
/// Provides the default unprefixed launcher experience.
/// </summary>
/// <remarks>
/// This keeps the no-prefix path wired through the same engine even before app and file search are implemented.
/// </remarks>
public sealed class DefaultQueryProvider : IQueryProvider
{
    private readonly ApplicationSearchService m_applicationSearchService;

    public DefaultQueryProvider() : this(new ApplicationSearchService()) { }

    internal DefaultQueryProvider(ApplicationSearchService applicationSearchService)
    {
        m_applicationSearchService = applicationSearchService ?? throw new ArgumentNullException(nameof(applicationSearchService));
    }

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

        if (TryCreateUrlResponse(query, out var urlResponse))
            return Task.FromResult(urlResponse);

        if (TryCreateDateTimeResponse(query, out var dateTimeResponse))
            return Task.FromResult(dateTimeResponse);

        return QueryApplicationsAsync(query, cancellationToken);
    }

    private static bool TryCreateUrlResponse(string query, out QueryResponse response)
    {
        var trimmedQuery = query.Trim();
        var candidate = BuildUrlCandidate(trimmedQuery);

        if (candidate == null)
        {
            response = null;
            return false;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            response = null;
            return false;
        }

        response = new QueryResponse(
            [
                new QueryResult(
                    uri.AbsoluteUri,
                    "Open URL in browser",
                    "URL",
                    new QueryActionDescriptor(
                        QueryActionKind.OpenUri,
                        uri.AbsoluteUri,
                        successMessage: $"Opening {uri.Host}."))
            ],
            "URL ready. Press Enter to open it.");
        return true;
    }

    private static string BuildUrlCandidate(string query)
    {
        if (string.IsNullOrWhiteSpace(query) ||
            query.Contains(' ') ||
            query.Contains('\\'))
        {
            return null;
        }

        if (query.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            query.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        if (query.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            return $"https://{query}";

        var firstSlash = query.IndexOf('/');
        var firstQuestionMark = query.IndexOf('?');
        var firstHash = query.IndexOf('#');
        var hostLength = new[] { firstSlash, firstQuestionMark, firstHash }
            .Where(index => index >= 0)
            .DefaultIfEmpty(query.Length)
            .Min();
        var host = query[..hostLength];

        if (!host.Contains('.') || Uri.CheckHostName(host) != UriHostNameType.Dns)
            return null;

        return $"https://{query}";
    }

    private static bool TryCreateDateTimeResponse(string query, out QueryResponse response)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var now = DateTimeOffset.Now;
        string value;
        string label;
        string statusText;

        switch (normalizedQuery)
        {
            case "now":
                value = now.ToString("O", CultureInfo.InvariantCulture);
                label = "Current date and time";
                statusText = "Current timestamp ready. Press Enter to copy it.";
                break;

            case "date":
                value = FormatLongDate(now);
                label = "Current date";
                statusText = "Current date ready. Press Enter to copy it.";
                break;

            case "time":
                value = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                label = "Current time";
                statusText = "Current time ready. Press Enter to copy it.";
                break;

            default:
                response = null;
                return false;
        }

        response = new QueryResponse(
            [
                new QueryResult(
                    value,
                    label,
                    "Value",
                    new QueryActionDescriptor(
                        QueryActionKind.CopyText,
                        value,
                        successMessage: $"{label} copied."))
            ],
            statusText);
        return true;
    }

    private static string FormatLongDate(DateTimeOffset value)
    {
        var day = value.Day;
        var suffix = day switch
        {
            >= 11 and <= 13 => "th",
            _ when day % 10 == 1 => "st",
            _ when day % 10 == 2 => "nd",
            _ when day % 10 == 3 => "rd",
            _ => "th"
        };

        return $"{value:dddd} {day}{suffix} {value:MMMM}, {value:yyyy}";
    }

    private async Task<QueryResponse> QueryApplicationsAsync(string query, CancellationToken cancellationToken)
    {
        var applications = await m_applicationSearchService.SearchAsync(query, cancellationToken);
        if (applications.Count == 0)
            return new QueryResponse([], $"No applications matched \"{query}\".");

        return new QueryResponse(
            applications
                .Select(
                    app =>
                        new QueryResult(
                            app.DisplayName,
                            app.LaunchPath,
                            "App",
                            new QueryActionDescriptor(
                                QueryActionKind.OpenPath,
                                app.LaunchPath,
                                successMessage: $"Launching {app.DisplayName}.")))
                .ToArray(),
            $"Found {applications.Count} application{(applications.Count == 1 ? string.Empty : "s")}.");
    }
}
