// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Collections.Generic;
using System.Linq;
using DTC.Core;
using G33kSeek.Providers;
using G33kSeek.Services;
using G33kSeek.ViewModels;
using G33kSeek.Models;
using QueryProvider = G33kSeek.Providers.IQueryProvider;

namespace G33kSeek.Views;

public class App : Application
{
    private FileSearchService m_fileSearchService;
    private GlobalHotkeyService m_globalHotkeyService;
    private TrayIconService m_trayIconService;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Logger.Instance.SysInfo();
            Logger.Instance.Info("Starting G33kSeek.");
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var applicationSearchService = new ApplicationSearchService();
            m_fileSearchService = new FileSearchService();
            var indexRefreshCoordinator = new IndexRefreshCoordinator(applicationSearchService, m_fileSearchService);

            List<QueryProvider> providers = [];
            var supplementalHelpEntries = new[]
            {
                new QueryProviderHelpEntry(
                    "Direct URLs",
                    "Typing http://, https://, or www. opens a URL directly.",
                    "https://github.com/deanthecoder"),
                new QueryProviderHelpEntry(
                    "Unit conversion",
                    "Type conversions like 10mb in bytes, 255 in hex, or 255 in binary with no prefix.",
                    "10mb in bytes"),
                new QueryProviderHelpEntry(
                    "File search",
                    "Documents are indexed in the background so no-prefix queries can find files quickly.",
                    "invoice"),
                new QueryProviderHelpEntry(
                    "Web search",
                    "Use ?\"search text\" to run a Google web search.",
                    "?\"avalonia docs\"")
            };
            IReadOnlyList<QueryProviderHelpEntry> GetHelpEntries() =>
                providers
                    .Select(provider => provider.HelpEntry)
                    .Concat(supplementalHelpEntries)
                    .ToArray();

            providers.Add(new DefaultQueryProvider(applicationSearchService, m_fileSearchService));
            providers.Add(new CalculatorQueryProvider());
            providers.Add(new HelpQueryProvider(GetHelpEntries));
            providers.Add(new PlaceholderQueryProvider("??", "Content search", "File content search will reuse the proven G33kShell grep path.", "??TODO"));
            providers.Add(new PlaceholderQueryProvider("@", "AI prompt", "AI provider integration comes after the local query engine.", "@summarise this text"));
            providers.Add(new CommandQueryProvider());
            QueryExecutionService.SearchRootAdder = m_fileSearchService.AddSearchRootAsync;
            QueryExecutionService.IndexRefresher = indexRefreshCoordinator.RefreshAllAsync;

            var queryEngine = new QueryEngine(providers);
            var viewModel = new MainWindowViewModel(queryEngine, indexRefreshCoordinator);
            var launcherWindow = new MainWindow(viewModel);
            var launcherWindowService = new LauncherWindowService(desktop, launcherWindow);
            m_trayIconService = new TrayIconService(this, launcherWindowService, () => desktop.Shutdown());
            m_globalHotkeyService = new GlobalHotkeyService(launcherWindowService);

            desktop.Exit += Desktop_OnExit;

            m_trayIconService.Initialize();
            m_globalHotkeyService.Start();
            _ = indexRefreshCoordinator.WarmAsync();

#if DEBUG
            launcherWindowService.Show();
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Desktop_OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        m_globalHotkeyService?.Dispose();
        m_fileSearchService?.Dispose();
        m_trayIconService?.Dispose();
    }
}
