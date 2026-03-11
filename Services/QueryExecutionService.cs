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
using DTC.Core.UI;
using DTC.Core.Extensions;
using G33kSeek.Models;

namespace G33kSeek.Services;

/// <summary>
/// Executes the default action associated with a query result.
/// </summary>
/// <remarks>
/// This centralizes clipboard and open-item behavior so providers only need to describe what Enter should do.
/// </remarks>
public static class QueryExecutionService
{
    internal static Action<FileInfo> FileOpener { get; set; } = file => file.OpenWithDefaultViewer();
    internal static Action<DirectoryInfo> DirectoryOpener { get; set; } = directory => directory.Explore();
    internal static Action<FileInfo> FileRevealer { get; set; } = file => file.Explore();
    internal static Action<DirectoryInfo> DirectoryRevealer { get; set; } = directory => directory.Explore();
    internal static Action<Uri> UriOpener { get; set; } = uri => uri.Open();
    internal static Action<ProcessStartInfo> ProcessStarter { get; set; } = processStartInfo => Process.Start(processStartInfo);
    internal static Func<CancellationToken, Task<DirectoryInfo>> SearchRootPicker { get; set; } = PickSearchRootAsync;
    internal static Func<DirectoryInfo, CancellationToken, Task<FileSearchRootAddStatus>> SearchRootAdder { get; set; }
    internal static Func<CancellationToken, Task> IndexRefresher { get; set; }

    public static async Task<QueryExecutionResult> ExecuteAsync(
        QueryResult result,
        Window ownerWindow,
        CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));

        cancellationToken.ThrowIfCancellationRequested();

        if (result.PrimaryAction == null || result.PrimaryAction.Kind == QueryActionKind.None)
            return new QueryExecutionResult(false, "That result does not have an action.");

        var action = result.PrimaryAction;
        switch (action.Kind)
        {
            case QueryActionKind.CopyText:
                if (ownerWindow == null)
                    throw new ArgumentNullException(nameof(ownerWindow));

                var clipboard = TopLevel.GetTopLevel(ownerWindow)?.Clipboard;
                if (clipboard == null)
                    return new QueryExecutionResult(false, "Clipboard is not currently available.");

                await clipboard.SetTextAsync(action.Payload ?? string.Empty);
                return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);

            case QueryActionKind.OpenPath:
                if (File.Exists(action.Payload))
                {
                    FileOpener(new FileInfo(action.Payload));
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                if (Directory.Exists(action.Payload))
                {
                    DirectoryOpener(new DirectoryInfo(action.Payload));
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                return new QueryExecutionResult(false, "The target path no longer exists.");

            case QueryActionKind.RevealPath:
                if (File.Exists(action.Payload))
                {
                    FileRevealer(new FileInfo(action.Payload));
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                if (Directory.Exists(action.Payload))
                {
                    DirectoryRevealer(new DirectoryInfo(action.Payload));
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                return new QueryExecutionResult(false, "The target path no longer exists.");

            case QueryActionKind.OpenUri:
                if (Uri.TryCreate(action.Payload, UriKind.Absolute, out var uri))
                {
                    UriOpener(uri);
                    return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);
                }

                return new QueryExecutionResult(false, "The target URL was invalid.");

            case QueryActionKind.RunProcess:
                if (string.IsNullOrWhiteSpace(action.Payload))
                    return new QueryExecutionResult(false, "The target command was invalid.");

                ProcessStarter(
                    new ProcessStartInfo(action.Payload, action.Arguments ?? string.Empty)
                    {
                        UseShellExecute = false
                    });
                return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);

            case QueryActionKind.AddSearchRoot:
                if (SearchRootPicker == null || SearchRootAdder == null)
                    return new QueryExecutionResult(false, "Adding search folders is not available.");

                var selectedDirectory = await SearchRootPicker(cancellationToken);
                if (selectedDirectory == null)
                    return new QueryExecutionResult(false, "Folder selection cancelled.");

                var addStatus = await SearchRootAdder(selectedDirectory, cancellationToken);
                return addStatus switch
                {
                    FileSearchRootAddStatus.Added => new QueryExecutionResult(true, $"Added {selectedDirectory.FullName} to file search.", action.ShouldHideLauncher),
                    FileSearchRootAddStatus.AlreadyCovered => new QueryExecutionResult(true, $"{selectedDirectory.FullName} is already covered by file search.", action.ShouldHideLauncher),
                    FileSearchRootAddStatus.Unavailable => new QueryExecutionResult(false, $"{selectedDirectory.FullName} is no longer available."),
                    _ => new QueryExecutionResult(false, "Unable to add that folder to file search.")
                };

            case QueryActionKind.RefreshIndexes:
                if (IndexRefresher == null)
                    return new QueryExecutionResult(false, "Refreshing indexes is not available.");

                _ = RefreshIndexesInBackgroundAsync();
                return new QueryExecutionResult(true, action.SuccessMessage, action.ShouldHideLauncher);

            default:
                return new QueryExecutionResult(false, "That action type is not implemented yet.");
        }
    }

    private static async Task RefreshIndexesInBackgroundAsync()
    {
        try
        {
            await IndexRefresher(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected if the operation is canceled.
        }
        catch (Exception ex)
        {
            DTC.Core.Logger.Instance.Exception("Manual index refresh failed.", ex);
        }
    }

    private static async Task<DirectoryInfo> PickSearchRootAsync(CancellationToken cancellationToken)
    {
        var directory = await DialogService.Instance.SelectFolderAsync("Select folder to include in file search");
        cancellationToken.ThrowIfCancellationRequested();
        return directory?.Exists == true ? directory : null;
    }
}
