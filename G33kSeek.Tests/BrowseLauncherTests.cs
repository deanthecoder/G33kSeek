// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.IO;
using G33kSeek.Services;

namespace G33kSeek.Tests;

public class BrowseLauncherTests
{
    [Test]
    public void GetExecutablePathFindsBrowseInProgramFiles()
    {
        var programFiles = @"C:\Program Files";
        var expectedPath = Path.Combine(programFiles, "Browse", "Browse.exe");

        var result = BrowseLauncher.GetExecutablePath(programFiles, path => path == expectedPath);

        Assert.That(result, Is.EqualTo(expectedPath));
    }

    [Test]
    public void GetExecutablePathReturnsNullWhenBrowseIsNotInstalled()
    {
        var result = BrowseLauncher.GetExecutablePath(@"C:\Program Files", _ => false);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CreateStartInfoPassesTheFullPathAsOneArgument()
    {
        var startInfo = BrowseLauncher.CreateStartInfo(@"C:\Program Files\Browse\Browse.exe", @"C:\Folder With Spaces\file.txt");

        Assert.That(startInfo.FileName, Is.EqualTo(@"C:\Program Files\Browse\Browse.exe"));
        Assert.That(startInfo.UseShellExecute, Is.True);
        Assert.That(startInfo.ArgumentList, Is.EquivalentTo(new[] { @"C:\Folder With Spaces\file.txt" }));
    }
}
