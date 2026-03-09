// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Reflection;
using DTC.Core.Extensions;
using DTC.Core.Settings;

namespace G33kSeek.Tests;

public class UserSettingsBaseTests
{
    [Test]
    public void GetConvertsInt64SettingValuesToIntProperties()
    {
        var settingsFile = Assembly.GetEntryAssembly().GetAppSettingsPath().GetFile("settings.json");
        var originalContent = settingsFile.Exists() ? settingsFile.ReadAllText() : null;

        try
        {
            settingsFile.WriteAllText("{\"CacheFormatVersion\":2}");

            using var settings = new TestSettings();

            Assert.That(settings.CacheFormatVersion, Is.EqualTo(2));
        }
        finally
        {
            if (originalContent == null)
                settingsFile.TryDelete();
            else
                settingsFile.WriteAllText(originalContent);
        }
    }

    private sealed class TestSettings : UserSettingsBase
    {
        public int CacheFormatVersion => Get<int>();

        protected override void ApplyDefaults()
        {
        }
    }
}
