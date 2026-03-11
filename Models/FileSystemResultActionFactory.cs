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
using System.Collections.Generic;

namespace G33kSeek.Models;

/// <summary>
/// Builds common secondary actions for file-system backed query results.
/// </summary>
/// <remarks>
/// This keeps reveal and copy-path actions consistent across app, file, folder, and direct-path results.
/// </remarks>
internal static class FileSystemResultActionFactory
{
    public static IReadOnlyList<QueryActionDescriptor> CreateSecondaryActions(string displayName, string fullPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        return
        [
            new QueryActionDescriptor(
                QueryActionKind.RevealPath,
                fullPath,
                successMessage: $"Revealing {displayName}.",
                displayText: OperatingSystem.IsWindows() ? "Reveal in Explorer" : "Reveal in Finder"),
            new QueryActionDescriptor(
                QueryActionKind.CopyText,
                fullPath,
                successMessage: $"Copied path for {displayName}.",
                shouldHideLauncher: false,
                displayText: "Copy path")
        ];
    }
}
