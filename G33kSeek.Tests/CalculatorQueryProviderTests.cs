// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Globalization;
using G33kSeek.Models;
using G33kSeek.Providers;

namespace G33kSeek.Tests;

public class CalculatorQueryProviderTests
{
    private readonly CalculatorQueryProvider m_provider = new();

    [Test]
    public async Task QueryAsyncEvaluatesArithmeticExpression()
    {
        var response = await m_provider.QueryAsync(new QueryRequest("=2+2*3", "2+2*3", "="), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("8"));
        Assert.That(response.Results[0].PrimaryAction?.Kind, Is.EqualTo(QueryActionKind.CopyText));
        Assert.That(response.Results[0].PrimaryAction?.Payload, Is.EqualTo("8"));
    }

    [Test]
    public void HelpEntryDescribesCalculatorMode()
    {
        Assert.That(m_provider.HelpEntry.Title, Is.EqualTo("Calculator"));
        Assert.That(m_provider.HelpEntry.Example, Is.EqualTo("=sin(pi/2)"));
    }

    [Test]
    public async Task QueryAsyncEvaluatesTrigExpressionUsingRadians()
    {
        var response = await m_provider.QueryAsync(new QueryRequest("=sin(pi / 2)", "sin(pi / 2)", "="), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        var numericResult = double.Parse(response.Results[0].Title, CultureInfo.InvariantCulture);
        Assert.That(numericResult, Is.EqualTo(1d).Within(0.0000001d));
    }

    [Test]
    public async Task QueryAsyncEvaluatesLargeIntegerMultiplicationWithoutOverflow()
    {
        var response = await m_provider.QueryAsync(new QueryRequest("=111111*111111", "111111*111111", "="), CancellationToken.None);

        Assert.That(response.Results, Has.Count.EqualTo(1));
        Assert.That(response.Results[0].Title, Is.EqualTo("12345654321"));
    }
}
