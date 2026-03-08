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
using G33kSeek.Services;
using QueryProvider = G33kSeek.Providers.IQueryProvider;

namespace G33kSeek.Tests;

public class QueryEngineTests
{
    [Test]
    public async Task QueryAsyncRoutesPrefixlessInputToDefaultProvider()
    {
        var defaultProvider = new FakeQueryProvider(string.Empty, "default");
        var engine = new QueryEngine([new FakeQueryProvider("=", "calculator"), defaultProvider]);

        var response = await engine.QueryAsync("notes.md");

        Assert.That(response.Results[0].Title, Is.EqualTo("default"));
        Assert.That(defaultProvider.LastRequest.ProviderQuery, Is.EqualTo("notes.md"));
        Assert.That(defaultProvider.LastRequest.Prefix, Is.EqualTo(string.Empty));
    }

    [Test]
    public async Task QueryAsyncUsesLongestMatchingPrefix()
    {
        var grepProvider = new FakeQueryProvider("??", "grep");
        var webProvider = new FakeQueryProvider("?", "web");
        var engine = new QueryEngine([grepProvider, webProvider, new FakeQueryProvider(string.Empty, "default")]);

        var response = await engine.QueryAsync("??TODO");

        Assert.That(response.Results[0].Title, Is.EqualTo("grep"));
        Assert.That(grepProvider.LastRequest.ProviderQuery, Is.EqualTo("TODO"));
        Assert.That(grepProvider.LastRequest.Prefix, Is.EqualTo("??"));
    }

    private sealed class FakeQueryProvider : QueryProvider
    {
        private readonly string m_title;

        public FakeQueryProvider(string prefix, string title)
        {
            Prefix = prefix;
            m_title = title;
        }

        public string Prefix { get; }

        public QueryRequest LastRequest { get; private set; }

        public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new QueryResponse([new QueryResult(m_title)]));
        }
    }
}
