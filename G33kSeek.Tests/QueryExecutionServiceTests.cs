// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Diagnostics;
using DTC.Core;
using DTC.Core.Extensions;
using G33kSeek.Models;
using G33kSeek.Services;

namespace G33kSeek.Tests;

public class QueryExecutionServiceTests
{
    [SetUp]
    public void SetUp()
    {
        QueryExecutionService.FileOpener = file => file.OpenWithDefaultViewer();
        QueryExecutionService.DirectoryOpener = directory => directory.Explore();
        QueryExecutionService.FileRevealer = file => file.Explore();
        QueryExecutionService.DirectoryRevealer = directory => directory.Explore();
        QueryExecutionService.UriOpener = uri => uri.Open();
        QueryExecutionService.ProcessStarter = processStartInfo => Process.Start(processStartInfo);
        QueryExecutionService.ExitApplication = null;
        QueryExecutionService.SearchRootPicker = _ => Task.FromResult<DirectoryInfo>(null);
        QueryExecutionService.SearchRootAdder = (_, _) => Task.FromResult(FileSearchRootAddStatus.Unavailable);
        QueryExecutionService.IndexRefresher = _ => Task.CompletedTask;
    }

    [Test]
    public async Task ExecuteAsyncUsesDirectoryOpenerForDirectoryPaths()
    {
        using var tempDirectory = new TempDirectory();
        var reportsDirectory = tempDirectory.GetDir("Reports");
        reportsDirectory.Create();
        DirectoryInfo openedDirectory = null;

        QueryExecutionService.DirectoryOpener = directory => openedDirectory = directory;
        QueryExecutionService.FileOpener = _ => Assert.Fail("File opener should not be used for directories.");

        var result = await QueryExecutionService.ExecuteAsync(
            new QueryResult(
                "Reports",
                primaryAction: new QueryActionDescriptor(QueryActionKind.OpenPath, reportsDirectory.FullName)),
            null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(openedDirectory?.FullName, Is.EqualTo(reportsDirectory.FullName));
    }

    [Test]
    public async Task ExecuteAsyncUsesFileOpenerForFilePaths()
    {
        using var tempDirectory = new TempDirectory();
        var reportFile = tempDirectory.GetFile("report.txt");
        reportFile.WriteAllText("report");
        FileInfo openedFile = null;

        QueryExecutionService.FileOpener = file => openedFile = file;
        QueryExecutionService.DirectoryOpener = _ => Assert.Fail("Directory opener should not be used for files.");

        var result = await QueryExecutionService.ExecuteAsync(
            new QueryResult(
                "report.txt",
                primaryAction: new QueryActionDescriptor(QueryActionKind.OpenPath, reportFile.FullName)),
            null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(openedFile?.FullName, Is.EqualTo(reportFile.FullName));
    }

    [Test]
    public async Task ExecuteAsyncUsesFileRevealerForFilePaths()
    {
        using var tempDirectory = new TempDirectory();
        var reportFile = tempDirectory.GetFile("report.txt");
        reportFile.WriteAllText("report");
        FileInfo revealedFile = null;

        QueryExecutionService.FileRevealer = file => revealedFile = file;
        QueryExecutionService.DirectoryRevealer = _ => Assert.Fail("Directory revealer should not be used for files.");

        var result = await QueryExecutionService.ExecuteAsync(
            new QueryResult(
                "report.txt",
                primaryAction: new QueryActionDescriptor(QueryActionKind.RevealPath, reportFile.FullName)),
            null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(revealedFile?.FullName, Is.EqualTo(reportFile.FullName));
    }

    [Test]
    public async Task ExecuteAsyncUsesDirectoryRevealerForDirectoryPaths()
    {
        using var tempDirectory = new TempDirectory();
        var reportsDirectory = tempDirectory.GetDir("Reports");
        reportsDirectory.Create();
        DirectoryInfo revealedDirectory = null;

        QueryExecutionService.DirectoryRevealer = directory => revealedDirectory = directory;
        QueryExecutionService.FileRevealer = _ => Assert.Fail("File revealer should not be used for directories.");

        var result = await QueryExecutionService.ExecuteAsync(
            new QueryResult(
                "Reports",
                primaryAction: new QueryActionDescriptor(QueryActionKind.RevealPath, reportsDirectory.FullName)),
            null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(revealedDirectory?.FullName, Is.EqualTo(reportsDirectory.FullName));
    }

    [Test]
    public void ExecuteAsyncRequiresOwnerWindowForClipboardActions()
    {
        Assert.ThrowsAsync<ArgumentNullException>(
            async () => await QueryExecutionService.ExecuteAsync(
                new QueryResult(
                    "Copied text",
                    primaryAction: new QueryActionDescriptor(QueryActionKind.CopyText, "text")),
                null));
    }

    [Test]
    public async Task ExecuteAsyncAddsSelectedSearchRoot()
    {
        using var tempDirectory = new TempDirectory();
        var pickedDirectory = tempDirectory.GetDir("Extra");
        pickedDirectory.Create();
        DirectoryInfo addedDirectory = null;
        QueryExecutionService.SearchRootPicker = _ => Task.FromResult(pickedDirectory);
        QueryExecutionService.SearchRootAdder = (directory, _) =>
        {
            addedDirectory = directory;
            return Task.FromResult(FileSearchRootAddStatus.Added);
        };

        var result = await QueryExecutionService.ExecuteAsync(
            new QueryResult(
                "addfolder",
                primaryAction: new QueryActionDescriptor(QueryActionKind.AddSearchRoot)),
            null);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.StatusText, Is.EqualTo($"Added {pickedDirectory.FullName} to file search."));
        Assert.That(addedDirectory?.FullName, Is.EqualTo(pickedDirectory.FullName));
    }

    [Test]
    public async Task ExecuteAsyncStartsBackgroundRefreshWithoutHidingLauncher()
    {
        var refreshStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        QueryExecutionService.IndexRefresher = _ =>
        {
            refreshStarted.TrySetResult(true);
            return Task.Delay(50);
        };

        var result = await QueryExecutionService.ExecuteAsync(
            new QueryResult(
                "refresh",
                primaryAction: new QueryActionDescriptor(
                    QueryActionKind.RefreshIndexes,
                    successMessage: "Refreshing app and file indexes.",
                    shouldHideLauncher: false)),
            null);

        await refreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.StatusText, Is.EqualTo("Refreshing app and file indexes."));
        Assert.That(result.ShouldHideLauncher, Is.False);
    }

    [Test]
    public async Task ExecuteAsyncExitsApplication()
    {
        var exitRequested = false;
        QueryExecutionService.ExitApplication = () => exitRequested = true;

        var result = await QueryExecutionService.ExecuteAsync(
            new QueryResult(
                "exit",
                primaryAction: new QueryActionDescriptor(
                    QueryActionKind.ExitApp,
                    successMessage: "Exiting G33kSeek.")),
            null);

        Assert.That(exitRequested, Is.True);
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.StatusText, Is.EqualTo("Exiting G33kSeek."));
    }
}
