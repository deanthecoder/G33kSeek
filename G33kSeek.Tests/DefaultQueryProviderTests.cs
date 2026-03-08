// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Globalization;
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
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());
        var response = await provider.QueryAsync(new QueryRequest(string.Empty, string.Empty, string.Empty), CancellationToken.None);

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
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Safari",
                    SearchName = "safari",
                    BundleDirectory = safariBundle
                }
            ],
            isMacOS: true,
            isWindows: false,
            DateTime.UtcNow);
        var provider = new DefaultQueryProvider(applicationSearchService);

        var response = await provider.QueryAsync(new QueryRequest("saf", "saf", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Safari"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
    }

    [Test]
    public async Task QueryAsyncReturnsOpenUrlResultForHttpsAddress()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());

        var response = await provider.QueryAsync(new QueryRequest("https://avaloniaui.net", "https://avaloniaui.net", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("Open URL in browser"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://avaloniaui.net/"));
    }

    [Test]
    public async Task QueryAsyncNormalizesWwwAddressToHttps()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());

        var response = await provider.QueryAsync(new QueryRequest("www.openai.com", "www.openai.com", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://www.openai.com/"));
    }

    [Test]
    public async Task QueryAsyncNormalizesBareDotComAddressToHttps()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());

        var response = await provider.QueryAsync(new QueryRequest("openai.com", "openai.com", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://openai.com/"));
    }

    [Test]
    public async Task QueryAsyncNormalizesBareCoUkAddressWithPathToHttps()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());

        var response = await provider.QueryAsync(new QueryRequest("bbc.co.uk/news", "bbc.co.uk/news", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://bbc.co.uk/news"));
    }

    [Test]
    public async Task QueryAsyncReturnsIsoTimestampForNow()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());

        var response = await provider.QueryAsync(new QueryRequest("now", "now", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("Current date and time"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
        Assert.That(DateTimeOffset.TryParseExact(
            response.Results[0].Title,
            "O",
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out _), Is.True);
    }

    [Test]
    public async Task QueryAsyncReturnsDateForDate()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());

        var response = await provider.QueryAsync(new QueryRequest("date", "date", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("Current date"));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Does.Match(@"^[A-Z][a-z]+ \d{1,2}(st|nd|rd|th) [A-Z][a-z]+, \d{4}$"));
    }

    [Test]
    public async Task QueryAsyncReturnsTimeForTime()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());

        var response = await provider.QueryAsync(new QueryRequest("time", "time", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("Current time"));
        Assert.That(DateTime.TryParseExact(
            response.Results[0].PrimaryAction?.Payload,
            "HH:mm:ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _), Is.True);
    }

    [Test]
    public async Task QueryAsyncReturnsNoMatchesStatusWhenNothingMatches()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());
        var response = await provider.QueryAsync(new QueryRequest("nomatch", "nomatch", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Is.Empty);
        Assert.That(response.StatusText, Is.EqualTo("No applications matched \"nomatch\"."));
    }

    [Test]
    public void HelpEntryDescribesDefaultSearch()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService());
        Assert.That(provider.HelpEntry.Title, Is.EqualTo("App and file search"));
        Assert.That(provider.HelpEntry.Example, Is.EqualTo("rider"));
    }

    private static ApplicationSearchService CreateEmptyApplicationSearchService()
    {
        return new ApplicationSearchService([], [], [], isMacOS: true, isWindows: false);
    }
}
