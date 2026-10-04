using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Caliburn.Micro;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using TT_Lab.Project.Prefabs;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.Views;
using Twinsanity.TwinsanityInterchange.Common;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Editor;

// Prefabs are kept in folders, the panel shows one at a time, and a new project gets a prefab of every different object instance of its
// chunks in the chunk's folder, the global objects' in Global
[Collection(ProjectCollection.Name)]
public sealed class PrefabFoldersTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly PrefabLibrary _library;

    public PrefabFoldersTests()
    {
        _library = new PrefabLibrary(_project.Project);
    }

    public void Dispose() => _project.Dispose();

    private Prefab CratePrefab(string name, string folder = "") => new()
    {
        Name = name, Kind = PrefabKind.Instance, Platform = "PS2", Package = _project.Project.GlobalPackagePS2.URI, LayoutID = 0,
        AssetType = typeof(ObjectInstance).FullName!, DataType = typeof(ObjectInstanceData).FullName, Data = new JObject(), Folder = folder
    };

    [Fact]
    public void PrefabsAreKeptInFolders()
    {
        _library.Save(CratePrefab("Crate"));
        _library.Save(CratePrefab("Nitro", "Global/Crates"));

        Assert.Equal(["Crate"], _library.Load(string.Empty).Select(prefab => prefab.Name));
        Assert.Equal(["Global"], _library.Folders(string.Empty));
        Assert.Equal(["Crates"], _library.Folders("Global"));
        var nitro = Assert.Single(_library.Load("Global/Crates"));
        Assert.Equal("Global/Crates", nitro.Folder);
        Assert.True(File.Exists(Path.Combine(_library.Folder, "Global", "Crates", "Nitro.json")));
        // Every prefab of every folder
        Assert.Equal(["Crate", "Nitro"], _library.Load().Select(prefab => prefab.Name));
        Assert.Equal(1, _library.CountPrefabs("Global"));
    }

    // Numbered prefabs and folders go in the order of their numbers, the panel shows Mesh 2 before Mesh 10
    [Fact]
    public void PrefabsAndFoldersGoInTheOrderOfTheirNumbers()
    {
        foreach (var name in new[] { "Mesh 10", "mesh 2", "Mesh 1", "Bush" })
        {
            _library.Save(CratePrefab(name));
        }

        _library.CreateFolder(string.Empty, "Level 12");
        _library.CreateFolder(string.Empty, "Level 3");

        Assert.Equal(["Bush", "Mesh 1", "mesh 2", "Mesh 10"], _library.Load(string.Empty).Select(prefab => prefab.Name));
        Assert.Equal(["Level 3", "Level 12"], _library.Folders(string.Empty));
    }

    [Fact]
    public void FoldersAreMadeRenamedAndDeleted()
    {
        Assert.Equal("Rocks", _library.CreateFolder(string.Empty, "Rocks"));
        Assert.Equal("Rocks 2", _library.CreateFolder(string.Empty, "Rocks"));
        _library.Save(CratePrefab("Boulder", "Rocks"));

        Assert.Equal("Stones", _library.RenameFolder("Rocks", "Stones"));
        Assert.Equal("Stones", Assert.Single(_library.Load("Stones")).Folder);
        // A name another folder has keeps the folder as it is
        Assert.Equal("Rocks 2", _library.RenameFolder("Rocks 2", "Stones"));

        _library.DeleteFolder("Stones");
        Assert.Equal(["Rocks 2"], _library.Folders(string.Empty));
        Assert.Throws<ArgumentException>(() => _library.Load("../Outside"));
    }

    [Fact]
    public void PrefabsMoveWithTheirPicture()
    {
        var crate = CratePrefab("Crate");
        _library.Save(crate);
        File.WriteAllBytes(Path.ChangeExtension(crate.FilePath!, ".png"), [1, 2, 3]);
        crate = _library.Load(string.Empty).Single();

        _library.Move(crate, "Global");

        Assert.Empty(_library.Load(string.Empty));
        var moved = Assert.Single(_library.Load("Global"));
        Assert.NotNull(moved.PreviewPath);
        Assert.Equal("Global", crate.Folder);
    }

    // The panel shows a folder's folders and prefabs, the way to it as steps back, and saves into it
    [AvaloniaFact]
    public void ThePanelGoesThroughFolders()
    {
        _library.Save(CratePrefab("Crate"));
        _library.Save(CratePrefab("Nitro", "Global/Crates"));
        var panel = new PrefabsViewModel(new ScenesEditorsViewModel(), new EventAggregator(), new PrefabPictures());
        panel.Refresh();

        Assert.Equal(["Crate"], panel.Prefabs.Select(entry => entry.Name));
        Assert.Equal(["Global"], panel.Folders.Select(entry => entry.Name));
        Assert.Equal(["Prefabs"], panel.Crumbs.Select(crumb => crumb.Name));

        panel.OpenFolder("Global/Crates");
        Assert.Equal(["Nitro"], panel.Prefabs.Select(entry => entry.Name));
        Assert.Equal(["Prefabs", "Global", "Crates"], panel.Crumbs.Select(crumb => crumb.Name));

        panel.Move(panel.Prefabs[0].Prefab, "Global");
        Assert.Empty(panel.Prefabs);
        panel.Crumbs[1].OpenCommand.Execute().Subscribe();
        Assert.Equal(["Nitro"], panel.Prefabs.Select(entry => entry.Name));

        panel.NewFolderCommand.Execute().Subscribe();
        var made = panel.Folders.Single(entry => entry.Name == "New folder");
        Assert.True(made.IsRenaming);
        made.EditName = "Bonus";
        made.CommitRename();
        Assert.Contains("Bonus", panel.Folders.Select(entry => entry.Name));

        // A folder deleted elsewhere leaves the panel showing the one it was in
        panel.OpenFolder("Global/Bonus");
        _library.DeleteFolder("Global/Bonus");
        panel.Refresh();
        Assert.Equal("Global", panel.CurrentFolder);
    }

    private ObjectInstance AddInstance(LevelChunk chunk, string name, GameObject gameObject, Vector3 position, Action<ObjectInstanceData>? setup = null)
    {
        var instance = _project.Add(new ObjectInstance { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = 0 }, $"{name} {chunk.Name}", package: _project.Project.Ps2Package);
        var data = new ObjectInstanceData(instance) { ObjectId = gameObject.URI, Position = position, Rotation = new Vector3(0, 90, 0), FloatProperties = [1.0f] };
        setup?.Invoke(data);
        instance.SetData(data);
        chunk.ChunkResources.Add(instance.URI);
        return instance;
    }

    private GameObject AddObject(string name, string gameName, Package package)
    {
        var gameObject = _project.Add(new GameObject(), name, package: package);
        gameObject.SetData(new GameObjectData(gameObject) { Name = gameName });
        return gameObject;
    }

    // Instances of an object that only differ in where they are and how they're turned are one prefab: in the folder of the chunk it's
    // first found in, or Global for the global package's objects, named after the object, without its links to the chunk's instances
    [AvaloniaFact]
    public void EveryDifferentObjectInstanceBecomesAPrefab()
    {
        var beach = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach", package: _project.Project.Ps2Package);
        var cove = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/cove" }, "cove", package: _project.Project.Ps2Package);
        var wumpa = AddObject("WUMPA_1", "WUMPA", _project.Project.GlobalPackagePS2);
        var crab = AddObject("_Beach_act_CRAB_12", "|Beach|act_CRAB", _project.Project.Ps2Package);
        var copy = AddObject("_Beach_ActorCopy_act_SPIKES_13", "|Beach|ActorCopy_act_SPIKES", _project.Project.Ps2Package);
        var impact = AddObject("_Beach_IMPACT_SOUND_14", "|Beach|IMPACT_SOUND", _project.Project.Ps2Package);
        var spot = _project.Add(new Position { Chunk = beach.AdditionalPath!, LayoutID = 0 }, "Spot", package: _project.Project.Ps2Package);
        spot.SetData(new PositionData(spot));
        AddInstance(beach, "Wumpa A", wumpa, new Vector3(1, 0, 0));
        AddInstance(beach, "Wumpa B", wumpa, new Vector3(5, 2, 0));
        AddInstance(beach, "Wumpa C", wumpa, new Vector3(9, 0, 0), data => data.FloatProperties = [2.0f]);
        AddInstance(beach, "Crab", crab, new Vector3(0, 0, 3), data => data.Positions = [spot.URI]);
        AddInstance(cove, "Crab", crab, new Vector3(7, 0, 3), data => data.Positions = [spot.URI]);
        AddInstance(cove, "Hiding crab", crab, new Vector3(4, 0, 3), data => data.StateFlags = Twinsanity.TwinsanityInterchange.Enumerations.Enums.InstanceState.Visible);
        AddInstance(cove, "Spikes", copy, new Vector3(0, 0, 0));
        AddInstance(cove, "Sound", impact, new Vector3(0, 0, 0));

        Assert.Equal((6, 0), InstancePrefabs.Make(_project.Project, _library));

        Assert.Equal(["Global", "levels"], _library.Folders(string.Empty));
        Assert.Equal(["WUMPA", "WUMPA 2"], _library.Load(InstancePrefabs.GlobalFolder).Select(prefab => prefab.Name));
        var crabPrefab = Assert.Single(_library.Load("levels/earth/hub/beach"));
        Assert.Equal("CRAB", crabPrefab.Name);
        // Copies of actors are named after what they copy, names that only have the letters in them stay
        Assert.Equal(["CRAB", "IMPACT_SOUND", "SPIKES"], _library.Load("levels/earth/hub/cove").Select(prefab => prefab.Name));
        // Its link to the chunk's position is left out, it stands at the cursor wherever it's placed
        var data = crabPrefab.Data.ToObject<ObjectInstanceData>()!;
        Assert.Empty(data.Positions);
        Assert.Equal((0.0f, 0.0f, 0.0f), (data.Position.X, data.Position.Y, data.Position.Z));
        Assert.Equal((0.0f, 0.0f, 0.0f), (data.Rotation.X, data.Rotation.Y, data.Rotation.Z));
        Assert.Equal(PrefabKind.Instance, crabPrefab.Kind);
        Assert.Equal("PS2", crabPrefab.Platform);

        // Made again, the library has them all already
        Assert.Equal((0, 6), InstancePrefabs.Make(_project.Project, _library));
        Assert.Equal(6, _library.Load().Count);
    }

    // Pressing From the instances again only makes what the library has no prefab of, wherever the user moved one, and a new prefab
    // doesn't take the name of one the folder has
    [AvaloniaFact]
    public void PrefabsTheLibraryHasAreNotMadeAgain()
    {
        var beach = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach", package: _project.Project.Ps2Package);
        var crab = AddObject("_Beach_act_CRAB_12", "|Beach|act_CRAB", _project.Project.Ps2Package);
        var impact = AddObject("_Beach_IMPACT_SOUND_14", "|Beach|IMPACT_SOUND", _project.Project.Ps2Package);
        AddInstance(beach, "Crab", crab, new Vector3(0, 0, 3));
        AddInstance(beach, "Sound", impact, new Vector3(0, 0, 0));
        Assert.Equal((2, 0), InstancePrefabs.Make(_project.Project, _library));

        AddInstance(beach, "Big crab", crab, new Vector3(4, 0, 3), data => data.FloatProperties = [2.0f]);
        Assert.Equal((1, 2), InstancePrefabs.Make(_project.Project, _library));
        Assert.Equal(["CRAB", "CRAB 2", "IMPACT_SOUND"], _library.Load("levels/earth/hub/beach").Select(prefab => prefab.Name));

        _library.Move(_library.Load("levels/earth/hub/beach").Single(prefab => prefab.Name == "CRAB 2"), "Mine");
        _library.Delete(_library.Load("levels/earth/hub/beach").Single(prefab => prefab.Name == "IMPACT_SOUND"));
        Assert.Equal((1, 2), InstancePrefabs.Make(_project.Project, _library));
        Assert.Equal(["CRAB", "IMPACT_SOUND"], _library.Load("levels/earth/hub/beach").Select(prefab => prefab.Name));
        Assert.Equal(["CRAB 2"], _library.Load("Mine").Select(prefab => prefab.Name));
    }

    // The folders are tiles before the prefabs, with the project tree's folder icon: a double click opens one, Backspace goes back up
    [AvaloniaFact]
    public void FoldersAreTilesOpenedWithADoubleClick()
    {
        _library.Save(CratePrefab("Crate"));
        _library.Save(CratePrefab("Nitro", "Global/Crates"));
        var panel = new PrefabsViewModel(new ScenesEditorsViewModel(), new EventAggregator(), new PrefabPictures());
        var view = new PrefabsView { DataContext = panel };
        var window = new Window { Content = view, Width = 600, Height = 500 };
        window.Show();
        panel.Refresh();
        // The clicks are hit tested by what the compositor got last, which waits for the render timer (see PrefabDragAndSearchTests)
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();

        var list = view.GetVisualDescendants().OfType<ListBox>().Single(box => box.Name == "PrefabList");
        var tiles = list.GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Assert.Equal(["Global", "Crate"], tiles.Select(tile => tile.DataContext switch
        {
            PrefabFolderEntry folder => folder.Name,
            PrefabEntry prefab => prefab.Name,
            _ => "?"
        }));
        var icon = tiles[0].GetVisualDescendants().OfType<Image>().Single(image => image.IsVisible);
        Assert.Same(TT_Lab.Util.MiscUtils.GetLabIcon("Folder"), icon.Source);

        var point = tiles[0].TranslatePoint(new Point(tiles[0].Bounds.Width / 2, 40), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.Equal(string.Empty, panel.CurrentFolder);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Global", panel.CurrentFolder);

        Dispatcher.UIThread.RunJobs();
        var crates = list.GetVisualDescendants().OfType<ListBoxItem>().Single();
        crates.Focus();
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Assert.Equal(string.Empty, panel.CurrentFolder);
        window.Close();
    }
}
