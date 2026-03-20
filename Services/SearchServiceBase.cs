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
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace G33kSeek.Services;

/// <summary>
/// Provides shared refresh-state and watcher helpers for cached search services.
/// </summary>
/// <remarks>
/// Application and file search both maintain background refresh state, watcher maps, and normalized root-path checks.
/// This base keeps that plumbing in one place without changing either service's search-specific logic.
/// </remarks>
internal abstract class SearchServiceBase : IDisposable
{
    protected readonly SemaphoreSlim m_refreshLock = new(1, 1);

    internal bool IsRefreshing { get; private set; }

    internal event EventHandler RefreshStateChanged;

    public virtual void Dispose()
    {
        m_refreshLock.Dispose();
    }

    protected void SetIsRefreshing(bool isRefreshing)
    {
        if (IsRefreshing == isRefreshing)
            return;

        IsRefreshing = isRefreshing;
        RefreshStateChanged?.Invoke(this, EventArgs.Empty);
    }

    protected static void DisposeWatchers<TState>(
        IDictionary<string, TState> rootWatchStates,
        Func<TState, FileSystemWatcher> watcherAccessor)
    {
        ArgumentNullException.ThrowIfNull(rootWatchStates);
        ArgumentNullException.ThrowIfNull(watcherAccessor);

        foreach (var rootWatchState in rootWatchStates.Values)
            watcherAccessor(rootWatchState)?.Dispose();

        rootWatchStates.Clear();
    }

    protected static FileSystemWatcher CreateWatcher(
        DirectoryInfo rootDirectory,
        int watcherBufferSize,
        Action<string> pathDirtyHandler,
        Action<Exception> errorHandler)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        ArgumentNullException.ThrowIfNull(pathDirtyHandler);
        ArgumentNullException.ThrowIfNull(errorHandler);

        var watcher = new FileSystemWatcher(rootDirectory.FullName)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = watcherBufferSize,
            NotifyFilter = NotifyFilters.DirectoryName |
                           NotifyFilters.FileName
        };

        watcher.Created += (_, args) => pathDirtyHandler(args.FullPath);
        watcher.Deleted += (_, args) => pathDirtyHandler(args.FullPath);
        watcher.Renamed += (_, args) =>
        {
            pathDirtyHandler(args.OldFullPath);
            pathDirtyHandler(args.FullPath);
        };
        watcher.Error += (_, args) => errorHandler(args.GetException());
        watcher.EnableRaisingEvents = true;
        return watcher;
    }

    protected static bool IsPathCoveredByRoot(string candidatePath, string rootPath)
    {
        var normalizedCandidatePath = NormalizeRootPath(candidatePath);
        var normalizedRootPath = NormalizeRootPath(rootPath);

        return normalizedCandidatePath.Equals(normalizedRootPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedCandidatePath.StartsWith($"{normalizedRootPath}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    protected static string NormalizeRootPath(string path)
    {
        return Path.GetFullPath(path ?? string.Empty)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
