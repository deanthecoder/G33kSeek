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
using G33kSeek.Models;
using G33kSeek.Providers;

namespace G33kSeek.Tests;

public class CommandQueryProviderTests
{
    [Test]
    public async Task QueryAsyncReturnsAllCommandsForBlankQuery()
    {
        var provider = CreateProvider(["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">", string.Empty, ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(5));
        Assert.That(response.Results.Select(result => result.Title), Is.EqualTo(new[] { "guid", "ip", "logoff", "restart", "shutdown" }));
    }

    [Test]
    public async Task QueryAsyncFiltersMatchingCommands()
    {
        var provider = new CommandQueryProvider(
            isMacOS: false,
            isWindows: true,
            guidFactory: () => Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ipAddressesAccessor: () => ["192.168.1.20"]);

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
        var provider = CreateProvider(["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">sh", "sh", ">"), CancellationToken.None);

        Assert.That(response.Results.Select(result => result.Title), Is.EqualTo(new[] { "shutdown" }));
    }

    [Test]
    public async Task QueryAsyncReturnsNoMatchesStatusWhenCommandIsUnknown()
    {
        var provider = CreateProvider(["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">hibernate", "hibernate", ">"), CancellationToken.None);

        Assert.That(response.Results, Is.Empty);
        Assert.That(response.StatusText, Is.EqualTo("No commands matched \"hibernate\"."));
    }

    [Test]
    public void HelpEntryDescribesCommands()
    {
        var provider = CreateProvider(["192.168.1.20"]);

        Assert.That(provider.HelpEntry.Title, Is.EqualTo("Commands"));
        Assert.That(provider.HelpEntry.Example, Is.EqualTo(">shutdown"));
    }

    [Test]
    public async Task QueryAsyncReturnsGeneratedGuidValue()
    {
        var provider = CreateProvider(["192.168.1.20"]);

        var response = await provider.QueryAsync(new QueryRequest(">guid", "guid", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("guid"));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("11111111-2222-3333-4444-555555555555"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    [Test]
    public async Task QueryAsyncReturnsIpValue()
    {
        var provider = CreateProvider(["192.168.1.20", "10.0.0.5"]);

        var response = await provider.QueryAsync(new QueryRequest(">ip", "ip", ">"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("ip"));
        Assert.That(response.Results[0].Subtitle, Is.EqualTo("10.0.0.5, 192.168.1.20"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
    }

    private static CommandQueryProvider CreateProvider(IReadOnlyList<string> ipAddresses)
    {
        return new CommandQueryProvider(
            isMacOS: true,
            isWindows: false,
            guidFactory: () => Guid.Parse("11111111-2222-3333-4444-555555555555"),
            ipAddressesAccessor: () => ipAddresses);
    }
}
