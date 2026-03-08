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

public class DefaultQueryProviderTests
{
    private readonly DefaultQueryProvider m_provider = new();

    [Test]
    public async Task QueryAsyncReturnsModeSummaryForBlankQuery()
    {
        var response = await m_provider.QueryAsync(new QueryRequest(string.Empty, string.Empty, string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(6));
        Assert.That(response.StatusText, Is.EqualTo("Type to search, or use a prefix to switch modes."));
    }

    [Test]
    public async Task QueryAsyncReturnsPlaceholderResultForTypedDefaultQuery()
    {
        var response = await m_provider.QueryAsync(new QueryRequest("readme", "readme", string.Empty), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Application and file search is next."));
        Assert.That(response.Results[0].Subtitle, Does.Contain("readme"));
    }
}
