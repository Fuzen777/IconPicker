using IconPicker.Core.Candidates;
using IconPicker.Core.Icons;
using IconPicker.Core.Recent;

namespace IconPicker.Core.Tests;

/// <summary>The last icons applied by hand, kept as copies and cycled.</summary>
[TestClass]
public sealed class RecentIconStoreTests
{
    [TestMethod]
    public async Task AddAsync_PastCapacity_KeepsTheMostRecentFirst()
    {
        using var testFolder = new TestFolder();
        var store = new RecentIconStore(Path.Combine(testFolder.Root.FullName, "Recent icons"));

        for (var index = 0; index <= RecentIconStore.Capacity; index++)
        {
            await store.AddAsync(await CreateCandidateAsync(testFolder, $"icon{index}.ico", red: (byte)(index * 20)), CancellationToken.None);
        }

        var names = store.List().Select(recentIcon => recentIcon.Name).ToList();
        Assert.HasCount(RecentIconStore.Capacity, names);
        Assert.AreEqual($"icon{RecentIconStore.Capacity}.ico", names[0]);
        CollectionAssert.DoesNotContain(names, "icon0.ico");
    }

    [TestMethod]
    public async Task AddAsync_SameIconFromAnotherFile_IsListedOnceAtTheTop()
    {
        using var testFolder = new TestFolder();
        var store = new RecentIconStore(Path.Combine(testFolder.Root.FullName, "Recent icons"));
        var first = await CreateCandidateAsync(testFolder, "first.ico", red: 255);
        await store.AddAsync(first, CancellationToken.None);
        await store.AddAsync(await CreateCandidateAsync(testFolder, "other.ico", red: 0), CancellationToken.None);
        var sameAsFirst = testFolder.CreateFile(@"Elsewhere\same.ico", File.ReadAllBytes(first.Source.FilePath));

        await store.AddAsync(Candidate(sameAsFirst.FullName), CancellationToken.None);

        var recentIcons = store.List();
        Assert.HasCount(2, recentIcons);
        Assert.AreEqual("same.ico", recentIcons[0].Name);
        Assert.AreEqual(sameAsFirst.FullName, recentIcons[0].SourcePath);
        // The copy, not the source, is what a recent icon gives.
        CollectionAssert.AreEqual(File.ReadAllBytes(first.Source.FilePath), File.ReadAllBytes(store.GetIconPath(recentIcons[0])));
    }

    [TestMethod]
    public async Task AddAsync_RecentIconAppliedAgain_KeepsWhereItCameFrom()
    {
        using var testFolder = new TestFolder();
        var store = new RecentIconStore(Path.Combine(testFolder.Root.FullName, "Recent icons"));
        var exe = testFolder.CreateIconExe(@"Game\Game.exe");
        var original = await store.AddAsync(Candidate(exe.FullName, IconSourceKind.Module), CancellationToken.None);
        await store.AddAsync(await CreateCandidateAsync(testFolder, "other.ico", red: 0), CancellationToken.None);
        var iconPath = store.GetIconPath(original);
        var recentCandidate = new IconCandidate(new IconSource(iconPath, IconSourceKind.IconFile, IsAppData: true), iconPath, 0, new FileInfo(iconPath).Length, 256, 256, Recent: original);

        await store.AddAsync(recentCandidate, CancellationToken.None);

        var top = store.List()[0];
        Assert.AreEqual(original.Id, top.Id);
        Assert.AreEqual("Game.exe", top.Name);
        Assert.AreEqual(original.SourcePath, top.SourcePath);
        // An executable's icon is kept as the .ico a folder gets, with its 256 px image.
        Assert.Contains(256, IcoFileBuilder.ReadImageSizes(iconPath)!.Select(size => size.Width));
    }

    [TestMethod]
    public async Task RemoveAsync_KeepsTheFileUntilDeleteUnlisted()
    {
        using var testFolder = new TestFolder();
        var store = new RecentIconStore(Path.Combine(testFolder.Root.FullName, "Recent icons"));
        var removed = await store.AddAsync(await CreateCandidateAsync(testFolder, "removed.ico", red: 255), CancellationToken.None);
        var kept = await store.AddAsync(await CreateCandidateAsync(testFolder, "kept.ico", red: 0), CancellationToken.None);

        await store.RemoveAsync(removed.Id);

        Assert.AreEqual(kept.Id, store.List().Single().Id);
        // A removed icon may still be picked in the open folder.
        Assert.IsTrue(File.Exists(store.GetIconPath(removed)));
        store.DeleteUnlisted();
        Assert.IsFalse(File.Exists(store.GetIconPath(removed)));
        Assert.IsTrue(File.Exists(store.GetIconPath(kept)));
        Assert.AreEqual(kept.Id, store.List().Single().Id);
    }

    [TestMethod]
    public async Task List_DamagedIndex_StartsAfresh()
    {
        using var testFolder = new TestFolder();
        var store = new RecentIconStore(Path.Combine(testFolder.Root.FullName, "Recent icons"));
        testFolder.CreateTextFile(@"Recent icons\recent.json", """[{"Id": "..\\..\\escape", "Name": "x"}, {"Id": 3""");

        Assert.IsEmpty(store.List());
        await store.AddAsync(await CreateCandidateAsync(testFolder, "icon.ico", red: 255), CancellationToken.None);
        Assert.AreEqual("icon.ico", store.List().Single().Name);
    }

    private static async Task<IconCandidate> CreateCandidateAsync(TestFolder testFolder, string fileName, byte red)
    {
        var icon = testFolder.CreateFile(Path.Combine("Icons", fileName), IcoFileBuilder.Build([IcoImage.FromPng(256, await TestImages.CreatePngAsync(256, 256, red: red))]));
        return Candidate(icon.FullName);
    }

    private static IconCandidate Candidate(string filePath, IconSourceKind kind = IconSourceKind.IconFile)
    {
        return new IconCandidate(new IconSource(filePath, kind), filePath, 0, new FileInfo(filePath).Length, 256, 256);
    }
}
