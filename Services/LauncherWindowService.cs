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
using System.Runtime.InteropServices;
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
    private static readonly TimeSpan[] WindowsActivationRetryDelays =
    [
        TimeSpan.FromMilliseconds(30),
        TimeSpan.FromMilliseconds(90),
        TimeSpan.FromMilliseconds(180)
    ];

    private readonly IClassicDesktopStyleApplicationLifetime m_desktop;
    private readonly bool m_isWindows;
    private readonly MainWindow m_mainWindow;

    public LauncherWindowService(
        IClassicDesktopStyleApplicationLifetime desktop,
        MainWindow mainWindow,
        bool isWindows = false)
    {
        m_desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
        m_mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        m_isWindows = isWindows || OperatingSystem.IsWindows();
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
        PulseTopmostOnWindows();
        ReassertActivation();
        Dispatcher.UIThread.Post(ReassertActivation, DispatcherPriority.Input);

        if (!m_isWindows)
            return;

        foreach (var retryDelay in WindowsActivationRetryDelays)
            DispatcherTimer.RunOnce(ReassertActivation, retryDelay);
    }

    private void HideCore()
    {
        if (m_mainWindow.IsVisible)
            m_mainWindow.Hide();
    }

    private void ForceForegroundOnWindows()
    {
        if (!m_isWindows)
            return;

        var platformHandle = m_mainWindow.TryGetPlatformHandle();
        if (platformHandle == null || platformHandle.Handle == IntPtr.Zero)
            return;

        ShowWindow(platformHandle.Handle, ShowWindowRestore);
        BringWindowToTop(platformHandle.Handle);
        SetForegroundWindow(platformHandle.Handle);
        SetActiveWindow(platformHandle.Handle);
        SetFocus(platformHandle.Handle);
    }

    private void ReassertActivation()
    {
        m_mainWindow.Activate();
        ForceForegroundOnWindows();
        m_mainWindow.PrepareForActivation();
    }

    private void PulseTopmostOnWindows()
    {
        if (!m_isWindows)
            return;

        m_mainWindow.Topmost = false;
        m_mainWindow.Topmost = true;
    }

    private const int ShowWindowRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);
}
