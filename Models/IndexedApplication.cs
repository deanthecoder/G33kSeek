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
using Newtonsoft.Json;

namespace G33kSeek.Models;

/// <summary>
/// Represents a cached application entry that can be searched and launched.
/// </summary>
/// <remarks>
/// Used by application search so queries can be answered from a lightweight in-memory index.
/// </remarks>
internal sealed class IndexedApplication
{
    public string DisplayName { get; set; } = string.Empty;

    public string SearchName { get; set; } = string.Empty;

    public ApplicationLaunchKind LaunchKind { get; set; }

    public string AppUserModelId { get; set; } = string.Empty;

    public DirectoryInfo BundleDirectory { get; set; }

    public FileInfo ShortcutFile { get; set; }

    [JsonIgnore]
    public string LaunchPath => BundleDirectory?.FullName ?? ShortcutFile?.FullName ?? string.Empty;

    [JsonIgnore]
    public string Subtitle => ResolveLaunchKind() == ApplicationLaunchKind.WindowsShellApp ? AppUserModelId : LaunchPath;

    public QueryActionDescriptor CreatePrimaryAction()
    {
        return ResolveLaunchKind() switch
        {
            ApplicationLaunchKind.OpenPath => new QueryActionDescriptor(
                QueryActionKind.OpenPath,
                LaunchPath,
                successMessage: $"Launching {DisplayName}."),
            ApplicationLaunchKind.WindowsShellApp => new QueryActionDescriptor(
                QueryActionKind.RunProcess,
                "explorer.exe",
                arguments: $"\"shell:AppsFolder\\{AppUserModelId}\"",
                successMessage: $"Launching {DisplayName}."),
            _ => throw new InvalidOperationException($"Indexed application '{DisplayName}' does not have a valid launch target.")
        };
    }

    private ApplicationLaunchKind ResolveLaunchKind()
    {
        if (LaunchKind != ApplicationLaunchKind.Auto)
            return LaunchKind;

        if (!string.IsNullOrWhiteSpace(LaunchPath))
            return ApplicationLaunchKind.OpenPath;

        if (!string.IsNullOrWhiteSpace(AppUserModelId))
            return ApplicationLaunchKind.WindowsShellApp;

        return ApplicationLaunchKind.Auto;
    }
}
