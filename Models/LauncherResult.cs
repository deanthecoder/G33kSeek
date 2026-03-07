namespace G33kSeek.Models;

/// <summary>
/// Represents a single launcher result row in the prototype UI.
/// </summary>
/// <remarks>
/// This gives the UI something realistic to render before the real provider pipeline is connected.
/// </remarks>
public sealed record LauncherResult(string Title, string Subtitle, string PrefixHint);
