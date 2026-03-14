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
using G33kSeek.Providers;

namespace G33kSeek.Tests;

public class EmojiQueryProviderTests
{
    [Test]
    public async Task QueryAsyncReturnsCommonEmojiForBlankQuery()
    {
        var provider = new EmojiQueryProvider();

        var response = await provider.QueryAsync(new QueryRequest(":", string.Empty, ":"), CancellationToken.None);

        Assert.That(response.Results, Is.Not.Empty);
        Assert.That(response.Results[0].Title, Does.StartWith("🙂"));
        Assert.That(response.StatusText, Is.EqualTo("Emoji mode is ready. Try :smile, :heart, :wave, or :)."));
    }

    [Test]
    public async Task QueryAsyncMatchesShortcode()
    {
        var provider = new EmojiQueryProvider();

        var response = await provider.QueryAsync(new QueryRequest(":heart", "heart", ":"), CancellationToken.None);

        Assert.That(response.Results, Is.Not.Empty);
        Assert.That(response.Results[0].Title, Does.Contain(":heart"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("❤️"));
    }

    [Test]
    public async Task QueryAsyncMatchesEmoticonAlias()
    {
        var provider = new EmojiQueryProvider();

        var response = await provider.QueryAsync(new QueryRequest(":)", ")", ":"), CancellationToken.None);

        Assert.That(response.Results, Is.Not.Empty);
        Assert.That(response.Results[0].Title, Does.StartWith("🙂"));
    }

    [Test]
    public async Task QueryAsyncMatchesNamesWithSpaces()
    {
        var provider = new EmojiQueryProvider();

        var response = await provider.QueryAsync(new QueryRequest(":fist bump", "fist bump", ":"), CancellationToken.None);

        Assert.That(response.Results, Is.Not.Empty);
        Assert.That(response.Results[0].Title, Does.Contain(":fist_bump"));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("👊"));
    }

    [Test]
    public async Task QueryAsyncReturnsNoMatchesMessageWhenUnknown()
    {
        var provider = new EmojiQueryProvider();

        var response = await provider.QueryAsync(new QueryRequest(":volcanoface", "volcanoface", ":"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("No emoji matched."));
        Assert.That(response.StatusText, Is.EqualTo("Emoji: no matches found."));
    }

    [Test]
    public void HelpEntryDescribesEmojiMode()
    {
        var provider = new EmojiQueryProvider();

        Assert.That(provider.HelpEntry.Title, Is.EqualTo("Emoji"));
        Assert.That(provider.HelpEntry.Example, Is.EqualTo(":smile"));
    }
}
