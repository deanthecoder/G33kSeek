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
/// Describes the action to take when a query result is activated.
/// </summary>
/// <remarks>
/// This keeps result rendering separate from execution so providers can return values, lists, or openable items consistently.
/// </remarks>
public sealed class QueryActionDescriptor
{
    public QueryActionDescriptor(
        QueryActionKind kind,
        string payload = null,
        string arguments = null,
        string successMessage = null,
        bool shouldHideLauncher = true)
    {
        Kind = kind;
        Payload = payload;
        Arguments = arguments;
        SuccessMessage = successMessage;
        ShouldHideLauncher = shouldHideLauncher;
    }

    public QueryActionKind Kind { get; }

    public string Payload { get; }

    public string Arguments { get; }

    public string SuccessMessage { get; }

    public bool ShouldHideLauncher { get; }
}
