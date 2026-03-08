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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using G33kSeek.Models;

namespace G33kSeek.Providers;

/// <summary>
/// Provides built-in launcher commands under the <c>&gt;</c> prefix.
/// </summary>
/// <remarks>
/// This gives the launcher a small set of immediately useful system commands without needing a separate shell integration layer.
/// </remarks>
public sealed class CommandQueryProvider : IQueryProvider
{
    private readonly bool m_isMacOS;
    private readonly bool m_isWindows;

    public CommandQueryProvider() : this(OperatingSystem.IsMacOS(), OperatingSystem.IsWindows())
    {
    }

    internal CommandQueryProvider(bool isMacOS, bool isWindows)
    {
        m_isMacOS = isMacOS;
        m_isWindows = isWindows;
    }

    public string Prefix => ">";

    public QueryProviderHelpEntry HelpEntry =>
        new("Commands", "Use > to run built-in launcher commands.", ">shutdown");

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();

        if (!m_isMacOS && !m_isWindows)
            return Task.FromResult(new QueryResponse([], "Commands are not supported on this platform yet."));

        var query = request.ProviderQuery?.Trim() ?? string.Empty;
        var commands = GetCommands()
            .Where(command => Matches(query, command))
            .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreateResult)
            .ToArray();

        if (commands.Length == 0)
            return Task.FromResult(new QueryResponse([], $"No commands matched \"{query}\"."));

        var statusText = string.IsNullOrWhiteSpace(query)
            ? "Commands ready. Type to filter, or press Enter to run the selected command."
            : $"Found {commands.Length} command{(commands.Length == 1 ? string.Empty : "s")}. Press Enter to run the selected command.";
        return Task.FromResult(new QueryResponse(commands, statusText));
    }

    private static QueryResult CreateResult(CommandDefinition definition)
    {
        return new QueryResult(
            definition.Name,
            definition.Description,
            "Command",
            new QueryActionDescriptor(
                QueryActionKind.RunProcess,
                definition.Executable,
                arguments: definition.Arguments,
                successMessage: definition.SuccessMessage));
    }

    private static bool Matches(string query, CommandDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var normalizedQuery = query.Trim().ToLowerInvariant();
        return definition.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
               definition.Description.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<CommandDefinition> GetCommands()
    {
        if (m_isMacOS)
        {
            return
            [
                new CommandDefinition("shutdown", "Shut down the Mac.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to shut down\"", "Shutdown requested."),
                new CommandDefinition("restart", "Restart the Mac.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to restart\"", "Restart requested."),
                new CommandDefinition("logoff", "Log out the current user.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to log out\"", "Log out requested.")
            ];
        }

        if (m_isWindows)
        {
            return
            [
                new CommandDefinition("shutdown", "Shut down Windows immediately.", "shutdown", "/s /t 0", "Shutdown requested."),
                new CommandDefinition("restart", "Restart Windows immediately.", "shutdown", "/r /t 0", "Restart requested."),
                new CommandDefinition("logoff", "Log off the current Windows session.", "shutdown", "/l", "Log off requested.")
            ];
        }

        return [];
    }

    private sealed class CommandDefinition
    {
        public CommandDefinition(string name, string description, string executable, string arguments, string successMessage)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Executable = executable ?? throw new ArgumentNullException(nameof(executable));
            Arguments = arguments ?? string.Empty;
            SuccessMessage = successMessage ?? string.Empty;
        }

        public string Name { get; }

        public string Description { get; }

        public string Executable { get; }

        public string Arguments { get; }

        public string SuccessMessage { get; }
    }
}
