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

public class HelpQueryProviderTests
{
    private readonly HelpQueryProvider m_provider = new();

    [Test]
    public async Task QueryAsyncReturnsAllTopicsForBlankHelpQuery()
    {
        var response = await m_provider.QueryAsync(new QueryRequest("?", string.Empty, "?"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(6));
        Assert.That(response.StatusText, Does.StartWith("Help: try an app name"));
        Assert.That(response.Results[0].Title, Is.EqualTo("App and file search"));
    }

    [Test]
    public async Task QueryAsyncFiltersTopicsWhenSearchTextIsProvided()
    {
        var response = await m_provider.QueryAsync(new QueryRequest("?calc", "calc", "?"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Calculator"));
        Assert.That(response.StatusText, Does.Contain("\"calc\""));
    }

    [Test]
    public async Task QueryAsyncReturnsFallbackRowWhenNoTopicsMatch()
    {
        var response = await m_provider.QueryAsync(new QueryRequest("?zebra", "zebra", "?"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("No help topics matched."));
        Assert.That(response.StatusText, Is.EqualTo("Help: no topics matched. Try a broader search term."));
    }
}
