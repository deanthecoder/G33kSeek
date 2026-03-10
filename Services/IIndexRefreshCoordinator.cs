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

namespace G33kSeek.Services;

/// <summary>
/// Coordinates background refresh activity for launcher indexes.
/// </summary>
/// <remarks>
/// This provides one place for the UI and command layer to observe and trigger application and file index refreshes together.
/// </remarks>
internal interface IIndexRefreshCoordinator
{
    bool IsRefreshing { get; }

    event EventHandler RefreshStateChanged;

    Task WarmAsync(CancellationToken cancellationToken = default);

    Task RefreshAllAsync(CancellationToken cancellationToken = default);
}
