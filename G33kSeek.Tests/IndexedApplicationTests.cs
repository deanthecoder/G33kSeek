// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core.JsonConverters;
using G33kSeek.Models;
using Newtonsoft.Json;

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

    [Test]
    public void CreatePrimaryActionReturnsOpenUriForWindowsSettingsApplications()
    {
        var application = new IndexedApplication
        {
            DisplayName = "Add or remove programs",
            LaunchKind = ApplicationLaunchKind.OpenUri,
            LaunchUri = "ms-settings:appsfeatures"
        };

        var action = application.CreatePrimaryAction();

        Assert.That(action.Kind, Is.EqualTo(QueryActionKind.OpenUri));
        Assert.That(action.Payload, Is.EqualTo("ms-settings:appsfeatures"));
        Assert.That(application.Subtitle, Is.EqualTo("ms-settings:appsfeatures"));
    }

    [Test]
    public void SerializationOmitsComputedLaunchProperties()
    {
        var application = new IndexedApplication
        {
            DisplayName = "Rider",
            SearchName = "rider",
            LaunchKind = ApplicationLaunchKind.OpenPath,
            ShortcutFile = new FileInfo(@"C:\Apps\Rider.lnk")
        };

        var json = JsonConvert.SerializeObject(
            application,
            new JsonSerializerSettings
            {
                Converters =
                [
                    new FileInfoConverter(),
                    new DirectoryInfoConverter()
                ]
            });

        Assert.That(json, Does.Not.Contain("LaunchPath"));
        Assert.That(json, Does.Not.Contain("Subtitle"));
        Assert.That(json, Does.Contain("ShortcutFile"));
    }
}
