// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
// 
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
// 
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using DTC.Core;
using DTC.Core.Extensions;
using DTC.Core.JsonConverters;
using G33kSeek.Models;
using Newtonsoft.Json;

namespace G33kSeek.Tests;

public class IndexedFileTests
{
    [Test]
    public void CreatePrimaryActionReturnsOpenPathAction()
    {
        using var tempDirectory = new TempDirectory();
        var file = tempDirectory.GetFile("report.txt");
        file.WriteAllText("report");
        var indexedFile = new IndexedFile
        {
            DisplayName = "report.txt",
            SearchText = "report txt",
            File = file
        };

        var action = indexedFile.CreatePrimaryAction();

        Assert.That(action.Kind, Is.EqualTo(QueryActionKind.OpenPath));
        Assert.That(action.Payload, Is.EqualTo(file.FullName));
        Assert.That(indexedFile.Subtitle, Is.EqualTo(file.DirectoryName));
    }

    [Test]
    public void CreatePrimaryActionReturnsOpenPathActionForDirectory()
    {
        using var tempDirectory = new TempDirectory();
        var directory = tempDirectory.GetDir("Reports");
        directory.Create();
        var indexedFile = new IndexedFile
        {
            DisplayName = "Reports",
            SearchText = "reports",
            Directory = directory
        };

        var action = indexedFile.CreatePrimaryAction();

        Assert.That(action.Kind, Is.EqualTo(QueryActionKind.OpenPath));
        Assert.That(action.Payload, Is.EqualTo(directory.FullName));
        Assert.That(indexedFile.IsDirectory, Is.True);
    }

    [Test]
    public void SerializationOmitsComputedFileProperties()
    {
        var indexedFile = new IndexedFile
        {
            DisplayName = "report.txt",
            SearchText = "report txt",
            File = new FileInfo(@"C:\Docs\report.txt")
        };

        var json = JsonConvert.SerializeObject(
            indexedFile,
            new JsonSerializerSettings
            {
                Converters =
                [
                    new FileInfoConverter(),
                    new DirectoryInfoConverter()
                ]
            });

        Assert.That(json, Does.Not.Contain("IsDirectory"));
        Assert.That(json, Does.Not.Contain("Subtitle"));
        Assert.That(json, Does.Not.Contain("FullPath"));
        Assert.That(json, Does.Contain("File"));
    }
}
