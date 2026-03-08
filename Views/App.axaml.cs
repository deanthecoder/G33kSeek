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
using G33kSeek.Providers;
using G33kSeek.Services;
using G33kSeek.ViewModels;

namespace G33kSeek.Views;

public class App : Application
{
    private GlobalHotkeyService m_globalHotkeyService;
    private TrayIconService m_trayIconService;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var queryEngine = new QueryEngine(
            [
                new CalculatorQueryProvider(),
                new PlaceholderQueryProvider("??", "Content search", "File content search will reuse the proven G33kShell grep path."),
                new PlaceholderQueryProvider("?", "Web search", "Browser query routing is planned after calculator mode."),
                new PlaceholderQueryProvider("@", "AI prompt", "AI provider integration comes after the local query engine."),
                new PlaceholderQueryProvider(">", "Commands", "Command routing will be added after the first providers settle."),
                new DefaultQueryProvider()
            ]);
            var queryExecutionService = new QueryExecutionService();
            var viewModel = new MainWindowViewModel(queryEngine);
            var launcherWindow = new MainWindow(viewModel, queryExecutionService);
            var launcherWindowService = new LauncherWindowService(desktop, launcherWindow);
            m_trayIconService = new TrayIconService(this, launcherWindowService, () => desktop.Shutdown());
            m_globalHotkeyService = new GlobalHotkeyService(launcherWindowService);

            desktop.Exit += Desktop_OnExit;

            m_trayIconService.Initialize();
            m_globalHotkeyService.Start();

#if DEBUG
            launcherWindowService.Show();
#endif
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Desktop_OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        m_globalHotkeyService?.Dispose();
        m_trayIconService?.Dispose();
    }
}
