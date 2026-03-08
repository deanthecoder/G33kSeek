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
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    private readonly Func<Guid> m_guidFactory;
    private readonly Func<IReadOnlyList<string>> m_ipAddressesAccessor;
    private readonly bool m_isMacOS;
    private readonly bool m_isWindows;

    public CommandQueryProvider()
        : this(OperatingSystem.IsMacOS(), OperatingSystem.IsWindows(), () => Guid.NewGuid(), GetLocalIpAddresses)
    {
    }

    internal CommandQueryProvider(bool isMacOS, bool isWindows, Func<Guid> guidFactory, Func<IReadOnlyList<string>> ipAddressesAccessor)
    {
        m_isMacOS = isMacOS;
        m_isWindows = isWindows;
        m_guidFactory = guidFactory ?? throw new ArgumentNullException(nameof(guidFactory));
        m_ipAddressesAccessor = ipAddressesAccessor ?? throw new ArgumentNullException(nameof(ipAddressesAccessor));
    }

    public string Prefix => ">";

    public QueryProviderHelpEntry HelpEntry =>
        new("Commands", "Use > to run built-in launcher commands.", ">shutdown");

    public Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();

        var query = request.ProviderQuery?.Trim() ?? string.Empty;
        var commands = GetCommands()
            .Where(command => Matches(query, command))
            .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreateResult)
            .ToArray();

        if (commands.Length == 0)
        {
            if (!m_isMacOS && !m_isWindows && string.IsNullOrWhiteSpace(query))
                return Task.FromResult(new QueryResponse([], "Only utility commands are available on this platform right now."));

            return Task.FromResult(new QueryResponse([], $"No commands matched \"{query}\"."));
        }

        var statusText = string.IsNullOrWhiteSpace(query)
            ? "Commands ready. Type to filter, or press Enter to run the selected command."
            : $"Found {commands.Length} command{(commands.Length == 1 ? string.Empty : "s")}. Press Enter to run the selected command.";
        return Task.FromResult(new QueryResponse(commands, statusText));
    }

    private static QueryResult CreateResult(CommandDefinition definition) =>
        definition.CreateResult();

    private QueryResult CreateGuidResult()
    {
        var guidText = m_guidFactory().ToString("D");
        return new QueryResult(
            "guid",
            guidText,
            "Value",
            new QueryActionDescriptor(
                QueryActionKind.CopyText,
                guidText,
                successMessage: "GUID copied."));
    }

    private QueryResult CreateIpResult()
    {
        var ipAddresses = m_ipAddressesAccessor()
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (ipAddresses.Length == 0)
            return new QueryResult("ip", "No local IPv4 address found.", "Value");

        var ipText = string.Join(", ", ipAddresses);
        return new QueryResult(
            "ip",
            ipText,
            "Value",
            new QueryActionDescriptor(
                QueryActionKind.CopyText,
                ipText,
                successMessage: "IP address copied."));
    }

    private static bool Matches(string query, CommandDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var normalizedQuery = query.Trim().ToLowerInvariant();
        return definition.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<CommandDefinition> GetCommands()
    {
        var commands = new List<CommandDefinition>
        {
            new("guid", "Generate a dashed GUID.", CreateGuidResult),
            new("ip", "Show local IPv4 addresses.", CreateIpResult)
        };

        if (m_isMacOS)
        {
            commands.Add(new CommandDefinition("shutdown", "Shut down the Mac.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to shut down\"", "Shutdown requested."));
            commands.Add(new CommandDefinition("restart", "Restart the Mac.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to restart\"", "Restart requested."));
            commands.Add(new CommandDefinition("logoff", "Log out the current user.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to log out\"", "Log out requested."));
        }
        else if (m_isWindows)
        {
            commands.Add(new CommandDefinition("shutdown", "Shut down Windows immediately.", "shutdown", "/s /t 0", "Shutdown requested."));
            commands.Add(new CommandDefinition("restart", "Restart Windows immediately.", "shutdown", "/r /t 0", "Restart requested."));
            commands.Add(new CommandDefinition("logoff", "Log off the current Windows session.", "shutdown", "/l", "Log off requested."));
        }

        return commands;
    }

    private sealed class CommandDefinition
    {
        public CommandDefinition(string name, string description, Func<QueryResult> createResult)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            CreateResult = createResult ?? throw new ArgumentNullException(nameof(createResult));
        }

        public CommandDefinition(string name, string description, string executable, string arguments, string successMessage)
            : this(
                name,
                description,
                () =>
                    new QueryResult(
                        name,
                        description,
                        "Command",
                        new QueryActionDescriptor(
                            QueryActionKind.RunProcess,
                            executable,
                            arguments: arguments,
                            successMessage: successMessage)))
        {
        }

        public string Name { get; }

        public string Description { get; }

        public Func<QueryResult> CreateResult { get; }
    }

    private static IReadOnlyList<string> GetLocalIpAddresses()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(networkInterface =>
                networkInterface.OperationalStatus == OperationalStatus.Up &&
                networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(networkInterface => networkInterface.GetIPProperties().UnicastAddresses)
            .Select(unicastAddress => unicastAddress.Address)
            .Where(address =>
                address.AddressFamily == AddressFamily.InterNetwork &&
                !IPAddress.IsLoopback(address))
            .Select(address => address.ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
