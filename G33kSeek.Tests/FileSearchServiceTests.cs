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
using G33kSeek.Models;
using G33kSeek.Services;

namespace G33kSeek.Tests;

public class FileSearchServiceTests
{
    [Test]
    public void GetDefaultSearchRootsIncludesCommonDocumentsAndDesktopOnWindows()
    {
        var userProfilePath = CreateRootedPath("Users", "Dean");
        var publicDocumentsPath = CreateRootedPath("Users", "Public", "Documents");
        var roots = FileSearchService.GetDefaultSearchRoots(
            isWindows: true,
            folderPathAccessor: specialFolder => specialFolder switch
            {
                Environment.SpecialFolder.MyDocuments => Path.Combine(userProfilePath, "Documents"),
                Environment.SpecialFolder.MyPictures => Path.Combine(userProfilePath, "Pictures"),
                Environment.SpecialFolder.DesktopDirectory => Path.Combine(userProfilePath, "Desktop"),
                Environment.SpecialFolder.UserProfile => userProfilePath,
                Environment.SpecialFolder.CommonDocuments => publicDocumentsPath,
                _ => string.Empty
            });

        Assert.That(roots.Select(root => root.FullName), Is.EqualTo(new[]
        {
            Path.Combine(userProfilePath, "Documents"),
            Path.Combine(userProfilePath, "Pictures"),
            Path.Combine(userProfilePath, "Desktop"),
            Path.Combine(userProfilePath, "Downloads"),
            publicDocumentsPath
        }));
    }

    [Test]
    public void GetDefaultSearchRootsIncludesPicturesDesktopAndDownloadsCrossPlatform()
    {
        var userProfilePath = CreateRootedPath("Users", "Dean");
        var roots = FileSearchService.GetDefaultSearchRoots(
            isWindows: false,
            folderPathAccessor: specialFolder => specialFolder switch
            {
                Environment.SpecialFolder.MyDocuments => Path.Combine(userProfilePath, "Documents"),
                Environment.SpecialFolder.MyPictures => Path.Combine(userProfilePath, "Pictures"),
                Environment.SpecialFolder.DesktopDirectory => Path.Combine(userProfilePath, "Desktop"),
                Environment.SpecialFolder.UserProfile => userProfilePath,
                _ => string.Empty
            });

        Assert.That(roots.Select(root => root.FullName), Is.EqualTo(new[]
        {
            Path.Combine(userProfilePath, "Documents"),
            Path.Combine(userProfilePath, "Pictures"),
            Path.Combine(userProfilePath, "Desktop"),
            Path.Combine(userProfilePath, "Downloads")
        }));
    }

    [Test]
    public void GetInitialSearchRootsMergesConfiguredRootsWithDefaultRoots()
    {
        var userProfilePath = CreateRootedPath("Users", "Dean");
        var configuredRoot = CreateRootedPath("Projects", "Shared");
        var roots = FileSearchService.GetInitialSearchRoots(
            [configuredRoot.ToDir()],
            hasExplicitSearchRoots: true,
            isWindows: false,
            folderPathAccessor: specialFolder => specialFolder switch
            {
                Environment.SpecialFolder.MyDocuments => Path.Combine(userProfilePath, "Documents"),
                Environment.SpecialFolder.MyPictures => Path.Combine(userProfilePath, "Pictures"),
                Environment.SpecialFolder.DesktopDirectory => Path.Combine(userProfilePath, "Desktop"),
                Environment.SpecialFolder.UserProfile => userProfilePath,
                _ => string.Empty
            });

        Assert.That(roots.Select(root => root.FullName), Is.EquivalentTo(new[]
        {
            configuredRoot,
            Path.Combine(userProfilePath, "Documents"),
            Path.Combine(userProfilePath, "Pictures"),
            Path.Combine(userProfilePath, "Desktop"),
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
    public async Task SearchAsyncMatchesSpaceSeparatedTokensOutOfOrderInFileName()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        documentsDirectory.GetFile("SpcOpcNodeDocument.html").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("spc opc html", CancellationToken.None);

        Assert.That(results.TotalMatchCount, Is.EqualTo(1));
        Assert.That(results.VisibleFiles.Single().DisplayName, Is.EqualTo("SpcOpcNodeDocument.html"));
    }

    [Test]
    public async Task SearchAsyncUsesPathTokensToNarrowDuplicateFileNames()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var g33kDirectory = documentsDirectory.GetDir("Repos/G33kSeek/Properties");
        g33kDirectory.Create();
        g33kDirectory.GetFile("AssemblyInfo.cs").WriteAllText("content");
        var otherDirectory = documentsDirectory.GetDir("Repos/OtherProduct/Properties");
        otherDirectory.Create();
        otherDirectory.GetFile("AssemblyInfo.cs").WriteAllText("content");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        var results = await service.SearchAsync("assemblyinfo g33k", CancellationToken.None);

        Assert.That(results.VisibleFiles, Is.Not.Empty);
        Assert.That(results.VisibleFiles.First().Subtitle, Is.EqualTo(g33kDirectory.FullName));
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

    [Test]
    public async Task WarmAsyncSkipsRefreshWhenWatchersReportNoChanges()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var cachedFile = new IndexedFile
        {
            DisplayName = "report.txt",
            File = documentsDirectory.GetFile("report.txt")
        };
        var refreshCount = 0;
        var service = new FileSearchService(
            [documentsDirectory],
            [cachedFile],
            DateTime.UtcNow - TimeSpan.FromMinutes(11),
            _ =>
            {
                refreshCount++;
                return ([cachedFile], 0, 0);
            },
            enableWatchers: true);

        await service.WarmAsync();

        Assert.That(refreshCount, Is.EqualTo(0));
    }

    [Test]
    public async Task WarmAsyncRefreshesWhenPeriodicFullReconcileIsDue()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        documentsDirectory.Create();
        var cachedFile = new IndexedFile
        {
            DisplayName = "report.txt",
            File = documentsDirectory.GetFile("report.txt")
        };
        var refreshCount = 0;
        var service = new FileSearchService(
            [documentsDirectory],
            [cachedFile],
            DateTime.UtcNow - TimeSpan.FromHours(2),
            _ =>
            {
                refreshCount++;
                return ([cachedFile], 0, 0);
            },
            enableWatchers: true);

        await service.WarmAsync();

        Assert.That(refreshCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RefreshNowAsyncReusesUnchangedDirectories()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var imagesDirectory = documentsDirectory.GetDir("Images");
        var nestedDirectory = imagesDirectory.GetDir("Nested");
        nestedDirectory.Create();
        documentsDirectory.GetFile("report.txt").WriteAllText("report");
        nestedDirectory.GetFile("image.png").WriteAllText("image");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        await service.RefreshNowAsync(CancellationToken.None);
        await service.RefreshNowAsync(CancellationToken.None);

        Assert.That(service.LastRefreshMetrics.VisitedDirectoryCount, Is.EqualTo(1));
        Assert.That(service.LastRefreshMetrics.RebuiltDirectoryCount, Is.EqualTo(0));
        Assert.That(service.LastRefreshMetrics.ReusedDirectoryCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RefreshNowAsyncRebuildsOnlyChangedSubtree()
    {
        using var tempDirectory = new TempDirectory();
        var documentsDirectory = tempDirectory.GetDir("Documents");
        var stableDirectory = documentsDirectory.GetDir("Stable");
        var changingDirectory = documentsDirectory.GetDir("Changing");
        var deepDirectory = changingDirectory.GetDir("Deep");
        stableDirectory.Create();
        deepDirectory.Create();
        stableDirectory.GetFile("stable.txt").WriteAllText("stable");
        deepDirectory.GetFile("before.txt").WriteAllText("before");
        var service = new FileSearchService([documentsDirectory], [], DateTime.MinValue);

        await service.RefreshNowAsync(CancellationToken.None);

        Thread.Sleep(1100);
        deepDirectory.GetFile("after.txt").WriteAllText("after");
        service.MarkPathDirty(deepDirectory.FullName);

        await service.RefreshNowAsync(CancellationToken.None);
        var results = await service.SearchAsync("after", CancellationToken.None);

        Assert.That(results.VisibleFiles.Any(result => result.DisplayName == "after.txt"), Is.True);
        Assert.That(service.LastRefreshMetrics.VisitedDirectoryCount, Is.EqualTo(3));
        Assert.That(service.LastRefreshMetrics.RebuiltDirectoryCount, Is.GreaterThan(0));
        Assert.That(service.LastRefreshMetrics.RebuiltDirectoryCount, Is.LessThan(service.LastRefreshMetrics.VisitedDirectoryCount));
        Assert.That(service.LastRefreshMetrics.ReusedDirectoryCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task RefreshNowAsyncReusesCleanTopLevelBucketsWhenKnownDirtyPathIsOutsideThem()
    {
        using var tempDirectory = new TempDirectory();
        var sourceDirectory = tempDirectory.GetDir("Source");
        var repoOneDirectory = sourceDirectory.GetDir("RepoOne");
        var repoTwoDirectory = sourceDirectory.GetDir("RepoTwo");
        var repoOneDeepDirectory = repoOneDirectory.GetDir("Deep");
        var repoTwoDeepDirectory = repoTwoDirectory.GetDir("Deep");
        repoOneDeepDirectory.Create();
        repoTwoDeepDirectory.Create();
        repoOneDeepDirectory.GetFile("one.txt").WriteAllText("one");
        repoTwoDeepDirectory.GetFile("before.txt").WriteAllText("before");
        var service = new FileSearchService([sourceDirectory], [], DateTime.MinValue);

        await service.RefreshNowAsync(CancellationToken.None);

        Thread.Sleep(1100);
        var changedFile = repoTwoDeepDirectory.GetFile("after.txt");
        changedFile.WriteAllText("after");
        service.MarkPathDirty(changedFile.FullName);

        await service.RefreshNowAsync(CancellationToken.None);

        Assert.That(service.LastRefreshMetrics.VisitedDirectoryCount, Is.EqualTo(3));
        Assert.That(service.LastRefreshMetrics.ReusedDirectoryCount, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task RefreshNowAsyncMarksRootEntriesDirtyForTopLevelFiles()
    {
        using var tempDirectory = new TempDirectory();
        var sourceDirectory = tempDirectory.GetDir("Source");
        sourceDirectory.Create();
        sourceDirectory.GetFile("before.txt").WriteAllText("before");
        var service = new FileSearchService([sourceDirectory], [], DateTime.MinValue);

        await service.RefreshNowAsync(CancellationToken.None);

        Thread.Sleep(1100);
        var changedFile = sourceDirectory.GetFile("after.txt");
        changedFile.WriteAllText("after");
        service.MarkPathDirty(changedFile.FullName);

        await service.RefreshNowAsync(CancellationToken.None);
        var results = await service.SearchAsync("after", CancellationToken.None);

        Assert.That(results.VisibleFiles.Any(result => result.DisplayName == "after.txt"), Is.True);
        Assert.That(service.LastRefreshMetrics.RebuiltDirectoryCount, Is.GreaterThan(0));
    }

    [Test]
    public async Task RefreshNowAsyncIgnoresDirtyPathsInsideExcludedDirectories()
    {
        using var tempDirectory = new TempDirectory();
        var sourceDirectory = tempDirectory.GetDir("Source");
        var repoDirectory = sourceDirectory.GetDir("Repo");
        var srcDirectory = repoDirectory.GetDir("src");
        var objDirectory = repoDirectory.GetDir("obj");
        srcDirectory.Create();
        objDirectory.Create();
        srcDirectory.GetFile("main.cs").WriteAllText("class Program {}");
        var service = new FileSearchService([sourceDirectory], [], DateTime.MinValue);

        await service.RefreshNowAsync(CancellationToken.None);

        Thread.Sleep(1100);
        var ignoredFile = objDirectory.GetFile("temp.obj");
        ignoredFile.WriteAllText("ignored");
        service.MarkPathDirty(ignoredFile.FullName);

        await service.RefreshNowAsync(CancellationToken.None);
        var results = await service.SearchAsync("temp.obj", CancellationToken.None);

        Assert.That(results.VisibleFiles, Is.Empty);
        Assert.That(service.LastRefreshMetrics.RebuiltDirectoryCount, Is.EqualTo(0));
        Assert.That(service.LastRefreshMetrics.ReusedDirectoryCount, Is.EqualTo(1));
    }

    private static string CreateRootedPath(params string[] segments)
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory) ?? Path.DirectorySeparatorChar.ToString();
        return Path.Combine([root, .. segments]);
    }
}
