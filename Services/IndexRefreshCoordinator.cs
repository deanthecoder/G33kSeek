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
using System.Threading;
using System.Threading.Tasks;
using DTC.Core;

namespace G33kSeek.Services;

/// <summary>
/// Coordinates application and file index refresh operations.
/// </summary>
/// <remarks>
/// This keeps manual refresh commands and UI activity indicators simple by treating both indexes as one refresh workflow.
/// </remarks>
internal sealed class IndexRefreshCoordinator : IIndexRefreshCoordinator
{
    private readonly ApplicationSearchService m_applicationSearchService;
    private readonly FileSearchService m_fileSearchService;
    private readonly object m_refreshAllSync = new();
    private Task m_activeRefreshTask = Task.CompletedTask;

    public IndexRefreshCoordinator(
        ApplicationSearchService applicationSearchService,
        FileSearchService fileSearchService)
    {
        m_applicationSearchService = applicationSearchService ?? throw new ArgumentNullException(nameof(applicationSearchService));
        m_fileSearchService = fileSearchService ?? throw new ArgumentNullException(nameof(fileSearchService));

        m_applicationSearchService.RefreshStateChanged += OnRefreshStateChanged;
        m_fileSearchService.RefreshStateChanged += OnRefreshStateChanged;
    }

    public bool IsRefreshing => m_applicationSearchService.IsRefreshing || m_fileSearchService.IsRefreshing;

    public event EventHandler RefreshStateChanged;

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            WarmApplicationIndexAsync(cancellationToken),
            WarmFileIndexAsync(cancellationToken));
    }

    public Task RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        lock (m_refreshAllSync)
        {
            if (!m_activeRefreshTask.IsCompleted)
                return m_activeRefreshTask;

            m_activeRefreshTask = RefreshAllCoreAsync(cancellationToken);
            return m_activeRefreshTask;
        }
    }

    private async Task RefreshAllCoreAsync(CancellationToken cancellationToken)
    {
        await Task.WhenAll(
            m_applicationSearchService.RefreshNowAsync(cancellationToken),
            m_fileSearchService.RefreshNowAsync(cancellationToken));
    }

    private async Task WarmApplicationIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            await m_applicationSearchService.WarmAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Instance.Exception("Application index warmup failed.", ex);
        }
    }

    private async Task WarmFileIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            await m_fileSearchService.WarmAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Instance.Exception("File index warmup failed.", ex);
        }
    }

    private void OnRefreshStateChanged(object sender, EventArgs e) =>
        RefreshStateChanged?.Invoke(this, EventArgs.Empty);
}
