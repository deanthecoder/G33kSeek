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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DTC.Core;
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
    private readonly Func<CommonFolderTargets> m_commonFolderTargetsAccessor;
    private readonly Func<Guid> m_guidFactory;
    private readonly Func<IReadOnlyList<string>> m_ipAddressesAccessor;
    private readonly Func<FileInfo> m_logFileAccessor;
    private readonly bool m_isMacOS;
    private readonly bool m_isWindows;

    public CommandQueryProvider()
        : this(OperatingSystem.IsMacOS(), OperatingSystem.IsWindows(), Guid.NewGuid, GetLocalIpAddresses, GetCommonFolderTargets, () => Logger.Instance.File)
    {
    }

    internal CommandQueryProvider(
        bool isMacOS,
        bool isWindows,
        Func<Guid> guidFactory,
        Func<IReadOnlyList<string>> ipAddressesAccessor,
        Func<CommonFolderTargets> commonFolderTargetsAccessor,
        Func<FileInfo> logFileAccessor)
    {
        m_isMacOS = isMacOS;
        m_isWindows = isWindows;
        m_guidFactory = guidFactory ?? throw new ArgumentNullException(nameof(guidFactory));
        m_ipAddressesAccessor = ipAddressesAccessor ?? throw new ArgumentNullException(nameof(ipAddressesAccessor));
        m_commonFolderTargetsAccessor = commonFolderTargetsAccessor ?? throw new ArgumentNullException(nameof(commonFolderTargetsAccessor));
        m_logFileAccessor = logFileAccessor ?? throw new ArgumentNullException(nameof(logFileAccessor));
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
        var matchingCommands = GetCommands()
            .Where(command => Matches(query, command))
            .ToArray();
        var exactMatch = matchingCommands.FirstOrDefault(
            command => command.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
        var commands = (exactMatch == null ? matchingCommands : [exactMatch])
            .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CreateQueryResult)
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

    private static QueryResult CreateQueryResult(CommandDefinition definition) =>
        definition.ResultFactory();

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

    private QueryResult CreateLogResult()
    {
        var logFile = m_logFileAccessor();
        if (logFile?.Exists != true)
            return new QueryResult("log", "No log file is available yet.", "File");

        return new QueryResult(
            "log",
            logFile.FullName,
            "File",
            new QueryActionDescriptor(
                QueryActionKind.OpenPath,
                logFile.FullName,
                successMessage: "Opening log."));
    }

    private IReadOnlyList<CommandDefinition> CreateFolderCommands()
    {
        var targets = m_commonFolderTargetsAccessor();
        var commands = new List<CommandDefinition>();
        AddFolderCommand(commands, "desktop", "Open the Desktop folder.", targets.DesktopDirectory);
        AddFolderCommand(commands, "documents", "Open the Documents folder.", targets.DocumentsDirectory);
        AddFolderCommand(commands, "downloads", "Open the Downloads folder.", targets.DownloadsDirectory);
        AddFolderCommand(commands, "home", "Open the home folder.", targets.HomeDirectory);
        return commands;
    }

    private static bool Matches(string query, CommandDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var normalizedQuery = query.Trim().ToLowerInvariant();
        if (definition.Name.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            return true;

        return normalizedQuery.Length >= 3 &&
               definition.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<CommandDefinition> GetCommands()
    {
        var commands = CreateFolderCommands().ToList();
        commands.Add(new CommandDefinition("addfolder", "Select a folder to include in file search.", CreateAddFolderResult));
        commands.Add(new CommandDefinition("guid", "Generate a dashed GUID.", CreateGuidResult));
        commands.Add(new CommandDefinition("ip", "Show local IPv4 addresses.", CreateIpResult));
        commands.Add(new CommandDefinition("log", "Open the launcher log file.", CreateLogResult));
        commands.Add(new CommandDefinition("exit", "Quit G33kSeek.", CreateExitResult));
        commands.Add(new CommandDefinition("refresh", "Refresh the app and file indexes now.", CreateRefreshResult));

        if (m_isMacOS)
        {
            commands.Add(new CommandDefinition("lock", "Lock the Mac.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to keystroke \\\"q\\\" using {control down, command down}\"", "Lock requested."));
            commands.Add(new CommandDefinition("shutdown", "Shut down the Mac.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to shut down\"", "Shutdown requested."));
            commands.Add(new CommandDefinition("restart", "Restart the Mac.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to restart\"", "Restart requested."));
            commands.Add(new CommandDefinition("logoff", "Log out the current user.", "/usr/bin/osascript", "-e \"tell application \\\"System Events\\\" to log out\"", "Log out requested."));
        }
        else if (m_isWindows)
        {
            commands.Add(new CommandDefinition("lock", "Lock the current Windows session.", "rundll32.exe", "user32.dll,LockWorkStation", "Lock requested."));
            commands.Add(new CommandDefinition("shutdown", "Shut down Windows immediately.", "shutdown", "/s /t 0", "Shutdown requested."));
            commands.Add(new CommandDefinition("restart", "Restart Windows immediately.", "shutdown", "/r /t 0", "Restart requested."));
            commands.Add(new CommandDefinition("logoff", "Log off the current Windows session.", "shutdown", "/l", "Log off requested."));
        }

        return commands;
    }

    private static QueryResult CreateAddFolderResult()
    {
        return new QueryResult(
            "addfolder",
            "Select a folder to include in file search.",
            "Command",
            new QueryActionDescriptor(
                QueryActionKind.AddSearchRoot,
                successMessage: "Search folder updated."));
    }

    private static QueryResult CreateRefreshResult()
    {
        return new QueryResult(
            "refresh",
            "Refresh the app and file indexes now.",
            "Command",
            new QueryActionDescriptor(
                QueryActionKind.RefreshIndexes,
                successMessage: "Refreshing app and file indexes.",
                shouldHideLauncher: false));
    }

    private static QueryResult CreateExitResult()
    {
        return new QueryResult(
            "exit",
            "Quit G33kSeek.",
            "Command",
            new QueryActionDescriptor(
                QueryActionKind.ExitApp,
                successMessage: "Exiting G33kSeek."));
    }

    private static void AddFolderCommand(List<CommandDefinition> commands, string name, string description, DirectoryInfo directory)
    {
        if (commands == null)
            throw new ArgumentNullException(nameof(commands));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(description));
        if (directory?.Exists != true)
            return;

        commands.Add(
            new CommandDefinition(
                name,
                description,
                () =>
                    new QueryResult(
                        name,
                        directory.FullName,
                        "Folder",
                        new QueryActionDescriptor(
                            QueryActionKind.OpenPath,
                            directory.FullName,
                            successMessage: $"Opening {name}."))));
    }

    private sealed class CommandDefinition
    {
        public CommandDefinition(string name, string description, Func<QueryResult> createResult)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _ = description ?? throw new ArgumentNullException(nameof(description));
            ResultFactory = createResult ?? throw new ArgumentNullException(nameof(createResult));
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

        public Func<QueryResult> ResultFactory { get; }
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

    private static CommonFolderTargets GetCommonFolderTargets()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        return new CommonFolderTargets(
            ToDirectoryInfo(homePath),
            ToDirectoryInfo(desktopPath),
            ToDirectoryInfo(documentsPath),
            ToDirectoryInfo(string.IsNullOrWhiteSpace(homePath) ? null : Path.Combine(homePath, "Downloads")));
    }

    private static DirectoryInfo ToDirectoryInfo(string path) =>
        string.IsNullOrWhiteSpace(path) ? null : new DirectoryInfo(path);

    internal sealed class CommonFolderTargets
    {
        public CommonFolderTargets(
            DirectoryInfo homeDirectory,
            DirectoryInfo desktopDirectory,
            DirectoryInfo documentsDirectory,
            DirectoryInfo downloadsDirectory)
        {
            HomeDirectory = homeDirectory;
            DesktopDirectory = desktopDirectory;
            DocumentsDirectory = documentsDirectory;
            DownloadsDirectory = downloadsDirectory;
        }

        public DirectoryInfo HomeDirectory { get; }

        public DirectoryInfo DesktopDirectory { get; }

        public DirectoryInfo DocumentsDirectory { get; }

        public DirectoryInfo DownloadsDirectory { get; }
    }
}
