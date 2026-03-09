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
using G33kSeek.Services;

namespace G33kSeek.Tests;

public class ApplicationSearchServiceTests
{
    [Test]
    public async Task SearchAsyncDiscoversMacApplicationsFromTopLevelRoots()
    {
        using var tempDirectory = new TempDirectory();
        var rootDirectory = tempDirectory.GetDir("Applications");
        rootDirectory.Create();
        rootDirectory.CreateSubdirectory("Safari.app");
        rootDirectory.CreateSubdirectory("Utilities");

        var service = new ApplicationSearchService([rootDirectory], [], [], isMacOS: true, isWindows: false);

        var results = await service.SearchAsync("saf", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Safari"));
        Assert.That(results[0].BundleDirectory?.FullName, Is.EqualTo(rootDirectory.GetDir("Safari.app").FullName));
    }

    [Test]
    public async Task SearchAsyncDiscoversMacApplicationsNestedOneLevelDeep()
    {
        using var tempDirectory = new TempDirectory();
        var rootDirectory = tempDirectory.GetDir("SystemApplications");
        rootDirectory.Create();
        rootDirectory.CreateSubdirectory("Calculator.app");
        var utilitiesDirectory = rootDirectory.CreateSubdirectory("Utilities");
        utilitiesDirectory.CreateSubdirectory("Terminal.app");

        var service = new ApplicationSearchService([rootDirectory], [], [], isMacOS: true, isWindows: false);

        var results = await service.SearchAsync("term", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Terminal"));
        Assert.That(results[0].BundleDirectory?.FullName, Is.EqualTo(utilitiesDirectory.GetDir("Terminal.app").FullName));
    }

    [Test]
    public async Task SearchAsyncPrefersStartsWithMatchesOverContainsMatches()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Code Runner",
                    SearchName = "code runner",
                    BundleDirectory = new DirectoryInfo("/Applications/Code Runner.app")
                },
                new IndexedApplication
                {
                    DisplayName = "Xcode",
                    SearchName = "xcode",
                    BundleDirectory = new DirectoryInfo("/Applications/Xcode.app")
                }
            ],
            isMacOS: true,
            isWindows: false,
            DateTime.UtcNow);

        var results = await service.SearchAsync("code", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].DisplayName, Is.EqualTo("Code Runner"));
    }

    [Test]
    public async Task SearchAsyncDiscoversWindowsApplicationsFromStartMenuShortcuts()
    {
        using var tempDirectory = new TempDirectory();
        var programsRoot = tempDirectory.GetDir("Programs");
        programsRoot.Create();
        var developmentDirectory = programsRoot.CreateSubdirectory("Development");
        var jetBrainsDirectory = developmentDirectory.CreateSubdirectory("JetBrains");
        var shortcutFile = jetBrainsDirectory.GetFile("Rider.lnk");
        shortcutFile.WriteAllText(string.Empty);

        var service = new ApplicationSearchService(
            [],
            [programsRoot],
            [],
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: null,
            windowsStartAppsAccessor: () => []);

        var results = await service.SearchAsync("rid", CancellationToken.None);

        Assert.That(results.Any(result => result.DisplayName == "Rider"), Is.True);
        var riderResult = results.Single(result => result.DisplayName == "Rider");
        Assert.That(riderResult.ShortcutFile?.FullName, Is.EqualTo(shortcutFile.FullName));
    }

    [Test]
    public async Task SearchAsyncDiscoversWindowsStoreApplicationsFromStartApps()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [],
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: null,
            windowsStartAppsAccessor: () => [new ApplicationSearchService.WindowsStartApp("Company Portal", "Microsoft.CompanyPortal_8wekyb3d8bbwe!App")]);

        var results = await service.SearchAsync("company portal", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Company Portal"));
        Assert.That(results[0].AppUserModelId, Is.EqualTo("Microsoft.CompanyPortal_8wekyb3d8bbwe!App"));
    }

    [Test]
    public async Task SearchAsyncReturnsEmptyWhenPlatformIsUnsupported()
    {
        var service = new ApplicationSearchService([], [], [], isMacOS: false, isWindows: false);

        var results = await service.SearchAsync("safari", CancellationToken.None);

        Assert.That(results, Is.Empty);
    }
}
