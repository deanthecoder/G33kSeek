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
using System.Collections.Generic;
using System.IO;
using DTC.Core.Settings;

namespace G33kSeek.Services;

/// <summary>
/// Persists application search roots and the cached app index for the launcher.
/// </summary>
/// <remarks>
/// This stores user-overridden roots and the cached app index so search can start quickly between launches.
/// </remarks>
internal sealed class ApplicationSearchSettings : UserSettingsBase
{
    protected override string SettingsFileName => "application-search-settings.json";

    public List<DirectoryInfo> MacApplicationRoots
    {
        get => Get<List<DirectoryInfo>>() ?? [];
        set => Set(value ?? []);
    }

    public List<DirectoryInfo> WindowsApplicationRoots
    {
        get => Get<List<DirectoryInfo>>() ?? [];
        set => Set(value ?? []);
    }

    public List<Models.IndexedApplication> CachedApplications
    {
        get => Get<List<Models.IndexedApplication>>() ?? [];
        set => Set(value ?? []);
    }

    public DateTime LastApplicationRefreshUtc
    {
        get => Get<DateTime>();
        set => Set(value);
    }

    protected override void ApplyDefaults()
    {
        MacApplicationRoots = [];
        WindowsApplicationRoots = [];
        CachedApplications = [];
        LastApplicationRefreshUtc = DateTime.MinValue;
    }
}
