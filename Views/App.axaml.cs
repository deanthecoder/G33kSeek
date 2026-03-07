using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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

            var viewModel = new MainWindowViewModel();
            var launcherWindow = new MainWindow(viewModel);
            var launcherWindowService = new LauncherWindowService(desktop, launcherWindow);
            m_trayIconService = new TrayIconService(this, launcherWindowService, () => desktop.Shutdown());
            m_globalHotkeyService = new GlobalHotkeyService(launcherWindowService);

            desktop.Exit += Desktop_OnExit;

            m_trayIconService.Initialize();
            m_globalHotkeyService.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Desktop_OnExit(object sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        m_globalHotkeyService?.Dispose();
        m_trayIconService?.Dispose();
    }
}
