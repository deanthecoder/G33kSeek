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

        return QueryApplicationsAsync(query, cancellationToken);
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
                            app.BundleDirectory.FullName,
                            "App",
                            new QueryActionDescriptor(
                                QueryActionKind.OpenPath,
                                app.BundleDirectory.FullName,
                                $"Launching {app.DisplayName}.")))
                .ToArray(),
            $"Found {applications.Count} application{(applications.Count == 1 ? string.Empty : "s")}.");
    }
}
