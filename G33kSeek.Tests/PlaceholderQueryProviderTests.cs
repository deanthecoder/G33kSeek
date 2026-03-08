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

public class PlaceholderQueryProviderTests
{
    [Test]
    public void ConstructorAssignsPrefix()
    {
        var provider = new PlaceholderQueryProvider("??", "Content search", "Search will be added later.");

        Assert.That(provider.Prefix, Is.EqualTo("??"));
    }

    [Test]
    public async Task QueryAsyncReturnsPlaceholderResult()
    {
        var provider = new PlaceholderQueryProvider("??", "Content search", "Search will be added later.");

        var response = await provider.QueryAsync(new QueryRequest("??todo", "todo", "??"), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("Content search"));
        Assert.That(response.Results[0].Subtitle, Does.Contain("todo"));
        Assert.That(response.StatusText, Is.EqualTo("Content search will be implemented in a later slice."));
    }
}
