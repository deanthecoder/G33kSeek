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

public class QueryProviderHelpEntryTests
{
    [Test]
    public void ConstructorAssignsProvidedValues()
    {
        var entry = new QueryProviderHelpEntry("Calculator", "Evaluate expressions.", "=2+2");

        Assert.That(entry.Title, Is.EqualTo("Calculator"));
        Assert.That(entry.Description, Is.EqualTo("Evaluate expressions."));
        Assert.That(entry.Example, Is.EqualTo("=2+2"));
    }
}
