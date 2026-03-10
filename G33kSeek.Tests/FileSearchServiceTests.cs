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
using G33kSeek.Services;

namespace G33kSeek.Tests;

public class FileSearchServiceTests
{
    [Test]
    public void GetDefaultSearchRootsIncludesCommonDocumentsOnWindows()
    {
        var userProfilePath = CreateRootedPath("Users", "Dean");
        var publicDocumentsPath = CreateRootedPath("Users", "Public", "Documents");
        var roots = FileSearchService.GetDefaultSearchRoots(
            isWindows: true,
            folderPathAccessor: specialFolder => specialFolder switch
            {
                Environment.SpecialFolder.MyDocuments => Path.Combine(userProfilePath, "Documents"),
                Environment.SpecialFolder.MyPictures => Path.Combine(userProfilePath, "Pictures"),
                Environment.SpecialFolder.UserProfile => userProfilePath,
                Environment.SpecialFolder.CommonDocuments => publicDocumentsPath,
                _ => string.Empty
            });

        Assert.That(roots.Select(root => root.FullName), Is.EqualTo(new[]
        {
            Path.Combine(userProfilePath, "Documents"),
            Path.Combine(userProfilePath, "Pictures"),
            Path.Combine(userProfilePath, "Downloads"),
            publicDocumentsPath
        }));
    }

    [Test]
    public void GetDefaultSearchRootsIncludesPicturesAndDownloadsCrossPlatform()
    {
        var userProfilePath = CreateRootedPath("Users", "Dean");
        var roots = FileSearchService.GetDefaultSearchRoots(
            isWindows: false,
            folderPathAccessor: specialFolder => specialFolder switch
            {
                Environment.SpecialFolder.MyDocuments => Path.Combine(userProfilePath, "Documents"),
                Environment.SpecialFolder.MyPictures => Path.Combine(userProfilePath, "Pictures"),
                Environment.SpecialFolder.UserProfile => userProfilePath,
                _ => string.Empty
            });

        Assert.That(roots.Select(root => root.FullName), Is.EqualTo(new[]
        {
            Path.Combine(userProfilePath, "Documents"),
            Path.Combine(userProfilePath, "Pictures"),
            Path.Combine(userProfilePath, "Downloads")
        }));
    }

    [Test]
    public async Task SearchAsyncReturnsMatchingFiles()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        documentsDirectory.GetFile("invoice.pdf").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("invoice", CancellationToken.None);

        Assert.That(results.TotalMatchCount, Is.EqualTo(1));
        Assert.That(results.VisibleFiles.Any(result => result.DisplayName == "invoice.pdf"), Is.True);
    }

    [Test]
    public async Task SearchAsyncReturnsMatchingFolders()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var invoicesDirectory = documentsDirectory.GetDir("Invoices");
        invoicesDirectory.Create();
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("invoice", CancellationToken.None);

        Assert.That(results.VisibleFiles.Any(result => result.IsDirectory && result.DisplayName == "Invoices"), Is.True);
    }

    [Test]
    public async Task SearchAsyncSkipsExcludedDirectories()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        documentsDirectory.GetFile("report.txt").WriteAllText("report");
        var nodeModulesDirectory = documentsDirectory.GetDir("node_modules");
        nodeModulesDirectory.Create();
        nodeModulesDirectory.GetFile("ignore-me.txt").WriteAllText("ignore");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var ignoredResults = await service.SearchAsync("ignore", CancellationToken.None);
        var includedResults = await service.SearchAsync("report", CancellationToken.None);

        Assert.That(ignoredResults.VisibleFiles, Is.Empty);
        Assert.That(includedResults.VisibleFiles.Any(result => result.DisplayName == "report.txt"), Is.True);
    }

    [Test]
    public async Task SearchAsyncCapturesMoreThanVisibleResultLimit()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        for (var i = 1; i <= 15; i++)
            documentsDirectory.GetFile($"AssemblyInfo{i}.cs").WriteAllText("content");

        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("assemblyinfo", CancellationToken.None);

        Assert.That(results.TotalMatchCount, Is.EqualTo(15));
        Assert.That(results.VisibleFiles, Has.Count.EqualTo(15));
    }

    [Test]
    public async Task SearchAsyncCapsVisibleResultsAtTwentyFive()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        for (var i = 1; i <= 30; i++)
            documentsDirectory.GetFile($"AssemblyInfo{i}.cs").WriteAllText("content");

        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("assemblyinfo", CancellationToken.None);

        Assert.That(results.TotalMatchCount, Is.EqualTo(30));
        Assert.That(results.VisibleFiles, Has.Count.EqualTo(25));
    }

    [Test]
    public async Task SearchAsyncMatchesAgainstDirectoryPathSegments()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var repoDirectory = documentsDirectory.GetDir("Repos/G33kSeek");
        repoDirectory.Create();
        repoDirectory.GetFile("AssemblyInfo.cs").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("g33kseek", CancellationToken.None);

        Assert.That(results.VisibleFiles.Any(result => result.DisplayName == "AssemblyInfo.cs"), Is.True);
    }

    [Test]
    public async Task SearchAsyncPrefersExactFileNameMatchesOverPathOnlyMatches()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var directMatch = documentsDirectory.GetFile("MainWindow.axaml.cs");
        directMatch.WriteAllText("content");
        var nestedDirectory = documentsDirectory.GetDir("Nested/MainWindow.axaml.cs");
        nestedDirectory.Create();
        nestedDirectory.GetFile("View.cs").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("mainwindow.axaml.cs", CancellationToken.None);

        var topFileResult = results.VisibleFiles.First(result => !result.IsDirectory);
        Assert.That(topFileResult.DisplayName, Is.EqualTo("MainWindow.axaml.cs"));
    }

    [Test]
    public async Task SearchAsyncPrefersNearestMatchingPathSegments()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var nearerDirectory = documentsDirectory.GetDir("Repos/G33kSeek");
        nearerDirectory.Create();
        nearerDirectory.GetFile("AssemblyInfo.cs").WriteAllText("content");
        var fartherDirectory = documentsDirectory.GetDir("G33kSeek/Deep/Nested");
        fartherDirectory.Create();
        fartherDirectory.GetFile("AssemblyInfo.cs").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("g33kseek", CancellationToken.None);

        var topFileResult = results.VisibleFiles.First(result => !result.IsDirectory);
        Assert.That(topFileResult.Subtitle, Is.EqualTo(nearerDirectory.FullName));
    }

    [Test]
    public async Task SearchAsyncTreatsPeriodsAsMeaningfulInFileQueries()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        documentsDirectory.GetDir("JetBrains").Create();
        documentsDirectory.GetFile("brains.mm").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("brains.", CancellationToken.None);

        Assert.That(results.TotalMatchCount, Is.EqualTo(1));
        Assert.That(results.VisibleFiles.Single().DisplayName, Is.EqualTo("brains.mm"));
    }

    [Test]
    public async Task AddSearchRootAsyncAddsFilesFromNewRoot()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var extraDirectory = tempDirectory.GetDir("Extra");
        extraDirectory.Create();
        extraDirectory.GetFile("image.png").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var addStatus = await service.AddSearchRootAsync(extraDirectory, CancellationToken.None);
        var results = await service.SearchAsync("image", CancellationToken.None);

        Assert.That(addStatus, Is.EqualTo(FileSearchRootAddStatus.Added));
        Assert.That(results.VisibleFiles.Any(result => result.DisplayName == "image.png"), Is.True);
    }

    [Test]
    public async Task AddSearchRootAsyncIgnoresRootsCoveredByExistingSearchLocations()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var nestedDirectory = documentsDirectory.GetDir("Nested");
        nestedDirectory.Create();
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var addStatus = await service.AddSearchRootAsync(nestedDirectory, CancellationToken.None);

        Assert.That(addStatus, Is.EqualTo(FileSearchRootAddStatus.AlreadyCovered));
    }

    [Test]
    public async Task AddSearchRootAsyncRemovesNowCoveredChildRoots()
    {
        using var tempDirectory = new TempDirectory();
        var parentDirectory = tempDirectory.GetDir("Parent");
        var childDirectory = parentDirectory.GetDir("Child");
        childDirectory.Create();
        childDirectory.GetFile("image.png").WriteAllText("content");
        var service = new FileSearchService([childDirectory], [], DateTime.MinValue);

        var addStatus = await service.AddSearchRootAsync(parentDirectory, CancellationToken.None);
        var results = await service.SearchAsync("image", CancellationToken.None);

        Assert.That(addStatus, Is.EqualTo(FileSearchRootAddStatus.Added));
        Assert.That(results.TotalMatchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WarmAsyncDoesNotRefreshTwiceWhenConcurrentRequestsOverlap()
    {
        var refreshCount = 0;
        var service = new FileSearchService(
            [],
            [],
            DateTime.MinValue,
            _ =>
            {
                refreshCount++;
                Thread.Sleep(150);
                return ([], 0, 0);
            });

        var firstWarmTask = service.WarmAsync();
        await Task.Delay(20);
        await Task.WhenAll(firstWarmTask, service.WarmAsync());

        Assert.That(refreshCount, Is.EqualTo(1));
    }

    private static string CreateRootedPath(params string[] segments)
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory) ?? Path.DirectorySeparatorChar.ToString();
        return Path.Combine([root, .. segments]);
    }
}
