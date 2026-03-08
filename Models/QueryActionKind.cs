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
/// Describes the default action to perform when a query result is executed.
/// </summary>
/// <remarks>
/// Different providers can return either a copy-style result, an opener result, or a no-op informational row.
/// </remarks>
public enum QueryActionKind
{
    None,
    CopyText,
    OpenPath,
    OpenUri,
    RunProcess
}
