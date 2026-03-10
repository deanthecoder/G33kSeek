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
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());
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
        var provider = new DefaultQueryProvider(applicationSearchService, CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("saf", "saf", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Safari"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
    }

    [Test]
    public async Task QueryAsyncReturnsWindowsStoreApplicationLaunchAction()
    {
        var applicationSearchService = new ApplicationSearchService(
            [],
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Company Portal",
                    SearchName = "company portal",
                    AppUserModelId = "Microsoft.CompanyPortal_8wekyb3d8bbwe!App"
                }
            ],
            isMacOS: false,
            isWindows: true,
            DateTime.UtcNow);
        var provider = new DefaultQueryProvider(applicationSearchService, CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("company portal", "company portal", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Company Portal"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.RunProcess));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("explorer.exe"));
        Assert.That(response.Results[0].PrimaryAction?.Arguments, Is.EqualTo("\"shell:AppsFolder\\Microsoft.CompanyPortal_8wekyb3d8bbwe!App\""));
    }

    [Test]
    public async Task QueryAsyncReturnsMatchingFiles()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var invoiceFile = documentsDirectory.GetFile("invoice.pdf");
        invoiceFile.WriteAllText("test");
        var provider = new DefaultQueryProvider(
            CreateEmptyApplicationSearchService(),
            new FileSearchService(
                [documentsDirectory],
                [
                    new IndexedFile
                    {
                        DisplayName = "invoice.pdf",
                        SearchText = "invoice pdf documents",
                        File = invoiceFile
                    }
                ],
                DateTime.UtcNow));

        var response = await provider.QueryAsync(new QueryRequest("invoice", "invoice", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("invoice.pdf"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
        Assert.That(response.StatusText, Is.EqualTo("Found 1 item."));
    }

    [Test]
    public async Task QueryAsyncPrefersMatchingFileOverBareDomainUrl()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var invoiceFile = documentsDirectory.GetFile("invoice.pdf");
        invoiceFile.WriteAllText("test");
        var provider = new DefaultQueryProvider(
            CreateEmptyApplicationSearchService(),
            new FileSearchService(
                [documentsDirectory],
                [
                    new IndexedFile
                    {
                        DisplayName = "invoice.pdf",
                        SearchText = "invoice pdf documents",
                        File = invoiceFile
                    }
                ],
                DateTime.UtcNow));

        var response = await provider.QueryAsync(new QueryRequest("invoice.pdf", "invoice.pdf", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("invoice.pdf"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
    }

    [Test]
    public async Task QueryAsyncReportsVisibleAndTotalFileCounts()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var indexedFiles = Enumerable.Range(1, 15)
            .Select(
                index =>
                {
                    var file = documentsDirectory.GetFile($"assemblyinfo{index}.cs");
                    file.WriteAllText("test");
                    return new IndexedFile
                    {
                        DisplayName = $"AssemblyInfo{index}.cs",
                        SearchText = $"assemblyinfo{index} cs {documentsDirectory.FullName}",
                        File = file
                    };
                })
            .ToArray();
        var provider = new DefaultQueryProvider(
            CreateEmptyApplicationSearchService(),
            new FileSearchService([documentsDirectory], indexedFiles, DateTime.UtcNow));

        var response = await provider.QueryAsync(new QueryRequest("assemblyinfo", "assemblyinfo", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(15));
        Assert.That(response.StatusText, Is.EqualTo("Found 15 items."));
    }

    [Test]
    public async Task QueryAsyncReportsVisibleAndTotalItemCountsWhenResultsAreCapped()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var indexedFiles = Enumerable.Range(1, 30)
            .Select(
                index =>
                {
                    var file = documentsDirectory.GetFile($"assemblyinfo{index}.cs");
                    file.WriteAllText("test");
                    return new IndexedFile
                    {
                        DisplayName = $"AssemblyInfo{index}.cs",
                        SearchText = $"assemblyinfo{index} cs {documentsDirectory.FullName}",
                        File = file
                    };
                })
            .ToArray();
        var provider = new DefaultQueryProvider(
            CreateEmptyApplicationSearchService(),
            new FileSearchService([documentsDirectory], indexedFiles, DateTime.UtcNow));

        var response = await provider.QueryAsync(new QueryRequest("assemblyinfo", "assemblyinfo", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(25));
        Assert.That(response.StatusText, Is.EqualTo("Showing 25 of 30 items."));
    }

    [Test]
    public async Task QueryAsyncReturnsMatchingFolders()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var invoicesDirectory = documentsDirectory.GetDir("Invoices");
        invoicesDirectory.Create();
        var provider = new DefaultQueryProvider(
            CreateEmptyApplicationSearchService(),
            new FileSearchService(
                [documentsDirectory],
                [
                    new IndexedFile
                    {
                        DisplayName = "Invoices",
                        SearchText = $"invoices {invoicesDirectory.FullName}",
                        Directory = invoicesDirectory
                    }
                ],
                DateTime.UtcNow));

        var response = await provider.QueryAsync(new QueryRequest("invoice", "invoice", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Invoices"));
        Assert.That(response.Results[0].TrailingText, Is.EqualTo("Folder"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
    }

    [Test]
    public async Task QueryAsyncReturnsOpenUrlResultForHttpsAddress()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("https://avaloniaui.net", "https://avaloniaui.net", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("Open URL in browser"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://avaloniaui.net/"));
    }

    [Test]
    public async Task QueryAsyncNormalizesWwwAddressToHttps()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("www.openai.com", "www.openai.com", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://www.openai.com/"));
    }

    [Test]
    public async Task QueryAsyncNormalizesBareDotComAddressToHttps()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("openai.com", "openai.com", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://openai.com/"));
    }

    [Test]
    public async Task QueryAsyncNormalizesBareCoUkAddressWithPathToHttps()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("bbc.co.uk/news", "bbc.co.uk/news", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("https://bbc.co.uk/news"));
    }

    [Test]
    public async Task QueryAsyncReturnsIsoTimestampForNow()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

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
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("date", "date", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("Current date"));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Does.Match(@"^[A-Z][a-z]+ \d{1,2}(st|nd|rd|th) [A-Z][a-z]+, \d{4}$"));
    }

    [Test]
    public async Task QueryAsyncReturnsTimeForTime()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

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
    public async Task QueryAsyncReturnsDataSizeConversion()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("10mb in bytes", "10mb in bytes", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("10,485,760 bytes"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsHexConversion()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("255 in hex", "255 in hex", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("0xFF"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsBinaryConversion()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("255 in binary", "255 in binary", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("0b11111111"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsTemperatureConversion()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("100c in f", "100c in f", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("212 F"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsLengthConversion()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());

        var response = await provider.QueryAsync(new QueryRequest("180cm in ft", "180cm in ft", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("5 ft 10.8661 in"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsNoMatchesStatusWhenNothingMatches()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());
        var response = await provider.QueryAsync(new QueryRequest("nomatch", "nomatch", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Is.Empty);
        Assert.That(response.StatusText, Is.EqualTo("No apps or files matched \"nomatch\"."));
    }

    [Test]
    public void HelpEntryDescribesDefaultSearch()
    {
        var provider = new DefaultQueryProvider(CreateEmptyApplicationSearchService(), CreateEmptyFileSearchService());
        Assert.That(provider.HelpEntry.Title, Is.EqualTo("App and file search"));
        Assert.That(provider.HelpEntry.Example, Is.EqualTo("rider"));
    }

    private static ApplicationSearchService CreateEmptyApplicationSearchService()
    {
        return new ApplicationSearchService([], [], [], isMacOS: true, isWindows: false);
    }

    private static FileSearchService CreateEmptyFileSearchService()
    {
        return new FileSearchService([], [], DateTime.UtcNow);
    }
}
