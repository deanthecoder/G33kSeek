// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
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
    private static readonly string HotkeyDisplayText = GlobalHotkeyService.GetShortcutDisplayText(OperatingSystem.IsWindows());
    private readonly QueryEngine m_queryEngine;
    private string m_searchText;
    private string m_statusText = $"Press {HotkeyDisplayText} to toggle the launcher. Esc dismisses it.";
    private QueryResult m_selectedResult;
    private int m_visibleResultCount;
    private bool m_hasResults;
    private double m_resultsPanelOpacity;
    private double m_resultsPanelMaxHeight;
    private CancellationTokenSource m_queryCancellation;
    private bool m_isRefreshActive;

    public MainWindowViewModel(QueryEngine queryEngine = null)
        : this(queryEngine, null)
    {
    }

    internal MainWindowViewModel(QueryEngine queryEngine, IIndexRefreshCoordinator indexRefreshCoordinator)
    {
        m_queryEngine = queryEngine;
        Results = new ObservableCollection<QueryResult>();
        IsRefreshActive = indexRefreshCoordinator?.IsRefreshing == true;
        if (indexRefreshCoordinator != null)
            indexRefreshCoordinator.RefreshStateChanged += (_, _) => UpdateRefreshState(indexRefreshCoordinator.IsRefreshing);
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

    public int VisibleResultCount
    {
        get => m_visibleResultCount;
        private set => SetField(ref m_visibleResultCount, value);
    }

    public bool HasResults
    {
        get => m_hasResults;
        private set => SetField(ref m_hasResults, value);
    }

    public double ResultsPanelOpacity
    {
        get => m_resultsPanelOpacity;
        private set => SetField(ref m_resultsPanelOpacity, value);
    }

    public double ResultsPanelMaxHeight
    {
        get => m_resultsPanelMaxHeight;
        private set => SetField(ref m_resultsPanelMaxHeight, value);
    }

    public bool IsRefreshActive
    {
        get => m_isRefreshActive;
        private set => SetField(ref m_isRefreshActive, value);
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
        VisibleResultCount = Results.Count;
        HasResults = Results.Count > 0;
        ResultsPanelOpacity = HasResults ? 1 : 0;
        ResultsPanelMaxHeight = HasResults ? 322 : 0;
        StatusText = string.IsNullOrWhiteSpace(response.StatusText)
            ? "Ready."
            : response.StatusText;
    }

    private void UpdateRefreshState(bool isRefreshActive)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            IsRefreshActive = isRefreshActive;
            return;
        }

        Dispatcher.UIThread.Post(() => IsRefreshActive = isRefreshActive);
    }
}
