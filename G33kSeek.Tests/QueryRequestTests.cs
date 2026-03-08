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

public class QueryRequestTests
{
    [Test]
    public void ConstructorAssignsProvidedValues()
    {
        var request = new QueryRequest("=2+2", "2+2", "=");

        Assert.That(request.RawQuery, Is.EqualTo("=2+2"));
        Assert.That(request.ProviderQuery, Is.EqualTo("2+2"));
        Assert.That(request.Prefix, Is.EqualTo("="));
        Assert.That(request.HasPrefix, Is.True);
    }
}
