// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using G33kSeek.Models;

namespace G33kSeek.Tests;

public class IndexedApplicationTests
{
    [Test]
    public void CreatePrimaryActionReturnsOpenPathForShortcutApplications()
    {
        var shortcutFile = new FileInfo(@"C:\Apps\Rider.lnk");
        var application = new IndexedApplication
        {
            DisplayName = "Rider",
            LaunchKind = ApplicationLaunchKind.OpenPath,
            ShortcutFile = shortcutFile
        };

        var action = application.CreatePrimaryAction();

        Assert.That(action.Kind, Is.EqualTo(QueryActionKind.OpenPath));
        Assert.That(action.Payload, Is.EqualTo(shortcutFile.FullName));
        Assert.That(application.Subtitle, Is.EqualTo(shortcutFile.FullName));
    }

    [Test]
    public void CreatePrimaryActionReturnsRunProcessForWindowsShellApplications()
    {
        var application = new IndexedApplication
        {
            DisplayName = "Company Portal",
            LaunchKind = ApplicationLaunchKind.WindowsShellApp,
            AppUserModelId = "Microsoft.CompanyPortal_8wekyb3d8bbwe!App"
        };

        var action = application.CreatePrimaryAction();

        Assert.That(action.Kind, Is.EqualTo(QueryActionKind.RunProcess));
        Assert.That(action.Payload, Is.EqualTo("explorer.exe"));
        Assert.That(action.Arguments, Is.EqualTo("\"shell:AppsFolder\\Microsoft.CompanyPortal_8wekyb3d8bbwe!App\""));
        Assert.That(application.Subtitle, Is.EqualTo("Microsoft.CompanyPortal_8wekyb3d8bbwe!App"));
    }
}
