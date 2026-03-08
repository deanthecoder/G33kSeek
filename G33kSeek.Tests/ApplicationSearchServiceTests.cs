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

        var service = new ApplicationSearchService([rootDirectory], [], isMacOS: true);

        var results = await service.SearchAsync("saf", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].DisplayName, Is.EqualTo("Safari"));
    }

    [Test]
    public async Task SearchAsyncPrefersStartsWithMatchesOverContainsMatches()
    {
        var service = new ApplicationSearchService(
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
            DateTime.UtcNow);

        var results = await service.SearchAsync("code", CancellationToken.None);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].DisplayName, Is.EqualTo("Code Runner"));
    }

    [Test]
    public async Task SearchAsyncReturnsEmptyWhenPlatformIsUnsupported()
    {
        var service = new ApplicationSearchService([], [], isMacOS: false);

        var results = await service.SearchAsync("safari", CancellationToken.None);

        Assert.That(results, Is.Empty);
    }
}
