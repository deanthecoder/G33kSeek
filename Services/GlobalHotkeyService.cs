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
using SharpHook;
using SharpHook.Data;

namespace G33kSeek.Services;

/// <summary>
/// Listens for a global hotkey to toggle the launcher window.
/// </summary>
/// <remarks>
/// This uses SharpHook so the same first-cut experience works across desktop platforms.
/// </remarks>
public sealed class GlobalHotkeyService : IDisposable
{
    private static readonly TimeSpan TriggerCooldown = TimeSpan.FromMilliseconds(250);
    private readonly bool m_isWindows;

    private readonly LauncherWindowService m_launcherWindowService;
    private readonly TaskPoolGlobalHook m_hook;
    private DateTime m_lastToggleUtc;
    private bool m_isStarted;

    public GlobalHotkeyService(LauncherWindowService launcherWindowService, bool isWindows = false)
    {
        m_launcherWindowService = launcherWindowService ?? throw new ArgumentNullException(nameof(launcherWindowService));
        m_isWindows = isWindows || OperatingSystem.IsWindows();
        m_hook = new TaskPoolGlobalHook(
            parallelismLevel: 1,
            globalHookType: GlobalHookType.Keyboard,
            globalHookProvider: null,
            runAsyncOnBackgroundThread: true);
    }

    public void Start()
    {
        if (m_isStarted)
            return;

        m_hook.KeyPressed += Hook_OnKeyPressed;
        m_hook.RunAsync();
        m_isStarted = true;
    }

    public void Dispose()
    {
        if (!m_isStarted)
            return;

        m_hook.KeyPressed -= Hook_OnKeyPressed;
        m_hook.Dispose();
        m_isStarted = false;
    }

    private void Hook_OnKeyPressed(object sender, KeyboardHookEventArgs e)
    {
        if (e == null)
            return;

        if (!IsToggleHotkey(e.Data.KeyCode, e.RawEvent.Mask, m_isWindows))
            return;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - m_lastToggleUtc < TriggerCooldown)
            return;

        m_lastToggleUtc = nowUtc;
        m_launcherWindowService.Toggle();
    }

    internal static bool IsToggleHotkey(KeyCode keyCode, EventMask modifierMask, bool isWindows)
    {
        if (keyCode != KeyCode.VcSpace)
            return false;

        return isWindows ? modifierMask.HasAlt() : modifierMask.HasCtrl();
    }

    internal static string GetShortcutDisplayText(bool isWindows) =>
        isWindows ? "Alt+Space" : "Ctrl+Space";
}
