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
