// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace G33kSeek.Models;

/// <summary>
/// Describes the outcome of executing a query result.
/// </summary>
/// <remarks>
/// This gives the window enough information to update status text and decide whether to hide after execution.
/// </remarks>
public sealed class QueryExecutionResult
{
    public QueryExecutionResult(
        bool isSuccess,
        string statusText = null,
        bool shouldHideLauncher = false)
    {
        IsSuccess = isSuccess;
        StatusText = statusText ?? string.Empty;
        ShouldHideLauncher = shouldHideLauncher;
    }

    public bool IsSuccess { get; }

    public string StatusText { get; }

    public bool ShouldHideLauncher { get; }
}
