// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;
using DTC.Core.Extensions;
using G33kSeek.Models;
using G33kSeek.Providers;
using G33kSeek.Services;

namespace G33kSeek.Tests;

public class DefaultQueryProviderTests
{
    [Test]
    public async Task QueryAsyncReturnsModeSummaryForBlankQuery()
    {
        var m_provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());
        var response = await m_provider.QueryAsync(new QueryRequest(string.Empty, string.Empty, string.Empty), CancellationToken.None);

        Assert.That(response.Results, Is.Empty);
        Assert.That(response.StatusText, Is.EqualTo("Type an app or file name, or use =2+2, ? for help, > for commands."));
    }

    [Test]
    public async Task QueryAsyncReturnsMatchingApplications()
    {
        using var tempDirectory = new TempDirectory();
        var applicationRoot = tempDirectory.GetDir("Applications");
        applicationRoot.Create();
        var safariBundle = applicationRoot.CreateSubdirectory("Safari.app");
        var applicationSearchService = new ApplicationSearchService(
            [applicationRoot],
            [
                new IndexedApplication
                {
                    DisplayName = "Safari",
                    SearchName = "safari",
                    BundleDirectory = safariBundle
                }
            ],
            isMacOS: true,
            DateTime.UtcNow);
        var m_provider = new DefaultQueryProvider(applicationSearchService);

        var response = await m_provider.QueryAsync(new QueryRequest("saf", "saf", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Safari"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
    }

    [Test]
    public async Task QueryAsyncReturnsNoMatchesStatusWhenNothingMatches()
    {
        var m_provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());
        var response = await m_provider.QueryAsync(new QueryRequest("nomatch", "nomatch", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Is.Empty);
        Assert.That(response.StatusText, Is.EqualTo("No applications matched \"nomatch\"."));
    }

    [Test]
    public void HelpEntryDescribesDefaultSearch()
    {
        var m_provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());
        Assert.That(m_provider.HelpEntry.Title, Is.EqualTo("App and file search"));
        Assert.That(m_provider.HelpEntry.Example, Is.EqualTo("rider"));
    }

    private static ApplicationSearchService CreateEmptyApplicationSearchService()
    {
        return new ApplicationSearchService([], [], isMacOS: true);
    }
}
