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
using System.Threading;
using System.Runtime.Versioning;
using DTC.Core;
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
    private const int WindowsHotkeyId = 0x5333;
    private const uint WmHotKey = 0x0312;
    private const uint WmQuit = 0x0012;
    private const uint PmRemove = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkSpace = 0x20;
    private readonly bool m_isWindows;

    private readonly LauncherWindowService m_launcherWindowService;
    private readonly TaskPoolGlobalHook m_hook;
    private readonly AutoResetEvent m_windowsHotkeyThreadReady = new(false);
    private DateTime m_lastToggleUtc;
    private Thread m_windowsHotkeyThread;
    private uint m_windowsHotkeyThreadId;
    private bool m_windowsHotkeyRegistered;
    private string m_registeredWindowsShortcut = "Ctrl+Space";
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

        if (m_isWindows)
        {
            StartWindowsHotkeyLoop();
            m_isStarted = true;
            return;
        }

        m_hook.KeyPressed += Hook_OnKeyPressed;
        m_hook.RunAsync();
        m_isStarted = true;
    }

    public void Dispose()
    {
        if (!m_isStarted)
            return;

        if (m_isWindows)
            StopWindowsHotkeyLoop();
        else
        {
            m_hook.KeyPressed -= Hook_OnKeyPressed;
            m_hook.Dispose();
        }

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

    private void StartWindowsHotkeyLoop()
    {
        m_windowsHotkeyThread = new Thread(WindowsHotkeyThreadMain)
        {
            IsBackground = true,
            Name = "G33kSeek Windows hotkey"
        };
#pragma warning disable CA1416
        ConfigureWindowsHotkeyThread(m_windowsHotkeyThread);
#pragma warning restore CA1416
        m_windowsHotkeyThread.Start();
        m_windowsHotkeyThreadReady.WaitOne(TimeSpan.FromSeconds(5));
    }

    private void StopWindowsHotkeyLoop()
    {
        if (m_windowsHotkeyThreadId != 0)
            PostThreadMessage(m_windowsHotkeyThreadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);

        if (m_windowsHotkeyThread != null && m_windowsHotkeyThread.IsAlive)
            m_windowsHotkeyThread.Join(TimeSpan.FromSeconds(2));

        m_windowsHotkeyThread = null;
        m_windowsHotkeyThreadId = 0;
        m_windowsHotkeyRegistered = false;
    }

    private void WindowsHotkeyThreadMain()
    {
        m_windowsHotkeyThreadId = GetCurrentThreadId();
        EnsureMessageQueueExists();
        m_windowsHotkeyRegistered = RegisterWindowsHotkey();
        m_windowsHotkeyThreadReady.Set();

        if (!m_windowsHotkeyRegistered)
            return;

        try
        {
            while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
            {
                if (message.message != WmHotKey || (int)message.wParam != WindowsHotkeyId)
                    continue;

                Logger.Instance.Info($"Windows hotkey WM_HOTKEY received for {m_registeredWindowsShortcut}.");
                m_launcherWindowService.Toggle();
            }
        }
        finally
        {
            UnregisterHotKey(IntPtr.Zero, WindowsHotkeyId);
            m_windowsHotkeyRegistered = false;
        }
    }

    private static void EnsureMessageQueueExists()
    {
        PeekMessage(out _, IntPtr.Zero, 0, 0, PmRemove);
    }

    private bool RegisterWindowsHotkey()
    {
        if (TryRegisterWindowsHotkey(ModControl | ModNoRepeat, "Ctrl+Space"))
            return true;

        var ctrlError = Marshal.GetLastWin32Error();
        Logger.Instance.Warn($"Windows hotkey registration for Ctrl+Space failed. Win32={ctrlError}.");
        return false;
    }

    private bool TryRegisterWindowsHotkey(uint modifiers, string shortcutDisplayText)
    {
        if (!RegisterHotKey(IntPtr.Zero, WindowsHotkeyId, modifiers, VkSpace))
            return false;

        m_registeredWindowsShortcut = shortcutDisplayText;
        Logger.Instance.Info($"Windows hotkey registration for {shortcutDisplayText} succeeded.");
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static void ConfigureWindowsHotkeyThread(Thread thread)
    {
        thread.SetApartmentState(ApartmentState.STA);
    }

    internal static bool IsToggleHotkey(KeyCode keyCode, EventMask modifierMask, bool isWindows)
    {
        if (keyCode != KeyCode.VcSpace)
            return false;

        return modifierMask.HasCtrl();
    }

    internal static string GetShortcutDisplayText(bool isWindows) =>
        "Ctrl+Space";

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr hWnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int ptX;
        public int ptY;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out Message lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out Message lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, UIntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
