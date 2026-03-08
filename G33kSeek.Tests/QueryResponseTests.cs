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

namespace G33kSeek.Tests;

public class QueryResponseTests
{
    [Test]
    public void ConstructorAssignsResultsAndStatus()
    {
        var rows = new[] { new QueryResult("42") };

        var response = new QueryResponse(rows, "Ready.");

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("42"));
        Assert.That(response.StatusText, Is.EqualTo("Ready."));
    }
}
