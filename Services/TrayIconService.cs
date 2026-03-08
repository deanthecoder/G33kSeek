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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using DTC.Core.Commands;

namespace G33kSeek.Services;

/// <summary>
/// Creates the tray icon and its menu for quick launcher access.
/// </summary>
/// <remarks>
/// The tray keeps the app available even when the launcher window is hidden between searches.
/// </remarks>
public sealed class TrayIconService : IDisposable
{
    private readonly Application m_application;
    private readonly LauncherWindowService m_launcherWindowService;
    private readonly Action m_exitApplication;
    private TrayIcon m_trayIcon;

    public TrayIconService(
        Application application,
        LauncherWindowService launcherWindowService,
        Action exitApplication)
    {
        m_application = application ?? throw new ArgumentNullException(nameof(application));
        m_launcherWindowService = launcherWindowService ?? throw new ArgumentNullException(nameof(launcherWindowService));
        m_exitApplication = exitApplication ?? throw new ArgumentNullException(nameof(exitApplication));
    }

    public void Initialize()
    {
        var icon = LoadIcon();
        var menu = new NativeMenu
        {
            new NativeMenuItem("Exit")
            {
                Command = new RelayCommand(_ => m_exitApplication())
            }
        };

        m_trayIcon = new TrayIcon
        {
            Icon = icon,
            IsVisible = true,
            ToolTipText = "G33kSeek",
            Menu = menu
        };
        m_trayIcon.Clicked += TrayIcon_OnClicked;

        TrayIcon.SetIcons(m_application, new TrayIcons
        {
            m_trayIcon
        });
    }

    public void Dispose()
    {
        if (m_trayIcon != null)
        {
            m_trayIcon.Clicked -= TrayIcon_OnClicked;
            m_trayIcon.Dispose();
            m_trayIcon = null;
        }

        TrayIcon.SetIcons(m_application, null);
    }

    private void TrayIcon_OnClicked(object sender, EventArgs e) =>
        m_launcherWindowService.Toggle();

    private static WindowIcon LoadIcon()
    {
        var loader = AssetLoader.Open(new Uri("avares://G33kSeek/Assets/app.ico"));
        return new WindowIcon(loader);
    }
}
