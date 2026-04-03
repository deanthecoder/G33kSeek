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
    public async Task SearchAsyncMatchesApplicationInitialisms()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Smart Print Controller",
                    SearchName = "smart print controller",
                    BundleDirectory = new DirectoryInfo("/Applications/Smart Print Controller.app")
                }
            ],
            isMacOS: true,
            isWindows: false,
            DateTime.UtcNow);

        var results = await service.SearchAsync("spc", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Smart Print Controller"));
    }

    [Test]
    public async Task SearchAsyncMatchesInitialismPrefixes()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Smart Print Controller",
                    SearchName = "smart print controller",
                    BundleDirectory = new DirectoryInfo("/Applications/Smart Print Controller.app")
                }
            ],
            isMacOS: true,
            isWindows: false,
            DateTime.UtcNow);

        var results = await service.SearchAsync("sp", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Smart Print Controller"));
    }

    [Test]
    public async Task SearchAsyncMatchesSpaceSeparatedNameTokensInOrder()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Boot Camp Assistant",
                    SearchName = "boot camp assistant",
                    BundleDirectory = new DirectoryInfo("/Applications/Boot Camp Assistant.app")
                }
            ],
            isMacOS: true,
            isWindows: false,
            DateTime.UtcNow);

        var results = await service.SearchAsync("boot ass", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Boot Camp Assistant"));
    }

    [Test]
    public async Task SearchAsyncMatchesSpaceSeparatedNameTokensOutOfOrder()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Boot Camp Assistant",
                    SearchName = "boot camp assistant",
                    BundleDirectory = new DirectoryInfo("/Applications/Boot Camp Assistant.app")
                }
            ],
            isMacOS: true,
            isWindows: false,
            DateTime.UtcNow);

        var results = await service.SearchAsync("ass boot", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Boot Camp Assistant"));
    }

    [Test]
    public async Task SearchAsyncPrefersTextMatchesOverInitialismMatches()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [
                new IndexedApplication
                {
                    DisplayName = "Smart Print Controller",
                    SearchName = "smart print controller",
                    BundleDirectory = new DirectoryInfo("/Applications/Smart Print Controller.app")
                },
                new IndexedApplication
                {
                    DisplayName = "Project Runner Integration Network Tool",
                    SearchName = "project runner integration network tool",
                    BundleDirectory = new DirectoryInfo("/Applications/Project Runner Integration Network Tool.app")
                }
            ],
            isMacOS: true,
            isWindows: false,
            DateTime.UtcNow);

        var results = await service.SearchAsync("print", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].DisplayName, Is.EqualTo("Smart Print Controller"));
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
    public async Task SearchAsyncDiscoversWindowsSystemSettingsEntries()
    {
        var service = new ApplicationSearchService(
            [],
            [],
            [],
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: null,
            windowsStartAppsAccessor: () => []);

        var results = await service.SearchAsync("remove programs", CancellationToken.None);

        Assert.That(results.Any(result => result.DisplayName == "Add or remove programs"), Is.True);
        var addRemoveProgramsResult = results.Single(result => result.DisplayName == "Add or remove programs");
        var action = addRemoveProgramsResult.CreatePrimaryAction();
        Assert.That(action.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(action.Payload, Is.EqualTo("ms-settings:appsfeatures"));
    }

    [Test]
    public async Task SearchAsyncReturnsEmptyWhenPlatformIsUnsupported()
    {
        var service = new ApplicationSearchService([], [], [], isMacOS: false, isWindows: false);

        var results = await service.SearchAsync("safari", CancellationToken.None);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task WarmAsyncDoesNotRefreshTwiceWhenConcurrentRequestsOverlap()
    {
        var refreshCount = 0;
        var service = new ApplicationSearchService(
            [],
            [],
            [],
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: null,
            windowsStartAppsAccessor: () => [],
            discoverApplicationsOverride: () =>
            {
                refreshCount++;
                Thread.Sleep(150);
                return
                [
                    new IndexedApplication
                    {
                        DisplayName = "Rider",
                        SearchName = "rider",
                        ShortcutFile = new FileInfo(@"C:\Apps\Rider.lnk")
                    }
                ];
            });

        var firstWarmTask = service.WarmAsync();
        await Task.Delay(20);
        await Task.WhenAll(firstWarmTask, service.WarmAsync());

        Assert.That(refreshCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WarmAsyncSkipsRefreshWhenApplicationWatchersReportNoChanges()
    {
        using var tempDirectory = new TempDirectory();
        var programsRoot = tempDirectory.GetDir("Programs");
        programsRoot.Create();
        IndexedApplication[] cachedApplications =
        [
            new IndexedApplication
            {
                DisplayName = "Rider",
                SearchName = "rider",
                ShortcutFile = programsRoot.GetFile("Rider.lnk")
            }
        ];
        var refreshCount = 0;
        var service = new ApplicationSearchService(
            [],
            [programsRoot],
            cachedApplications,
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: DateTime.UtcNow - TimeSpan.FromMinutes(6),
            windowsStartAppsAccessor: () => [],
            discoverApplicationsOverride: () =>
            {
                refreshCount++;
                return cachedApplications;
            },
            enableWatchers: true);

        await service.WarmAsync();

        Assert.That(refreshCount, Is.EqualTo(0));
    }

    [Test]
    public async Task WarmAsyncRefreshesWhenApplicationWatcherReportsChanges()
    {
        using var tempDirectory = new TempDirectory();
        var programsRoot = tempDirectory.GetDir("Programs");
        programsRoot.Create();
        var shortcutFile = programsRoot.GetFile("Rider.lnk");
        shortcutFile.WriteAllText(string.Empty);
        IndexedApplication[] cachedApplications =
        [
            new IndexedApplication
            {
                DisplayName = "Rider",
                SearchName = "rider",
                ShortcutFile = shortcutFile
            }
        ];
        var refreshCount = 0;
        var service = new ApplicationSearchService(
            [],
            [programsRoot],
            cachedApplications,
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: DateTime.UtcNow - TimeSpan.FromMinutes(6),
            windowsStartAppsAccessor: () => [],
            discoverApplicationsOverride: () =>
            {
                refreshCount++;
                return cachedApplications;
            },
            enableWatchers: true);

        service.MarkPathDirty(shortcutFile.FullName);
        await service.WarmAsync();

        Assert.That(refreshCount, Is.EqualTo(1));
    }
}
