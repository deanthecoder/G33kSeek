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
    private readonly FileSearchService m_fileSearchService;
    private readonly UnitConversionService m_unitConversionService;

    public DefaultQueryProvider() : this(new ApplicationSearchService(), new FileSearchService(), new UnitConversionService()) { }

    internal DefaultQueryProvider(
        ApplicationSearchService applicationSearchService,
        FileSearchService fileSearchService,
        UnitConversionService unitConversionService = null)
    {
        m_applicationSearchService = applicationSearchService ?? throw new ArgumentNullException(nameof(applicationSearchService));
        m_fileSearchService = fileSearchService ?? throw new ArgumentNullException(nameof(fileSearchService));
        m_unitConversionService = unitConversionService ?? new UnitConversionService();
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

        if (TryCreateExplicitUrlResponse(query, out var urlResponse))
            return Task.FromResult(urlResponse);

        if (TryCreateDateTimeResponse(query, out var dateTimeResponse))
            return Task.FromResult(dateTimeResponse);

        if (TryCreateUnitConversionResponse(query, out var unitConversionResponse))
            return Task.FromResult(unitConversionResponse);

        return QueryDefaultSearchAsync(query, cancellationToken);
    }

    private static bool TryCreateExplicitUrlResponse(string query, out QueryResponse response) =>
        TryCreateUrlResponse(query, allowBareDomains: false, out response);

    private static bool TryCreateImplicitUrlResponse(string query, out QueryResponse response) =>
        TryCreateUrlResponse(query, allowBareDomains: true, out response);

    private static bool TryCreateUrlResponse(string query, bool allowBareDomains, out QueryResponse response)
    {
        var trimmedQuery = query.Trim();
        var candidate = BuildUrlCandidate(trimmedQuery, allowBareDomains);

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

    private static string BuildUrlCandidate(string query, bool allowBareDomains)
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

        if (!allowBareDomains)
            return null;

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

    private bool TryCreateUnitConversionResponse(string query, out QueryResponse response)
    {
        if (!m_unitConversionService.TryConvert(query, out var convertedValue, out var description))
        {
            response = null;
            return false;
        }

        response = new QueryResponse(
            [
                new QueryResult(
                    convertedValue,
                    description,
                    "Value",
                    new QueryActionDescriptor(
                        QueryActionKind.CopyText,
                        convertedValue,
                        successMessage: "Conversion copied."))
            ],
            "Conversion ready. Press Enter to copy it.");
        return true;
    }

    private async Task<QueryResponse> QueryDefaultSearchAsync(string query, CancellationToken cancellationToken)
    {
        var applicationTask = m_applicationSearchService.SearchAsync(query, cancellationToken);
        var fileTask = m_fileSearchService.SearchAsync(query, cancellationToken);
        await Task.WhenAll(applicationTask, fileTask);

        var applications = applicationTask.Result;
        var fileResult = fileTask.Result;
        if (applications.Count == 0 && fileResult.TotalMatchCount == 0)
        {
            if (TryCreateImplicitUrlResponse(query, out var urlResponse))
                return urlResponse;

            return new QueryResponse([], $"No apps or files matched \"{query}\".");
        }

        var results = applications
            .Select(CreateApplicationResult)
            .Concat(fileResult.VisibleFiles.Select(CreateFileResult))
            .ToArray();

        return new QueryResponse(
            results,
            BuildSearchStatusText(applications.Count, fileResult.VisibleFiles.Count, fileResult.TotalMatchCount));
    }

    private static QueryResult CreateApplicationResult(IndexedApplication app)
    {
        return new QueryResult(app.DisplayName, app.Subtitle, "App", app.CreatePrimaryAction());
    }

    private static QueryResult CreateFileResult(IndexedFile file)
    {
        return new QueryResult(file.DisplayName, file.Subtitle, file.IsDirectory ? "Folder" : "File", file.CreatePrimaryAction());
    }

    private static string BuildSearchStatusText(int applicationCount, int visibleFileCount, int totalFileCount)
    {
        if (applicationCount > 0 && totalFileCount > 0)
            return totalFileCount == visibleFileCount
                ? $"Found {applicationCount} app{(applicationCount == 1 ? string.Empty : "s")} and {totalFileCount} item{(totalFileCount == 1 ? string.Empty : "s")}."
                : $"Found {applicationCount} app{(applicationCount == 1 ? string.Empty : "s")} and showing {visibleFileCount} of {totalFileCount} items.";

        if (applicationCount > 0)
            return $"Found {applicationCount} app{(applicationCount == 1 ? string.Empty : "s")}.";

        return totalFileCount == visibleFileCount
            ? $"Found {totalFileCount} item{(totalFileCount == 1 ? string.Empty : "s")}."
            : $"Showing {visibleFileCount} of {totalFileCount} items.";
    }
}
