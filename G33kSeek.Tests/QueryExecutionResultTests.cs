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

public class QueryExecutionResultTests
{
    [Test]
    public void ConstructorAssignsProvidedValues()
    {
        var result = new QueryExecutionResult(true, "Copied.", shouldHideLauncher: true);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.StatusText, Is.EqualTo("Copied."));
        Assert.That(result.ShouldHideLauncher, Is.True);
    }
}
