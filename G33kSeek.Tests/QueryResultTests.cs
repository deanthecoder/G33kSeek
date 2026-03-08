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

public class QueryResultTests
{
    [Test]
    public void ConstructorAssignsProvidedValues()
    {
        var action = new QueryActionDescriptor(QueryActionKind.CopyText, "42");
        var result = new QueryResult("42", "Calculated value", "Enter copies", action);

        Assert.That(result.Title, Is.EqualTo("42"));
        Assert.That(result.Subtitle, Is.EqualTo("Calculated value"));
        Assert.That(result.TrailingText, Is.EqualTo("Enter copies"));
        Assert.That(result.PrimaryAction, Is.SameAs(action));
    }
}
