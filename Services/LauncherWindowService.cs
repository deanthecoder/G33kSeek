using System;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using G33kSeek.Views;

namespace G33kSeek.Services;

/// <summary>
/// Controls showing and hiding the launcher window from non-UI entry points.
/// </summary>
/// <remarks>
/// The tray icon and global hotkey both use this shared service so window behavior stays consistent.
/// </remarks>
public sealed class LauncherWindowService
{
    private readonly IClassicDesktopStyleApplicationLifetime m_desktop;
    private readonly MainWindow m_mainWindow;

    public LauncherWindowService(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow)
    {
        m_desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        m_mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
    }

    public void Toggle()
    {
        Dispatcher.UIThread.Post(
            () =>
            {
                if (m_mainWindow.IsVisible)
                {
                    HideCore();
                    return;
                }

                ShowCore();
            });
    }

    public void Show() =>
        Dispatcher.UIThread.Post(ShowCore);

    public void Hide() =>
        Dispatcher.UIThread.Post(HideCore);

    private void ShowCore()
    {
        if (m_desktop.MainWindow != m_mainWindow)
            m_desktop.MainWindow = m_mainWindow;

        if (!m_mainWindow.IsVisible)
            m_mainWindow.Show();

        m_mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
        m_mainWindow.Activate();
        m_mainWindow.PrepareForActivation();
    }

    private void HideCore()
    {
        if (m_mainWindow.IsVisible)
            m_mainWindow.Hide();
    }
}
