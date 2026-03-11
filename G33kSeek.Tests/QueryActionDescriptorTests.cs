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

public class QueryActionDescriptorTests
{
    [Test]
    public void ConstructorAssignsProvidedValues()
    {
        var descriptor = new QueryActionDescriptor(
            QueryActionKind.CopyText,
            "42",
            arguments: "--value",
            "Copied.",
            shouldHideLauncher: false);

        Assert.That(descriptor.Kind, Is.EqualTo(QueryActionKind.CopyText));
        Assert.That(descriptor.Payload, Is.EqualTo("42"));
        Assert.That(descriptor.Arguments, Is.EqualTo("--value"));
        Assert.That(descriptor.SuccessMessage, Is.EqualTo("Copied."));
        Assert.That(descriptor.ShouldHideLauncher, Is.False);
        Assert.That(descriptor.DisplayText, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ConstructorAssignsDisplayText()
    {
        var descriptor = new QueryActionDescriptor(
            QueryActionKind.RevealPath,
            "c:\\temp\\file.txt",
            successMessage: "Revealed.",
            displayText: "Reveal in Explorer");

        Assert.That(descriptor.DisplayText, Is.EqualTo("Reveal in Explorer"));
    }
}
