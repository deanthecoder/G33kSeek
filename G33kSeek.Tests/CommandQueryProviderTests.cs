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
// ReSharper disable AccessToDisposedClosure

namespace G33kSeek.Tests;

public class CommandQueryProviderTests
{
    [Test]
    public async Task QueryAsyncReturnsAllCommandsForBlankQuery()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">", string.Empty, ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(14));
        Assert.That(response.Results.Select(result => result.Title), Is.EqualTo(new[] { "addfolder", "desktop", "documents", "downloads", "exit", "guid", "home", "ip", "lock", "log", "logoff", "refresh", "restart", "shutdown" }));
    }

    [Test]
    public async Task QueryAsyncFiltersMatchingCommands()
    {
        using var tempDirectory = new TempDirectory();
        var provider = new CommandQueryProvider(
            isMacOS: false,
            isWindows: true,
            guidFactory: () => Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ipAddressesAccessor: () => ["192.168.1.20"],
            commonFolderTargetsAccessor: () => CreateCommonFolderTargets(tempDirectory),
            logFileAccessor: () => tempDirectory.GetFile("log.txt"));

        var response = await provider.QueryAsync(new QueryRequest(">rest", "rest", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("restart"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.RunProcess));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("shutdown"));
        Assert.That(response.Results[0].PrimaryAction?.Arguments, Is.EqualTo("/r /t 0"));
    }

    [Test]
    public async Task QueryAsyncDoesNotMatchDescriptionText()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">include", "include", ">"), CancellationToken.None);

        Assert.That(response.Results, Is.Empty);
    }

    [Test]
    public async Task QueryAsyncReturnsWindowsLockCommand()
    {
        using var tempDirectory = new TempDirectory();
        var provider = new CommandQueryProvider(
            isMacOS: false,
            isWindows: true,
            guidFactory: () => Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ipAddressesAccessor: () => ["192.168.1.20"],
            commonFolderTargetsAccessor: () => CreateCommonFolderTargets(tempDirectory),
            logFileAccessor: () => tempDirectory.GetFile("log.txt"));

        var response = await provider.QueryAsync(new QueryRequest(">lock", "lock", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("lock"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.RunProcess));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("rundll32.exe"));
        Assert.That(response.Results[0].PrimaryAction?.Arguments, Is.EqualTo("user32.dll,LockWorkStation"));
    }

    [Test]
    public async Task QueryAsyncReturnsNoMatchesStatusWhenCommandIsUnknown()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">hibernate", "hibernate", ">"), CancellationToken.None);

        Assert.That(response.Results, Is.Empty);
        Assert.That(response.StatusText, Is.EqualTo("No commands matched \"hibernate\"."));
    }

    [Test]
    public void HelpEntryDescribesCommands()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        Assert.That(provider.HelpEntry.Title, Is.EqualTo("Commands"));
        Assert.That(provider.HelpEntry.Example, Is.EqualTo(">shutdown"));
    }

    [Test]
    public async Task QueryAsyncReturnsGeneratedGuidValue()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">guid", "guid", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("guid"));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("11111111-2222-3333-4444-555555555555"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsIpValue()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20", "10.0.0.5"]);

        var response = await provider.QueryAsync(new QueryRequest(">ip", "ip", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("ip"));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("10.0.0.5, 192.168.1.20"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsLogFileCommand()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">log", "log", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("log"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo(tempDirectory.GetFile("log.txt").FullName));
    }

    [Test]
    public async Task QueryAsyncReturnsDesktopFolderCommand()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">desk", "desk", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("desktop"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.OpenPath));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo(tempDirectory.GetDir("Home/Desktop").FullName));
    }

    [Test]
    public async Task QueryAsyncPrefersShortPrefixMatchesOverInnerSubstrings()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">de", "de", ">"), CancellationToken.None);

        Assert.That(response.Results.Select(result => result.Title), Is.EqualTo(new[] { "desktop" }));
    }

    [Test]
    public async Task QueryAsyncReturnsAddFolderCommand()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">addfolder", "addfolder", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("addfolder"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.AddSearchRoot));
    }

    [Test]
    public async Task QueryAsyncReturnsRefreshCommand()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">refresh", "refresh", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("refresh"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.RefreshIndexes));
        Assert.That(response.Results[0].PrimaryAction?.ShouldHideLauncher, Is.False);
    }

    [Test]
    public async Task QueryAsyncReturnsExitCommand()
    {
        using var tempDirectory = new TempDirectory();
        var provider = CreateProvider(tempDirectory, ["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">exit", "exit", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("exit"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.ExitApp));
    }

    private static CommandQueryProvider CreateProvider(TempDirectory tempDirectory, IReadOnlyList<string> ipAddresses)
    {
        tempDirectory.GetFile("log.txt").WriteAllText("test log");
        return new CommandQueryProvider(
            isMacOS: true,
            isWindows: false,
            guidFactory: () => Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ipAddressesAccessor: () => ipAddresses,
            commonFolderTargetsAccessor: () => CreateCommonFolderTargets(tempDirectory),
            logFileAccessor: () => tempDirectory.GetFile("log.txt"));
    }

    private static CommandQueryProvider.CommonFolderTargets CreateCommonFolderTargets(TempDirectory tempDirectory)
    {
        var homeDirectory = tempDirectory.GetDir("Home");
        homeDirectory.Create();
        var desktopDirectory = homeDirectory.GetDir("Desktop");
        desktopDirectory.Create();
        var documentsDirectory = homeDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var downloadsDirectory = homeDirectory.GetDir("Downloads");
        downloadsDirectory.Create();
        return new CommandQueryProvider.CommonFolderTargets(homeDirectory, desktopDirectory, documentsDirectory, downloadsDirectory);
    }
}
