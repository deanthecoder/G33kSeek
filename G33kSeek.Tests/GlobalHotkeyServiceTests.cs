// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using G33kSeek.Services;
using SharpHook.Data;

namespace G33kSeek.Tests;

public class GlobalHotkeyServiceTests
{
    [Test]
    public void IsToggleHotkeyUsesCtrlSpaceOnWindows()
    {
        var isMatch = GlobalHotkeyService.IsToggleHotkey(
            KeyCode.VcSpace,
            EventMask.LeftCtrl,
            isWindows: true);

        Assert.That(isMatch, Is.True);
    }

    [Test]
    public void IsToggleHotkeyRejectsWinSpaceOnWindows()
    {
        var isMatch = GlobalHotkeyService.IsToggleHotkey(
            KeyCode.VcSpace,
            EventMask.LeftMeta,
            isWindows: true);

        Assert.That(isMatch, Is.False);
    }

    [Test]
    public void IsToggleHotkeyUsesCtrlSpaceOffWindows()
    {
        var isMatch = GlobalHotkeyService.IsToggleHotkey(
            KeyCode.VcSpace,
            EventMask.LeftCtrl,
            isWindows: false);

        Assert.That(isMatch, Is.True);
    }

    [Test]
    public void GetShortcutDisplayTextReturnsPlatformSpecificLabel()
    {
        Assert.That(GlobalHotkeyService.GetShortcutDisplayText(isWindows: true), Is.EqualTo("Ctrl+Space"));
        Assert.That(GlobalHotkeyService.GetShortcutDisplayText(isWindows: false), Is.EqualTo("Ctrl+Space"));
    }
}
