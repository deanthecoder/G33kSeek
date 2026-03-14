// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using G33kSeek.Models;
using G33kSeek.Services;
// ReSharper disable AccessToDisposedClosure

namespace G33kSeek.Tests;

public class IndexRefreshCoordinatorTests
{
    [Test]
    public async Task RefreshAllAsyncRefreshesBothIndexes()
    {
        var applicationRefreshCount = 0;
        var fileRefreshCount = 0;
        using var gate = new ManualResetEventSlim(false);
        var applicationSearchService = new ApplicationSearchService(
            [],
            [],
            [],
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: DateTime.UtcNow,
            windowsStartAppsAccessor: () => [],
            discoverApplicationsOverride: () =>
            {
                applicationRefreshCount++;
                gate.Wait(TimeSpan.FromSeconds(1));
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
        var fileSearchService = new FileSearchService(
            [],
            [],
            DateTime.UtcNow,
            _ =>
            {
                fileRefreshCount++;
                gate.Wait(TimeSpan.FromSeconds(1));
                return ([], 0, 0);
            });
        var coordinator = new IndexRefreshCoordinator(applicationSearchService, fileSearchService);

        var refreshTask = coordinator.RefreshAllAsync();
        Assert.That(coordinator.IsRefreshing, Is.True);

        gate.Set();
        await refreshTask;

        Assert.That(applicationRefreshCount, Is.EqualTo(1));
        Assert.That(fileRefreshCount, Is.EqualTo(1));
        Assert.That(coordinator.IsRefreshing, Is.False);
    }

    [Test]
    public async Task RefreshAllAsyncDoesNotDuplicateConcurrentRequests()
    {
        var applicationRefreshCount = 0;
        var fileRefreshCount = 0;
        using var gate = new ManualResetEventSlim(false);
        var applicationSearchService = new ApplicationSearchService(
            [],
            [],
            [],
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: DateTime.UtcNow,
            windowsStartAppsAccessor: () => [],
            discoverApplicationsOverride: () =>
            {
                applicationRefreshCount++;
                gate.Wait(TimeSpan.FromSeconds(1));
                return [];
            });
        var fileSearchService = new FileSearchService(
            [],
            [],
            DateTime.UtcNow,
            _ =>
            {
                fileRefreshCount++;
                gate.Wait(TimeSpan.FromSeconds(1));
                return ([], 0, 0);
            });
        var coordinator = new IndexRefreshCoordinator(applicationSearchService, fileSearchService);

        var firstRefreshTask = coordinator.RefreshAllAsync();
        var secondRefreshTask = coordinator.RefreshAllAsync();
        gate.Set();
        await Task.WhenAll(firstRefreshTask, secondRefreshTask);

        Assert.That(applicationRefreshCount, Is.EqualTo(1));
        Assert.That(fileRefreshCount, Is.EqualTo(1));
    }

    [Test]
    public async Task StartBackgroundRefreshLoopWarmsIndexesWithoutUserQuery()
    {
        var applicationRefreshCount = 0;
        var fileRefreshCount = 0;
        var applicationRefreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fileRefreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var applicationSearchService = new ApplicationSearchService(
            [],
            [],
            [],
            isMacOS: false,
            isWindows: true,
            lastRefreshUtc: DateTime.MinValue,
            windowsStartAppsAccessor: () => [],
            discoverApplicationsOverride: () =>
            {
                applicationRefreshCount++;
                applicationRefreshed.TrySetResult();
                return [];
            });
        var fileSearchService = new FileSearchService(
            [],
            [],
            DateTime.MinValue,
            _ =>
            {
                fileRefreshCount++;
                fileRefreshed.TrySetResult();
                return ([], 0, 0);
            });
        var coordinator = new IndexRefreshCoordinator(applicationSearchService, fileSearchService);

        coordinator.StartBackgroundRefreshLoop(TimeSpan.Zero, TimeSpan.FromMilliseconds(20));

        await Task.WhenAll(
            applicationRefreshed.Task.WaitAsync(TimeSpan.FromSeconds(1)),
            fileRefreshed.Task.WaitAsync(TimeSpan.FromSeconds(1)));

        coordinator.StopBackgroundRefreshLoop();

        Assert.That(applicationRefreshCount, Is.EqualTo(1));
        Assert.That(fileRefreshCount, Is.EqualTo(1));
    }
}
