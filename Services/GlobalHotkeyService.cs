using System;
using System.Threading.Tasks;
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

    private readonly LauncherWindowService m_launcherWindowService;
    private readonly TaskPoolGlobalHook m_hook;
    private DateTime m_lastToggleUtc;
    private Task m_hookRunTask;
    private bool m_isStarted;

    public GlobalHotkeyService(LauncherWindowService launcherWindowService)
    {
        m_launcherWindowService = launcherWindowService ?? throw new ArgumentNullException(nameof(launcherWindowService));
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
        m_hookRunTask = m_hook.RunAsync();
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

        if (e.Data.KeyCode != KeyCode.VcSpace)
            return;

        if (!e.RawEvent.Mask.HasCtrl())
            return;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - m_lastToggleUtc < TriggerCooldown)
            return;

        m_lastToggleUtc = nowUtc;
        m_launcherWindowService.Toggle();
    }
}
