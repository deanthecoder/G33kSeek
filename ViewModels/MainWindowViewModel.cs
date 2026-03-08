// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using DTC.Core.ViewModels;
using G33kSeek.Models;
using G33kSeek.Services;

namespace G33kSeek.ViewModels;

/// <summary>
/// Provides the initial launcher surface state for G33kSeek.
/// </summary>
/// <remarks>
/// This keeps the first-run experience simple while the provider system is still being built out.
/// </remarks>
public class MainWindowViewModel : ViewModelBase
{
    private readonly QueryEngine m_queryEngine;
    private string m_searchText;
    private string m_statusText = "Press Ctrl+Space to toggle the launcher. Esc dismisses it.";
    private QueryResult m_selectedResult;
    private CancellationTokenSource m_queryCancellation;

    public MainWindowViewModel(QueryEngine queryEngine = null)
    {
        m_queryEngine = queryEngine;
        Results = new ObservableCollection<QueryResult>();
        _ = RefreshResultsAsync();
    }

    public ObservableCollection<QueryResult> Results { get; }

    public string SearchText
    {
        get => m_searchText;
        set
        {
            if (!SetField(ref m_searchText, value))
                return;

            _ = RefreshResultsAsync();
        }
    }

    public string StatusText
    {
        get => m_statusText;
        set => SetField(ref m_statusText, value);
    }

    public QueryResult SelectedResult
    {
        get => m_selectedResult;
        set => SetField(ref m_selectedResult, value);
    }

    private async Task RefreshResultsAsync()
    {
        m_queryCancellation?.Cancel();
        m_queryCancellation = new CancellationTokenSource();
        var cancellationToken = m_queryCancellation.Token;
        var response = await m_queryEngine.QueryAsync(SearchText, cancellationToken);
        if (cancellationToken.IsCancellationRequested)
            return;

        Results.Clear();
        foreach (var result in response.Results)
            Results.Add(result);

        SelectedResult = Results.Count > 0 ? Results[0] : null;
        StatusText = string.IsNullOrWhiteSpace(response.StatusText)
            ? "Ready."
            : response.StatusText;
    }
}
