using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;

using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Tests.Editor;

// The project tree lists what the project's folders hold, in the order of their names, and follows the file system
[Collection(ProjectCollection.Name)]
public sealed class ProjectTreeTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private ProjectManager Manager => Locator.Current.GetService<ProjectManager>()!;

    private ResourceTreeElementViewModel PackageElement(Package package)
    {
        var assets = Assert.Single(Manager.FullProjectTree, element => element.Alias == "assets");
        return Assert.Single(assets.GetInternalChildren()!, element => element.Alias == package.Name);
    }

    private static IEnumerable<string> Names(ResourceTreeElementViewModel element) => element.GetInternalChildren()!.Select(child => child.Alias);

    private GameObject AddObject(string name)
    {
        var gameObject = _project.Add(new GameObject(), name);
        gameObject.SetData(new GameObjectData(gameObject));
        return gameObject;
    }

    [Fact]
    public void FoldersComeBeforeAssetsInTheOrderOfTheirNames()
    {
        AddObject("zebra");
        AddObject("Apple");
        AddObject("mango");
        Directory.CreateDirectory(Path.Combine(_project.Project.ProjectPath, "prefabs"));
        Directory.CreateDirectory(Path.Combine(_project.Project.ProjectPath, "profiles"));
        _project.BuildProjectTree("Global PS2_Test/GameObject/beta", "Global PS2_Test/GameObject/Alpha");

        var objects = Assert.Single(PackageElement(_project.Project.GlobalPackagePS2).GetInternalChildren()!, element => element.Alias == "GameObject");
        Assert.Equal(["Alpha", "beta", "Apple", "mango", "zebra"], Names(objects));
        // What builds write, the prefabs (the Prefabs panel's) and the build profiles aren't assets, the tree doesn't show them
        Directory.CreateDirectory(Path.Combine(_project.Project.ProjectPath, "build", "archives"));
        Manager.SyncProjectTree();
        Assert.DoesNotContain(Manager.FullProjectTree, element => element.Alias is "prefabs" or "profiles" or "build");
        Assert.Contains(Manager.FullProjectTree, element => element.Alias == "assets");
    }

    [Fact]
    public void NewRowsGoInTheOrderOfTheirNames()
    {
        AddObject("mango");
        _project.BuildProjectTree("Global PS2_Test/GameObject/beta");
        var objects = Assert.Single(PackageElement(_project.Project.GlobalPackagePS2).GetInternalChildren()!, element => element.Alias == "GameObject");

        var apple = AddObject("Apple");
        objects.AddNewChild(apple.GetResourceTreeElement(objects));
        var zebra = AddObject("zebra");
        objects.AddNewChild(zebra.GetResourceTreeElement(objects));
        var folder = _project.Add(new Folder("Alpha") { Parent = objects.Asset.URI }, "Alpha");
        objects.AddNewChild(folder.GetResourceTreeElement(objects));

        Assert.Equal(["Alpha", "beta", "Apple", "mango", "zebra"], Names(objects));
        Assert.Equal(["Alpha", "beta", "Apple", "mango", "zebra"], objects.Children!.Select(child => child.Alias));
    }

    [Fact]
    public void TheTreeFollowsTheFileSystem()
    {
        AddObject("kept");
        var gone = AddObject("gone");
        _project.BuildProjectTree();
        var package = PackageElement(_project.Project.GlobalPackagePS2);
        var objects = Assert.Single(package.GetInternalChildren()!, element => element.Alias == "GameObject");
        Assert.Equal(["gone", "kept"], Names(objects));

        // An asset file written by something else, a file deleted and a new folder with a file in it
        var added = new GameObject { Package = _project.Project.GlobalPackagePS2.URI, InvariantName = "added", Alias = "added", Variation = string.Empty, ID = 5 };
        added.RegenerateUri();
        var objectsPath = Path.Combine(_project.AssetsPath, _project.Project.GlobalPackagePS2.Name, "GameObject");
        File.WriteAllText(Path.Combine(objectsPath, "added.json"), JsonConvert.SerializeObject(added, Formatting.Indented));
        File.Delete(Path.Combine(objectsPath, "gone.json"));
        var chunkPath = Path.Combine(_project.AssetsPath, _project.Project.GlobalPackagePS2.Name, "Levels", "Cave");
        Directory.CreateDirectory(chunkPath);
        var chunk = new LevelChunk { Package = _project.Project.GlobalPackagePS2.URI, InvariantName = "Cave", Alias = "Cave", Variation = string.Empty, AdditionalPath = "Levels/Cave" };
        chunk.RegenerateUri();
        File.WriteAllText(Path.Combine(chunkPath, "Cave.json"), JsonConvert.SerializeObject(chunk, Formatting.Indented));

        Manager.SyncProjectTree();

        Assert.Equal(["added", "kept"], Names(objects));
        Assert.True(_project.AssetManager.DoesAssetExist(added.URI));
        Assert.False(_project.AssetManager.DoesAssetExist(gone.URI));
        Assert.Contains(added.URI, ((Folder)objects.Asset).Children);
        var levels = Assert.Single(package.GetInternalChildren()!, element => element.Alias == "Levels");
        var cave = Assert.Single(levels.GetInternalChildren()!);
        Assert.Equal("Cave", cave.Alias);
        Assert.True(((Folder)cave.Asset).Mark.HasFlag(FolderMark.IsChunk));
        Assert.True(_project.AssetManager.DoesAssetExist(chunk.URI));

        // Syncing again with nothing changed changes nothing, and a deleted folder goes with everything in it
        Manager.SyncProjectTree();
        Assert.Equal(["added", "kept"], Names(objects));
        Directory.Delete(Path.Combine(_project.AssetsPath, _project.Project.GlobalPackagePS2.Name, "Levels"), true);
        Manager.SyncProjectTree();
        Assert.DoesNotContain(package.GetInternalChildren()!, element => element.Alias == "Levels");
        Assert.False(_project.AssetManager.DoesAssetExist(chunk.URI));
    }

    // Assets made in TT Lab have no file until what they were made for gets saved, the tree keeps them meanwhile. An asset listed in
    // another folder than the one its file is in moves there instead of leaving the project, the tree took placed instances out of it
    [Fact]
    public void UnsavedAssetsStayAndMisplacedOnesMoveToTheirFilesFolder()
    {
        var kept = AddObject("kept");
        _project.BuildProjectTree();
        var package = PackageElement(_project.Project.GlobalPackagePS2);
        var packageFolder = (Folder)package.Asset;
        var objects = (Folder)Assert.Single(package.GetInternalChildren()!, element => element.Alias == "GameObject").Asset;
        var unsaved = new GameObject { Package = _project.Project.GlobalPackagePS2.URI, InvariantName = "unsaved", Alias = "unsaved", Variation = string.Empty, ID = 6, IsUnsaved = true };
        _project.AssetManager.AddAsset(unsaved);
        objects.AddChild(unsaved);
        objects.Children.Remove(kept.URI);
        packageFolder.AddChild(kept);

        Manager.SyncProjectTree();

        Assert.Same(unsaved, _project.AssetManager.GetAsset(unsaved.URI));
        Assert.Contains(unsaved.URI, objects.Children);
        Assert.Same(kept, _project.AssetManager.GetAsset(kept.URI));
        Assert.Contains(kept.URI, objects.Children);
        Assert.DoesNotContain(kept.URI, packageFolder.Children);

        // Saved, it's an asset like the others
        unsaved.Serialize();
        Assert.False(unsaved.IsUnsaved);
        Manager.SyncProjectTree();
        Assert.Single(objects.Children, uri => uri == unsaved.URI);
    }
}
