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
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using DTC.Core.Extensions;
using G33kSeek.Models;

namespace G33kSeek.Services;

/// <summary>
/// Executes the default action associated with a query result.
/// </summary>
/// <remarks>
/// This centralizes clipboard and open-item behavior so providers only need to describe what Enter should do.
/// </remarks>
public sealed class QueryExecutionService
{
    public static async Task<QueryExecutionResult> ExecuteAsync(
        QueryResult result,
        Window ownerWindow,
        CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        if (ownerWindow == null)
            throw new ArgumentNullException(nameof(ownerWindow));

        cancellationToken.ThrowIfCancellationRequested();

        if (result.PrimaryAction == null || result.PrimaryAction.Kind == QueryActionKind.None)
            return new QueryExecutionResult(false, "That result does not have an action.");

        var action = result.PrimaryAction;
        switch (action.Kind)
        {
            case QueryActionKind.CopyText:
                var clipboard = TopLevel.GetTopLevel(ownerWindow)?.Clipboard;
                if (clipboard == null)
                    return new QueryExecutionResult(false, "Clipboard is not currently available.");

                await clipboard.SetTextAsync(action.Payload ?? string.Empty);
                return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);

            case QueryActionKind.OpenPath:
                if (File.Exists(action.Payload))
                {
                    new FileInfo(action.Payload).OpenWithDefaultViewer();
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                if (Directory.Exists(action.Payload))
                {
                    Process.Start(new ProcessStartInfo(action.Payload) { UseShellExecute = true });
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                return new QueryExecutionResult(false, "The target path no longer exists.");

            case QueryActionKind.OpenUri:
                if (Uri.TryCreate(action.Payload, UriKind.Absolute, out var uri))
                {
                    uri.Open();
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                return new QueryExecutionResult(false, "The target URL was invalid.");

            case QueryActionKind.RunProcess:
                if (string.IsNullOrWhiteSpace(action.Payload))
                    return new QueryExecutionResult(false, "The target command was invalid.");

                Process.Start(
                    new ProcessStartInfo(action.Payload, action.Arguments ?? string.Empty)
                    {
                        UseShellExecute = false
                    });
                return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);

            default:
                return new QueryExecutionResult(false, "That action type is not implemented yet.");
        }
    }
}
