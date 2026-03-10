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

    [Test]
    public void DerivedSettingsCanUseDifferentBackingFiles()
    {
        var settingsDirectory = Assembly.GetEntryAssembly().GetAppSettingsPath();
        var alphaFile = settingsDirectory.GetFile("alpha-settings.json");
        var betaFile = settingsDirectory.GetFile("beta-settings.json");
        var originalAlphaContent = alphaFile.Exists() ? alphaFile.ReadAllText() : null;
        var originalBetaContent = betaFile.Exists() ? betaFile.ReadAllText() : null;

        try
        {
            alphaFile.WriteAllText("{\"Value\":1}");
            betaFile.WriteAllText("{\"Value\":2}");

            using var alphaSettings = new AlphaSettings();
            using var betaSettings = new BetaSettings();

            Assert.That(alphaSettings.Value, Is.EqualTo(1));
            Assert.That(betaSettings.Value, Is.EqualTo(2));
        }
        finally
        {
            RestoreSettingsFile(alphaFile, originalAlphaContent);
            RestoreSettingsFile(betaFile, originalBetaContent);
        }
    }

    private static void RestoreSettingsFile(FileInfo settingsFile, string originalContent)
    {
        if (originalContent == null)
            settingsFile.TryDelete();
        else
            settingsFile.WriteAllText(originalContent);
    }

    private sealed class TestSettings : UserSettingsBase
    {
        public int CacheFormatVersion => Get<int>();

        protected override void ApplyDefaults()
        {
        }
    }

    private sealed class AlphaSettings : UserSettingsBase
    {
        protected override string SettingsFileName => "alpha-settings.json";

        public int Value => Get<int>();

        protected override void ApplyDefaults()
        {
        }
    }

    private sealed class BetaSettings : UserSettingsBase
    {
        protected override string SettingsFileName => "beta-settings.json";

        public int Value => Get<int>();

        protected override void ApplyDefaults()
        {
        }
    }
}
