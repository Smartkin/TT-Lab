using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Composite;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

// Another program writing an asset's file (a text editor saving a script, Blender exporting a model over its file) makes the editors
// showing the asset again from the file, through the project's watcher like in TT Lab; TT Lab's own saves stay as they are
[Collection(ProjectCollection.Name)]
public sealed class ExternalChangeTests : IDisposable
{
    private const string Script = "Root.AssetData.Graph";
    private static readonly string EditedElsewhere = BehaviourGraphData.Template.Replace("COM_RENAME_ME", "COM_EDITED_ELSEWHERE");

    private readonly TestProject _project = new();
    private readonly Folder _folder;
    private readonly List<string> _asked = [];
    private bool _reload;

    public ExternalChangeTests()
    {
        var package = _project.Project.GlobalPackagePS2;
        // Projects list the disc's music and videos in their tree, rows of no package, whose paths failed every reload of a real project
        var music = Path.Combine(_project.Project.ProjectPath, "disc", "ps2", "Crash6");
        Directory.CreateDirectory(music);
        File.WriteAllBytes(Path.Combine(music, "Music.mh"), [1]);
        File.WriteAllBytes(Path.Combine(music, "Music.mb"), [2]);
        _project.BuildProjectTree(Path.Combine(package.Name, "Code"));
        _folder = _project.GetFolder(package, "Code");
        // Opened the way TT Lab opens projects, nothing waits for a build
        Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
    }

    public void Dispose() => _project.Dispose();

    private T Create<T>(string name, Func<IAsset, AssetCreationStatus> dataCreator) where T : IAsset
    {
        return (T)AssetFactory.CreateAsset(typeof(T), _folder, name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<T>(), dataCreator)!;
    }

    private (ResourcesEditorsViewModel Resources, Window Window) ShowEditors()
    {
        var resources = new ResourcesEditorsViewModel();
        var editors = new EditorsViewModel(new ScenesEditorsViewModel(), resources, Locator.Current.GetService<ProjectManager>()!);
        editors.AskToReloadChangedFiles = what =>
        {
            _asked.Add(what);
            return Task.FromResult(_reload);
        };
        var window = new Window { Content = new EditorsViewerView { DataContext = resources }, Width = 1200, Height = 800 };
        window.Show();
        return (resources, window);
    }

    private static async Task<TabbedEditorViewModel> Open(ResourcesEditorsViewModel resources, IAsset asset)
    {
        resources.OpenEditor(asset);
        var tab = resources.Tabs.Single(tab => tab.EditableResource == asset.URI);
        await WaitUntil(() => tab.IsLoaded);
        Assert.True(tab.IsLoaded);
        return tab;
    }

    private static async Task WaitUntil(Func<bool> condition, int milliseconds = 5000)
    {
        for (var waited = 0; waited < milliseconds && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }
    }

    // The watcher waits for the writes to stop before telling, a change that didn't come has had more than its time
    private static Task LongerThanTheWatcherWaits() => Task.Delay(1500);

    private static string ShownScript(TabbedEditorViewModel tab) => (string)tab.Document!.PropertyGraph.Find(Script)!.GetValue()!;

    [AvaloniaFact]
    public async Task AScriptAnotherProgramSavedShowsInItsEditor()
    {
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, graph);
        var document = tab.Document;

        await File.WriteAllTextAsync(graph.FullDataPath, EditedElsewhere);
        await WaitUntil(() => tab.IsLoaded && tab.Document != document);

        Assert.NotSame(document, tab.Document);
        Assert.Equal(EditedElsewhere, ShownScript(tab));
        Assert.False(tab.Document!.IsDirty);
        Assert.Equal("COM_TEST", tab.Title);
        Assert.Empty(_asked);
        window.Close();
    }

    [AvaloniaFact]
    public async Task SavingInTTLabIsNoChangeFromElsewhere()
    {
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, graph);
        var document = tab.Document!;
        document.PropertyGraph.Find(Script)!.SetValue(EditedElsewhere);

        tab.SaveTab();
        await LongerThanTheWatcherWaits();

        Assert.Same(document, tab.Document);
        Assert.True(document.History.CanUndo);
        Assert.Equal(EditedElsewhere, await File.ReadAllTextAsync(graph.FullDataPath));
        window.Close();
    }

    // A chunk shows its scenery, a game object the scripts of its slots: what an editor shows changing makes it again as well
    [AvaloniaFact]
    public async Task AnEditorIsMadeAgainWhenWhatItShowsChanged()
    {
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var crash = Create<GameObject>("Crash", AssetDataFactory.CreateGameObjectData);
        ((IAsset)crash).GetData<GameObjectData>().BehaviourSlots.Add(graph.URI);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, crash);
        var document = tab.Document!;
        const string slot = "Root.AssetData.BehaviourSlots[0][data]";
        const string slotScript = $"{slot}.AssetData.Graph";
        Assert.Equal(BehaviourGraphData.Template, document.PropertyGraph.Find(slotScript)!.GetValue());
        document.OpenInspector(document.PropertyGraph.Find(slot));

        await File.WriteAllTextAsync(graph.FullDataPath, EditedElsewhere);
        await WaitUntil(() => tab.IsLoaded && tab.Document is { Inspector: not null } shown && shown != document);

        Assert.Equal(EditedElsewhere, tab.Document!.PropertyGraph.Find(slotScript)!.GetValue());
        // What was inspected is again
        Assert.Equal(slot, tab.Document.Inspector?.Property.Path);
        window.Close();
    }

    // Both changed the script: the editor asks, kept it stays as it is with everything showing the same script, saving writes over the file
    [AvaloniaFact]
    public async Task AnEditorWithUnsavedChangesToTheChangedFileAsksAndKeepsThem()
    {
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var crash = Create<GameObject>("Crash", AssetDataFactory.CreateGameObjectData);
        ((IAsset)crash).GetData<GameObjectData>().BehaviourSlots.Add(graph.URI);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, graph);
        var crashTab = await Open(resources, crash);
        var document = tab.Document!;
        var crashDocument = crashTab.Document!;
        var edited = BehaviourGraphData.Template.Replace("COM_RENAME_ME", "COM_EDITED_IN_TT_LAB");
        document.PropertyGraph.Find(Script)!.SetValue(edited);

        await File.WriteAllTextAsync(graph.FullDataPath, EditedElsewhere);
        await WaitUntil(() => _asked.Count > 0);
        await LongerThanTheWatcherWaits();

        Assert.Equal([
            "COM_TEST changed outside TT Lab, and COM_TEST has unsaved changes to it.\n\n" +
            "Reload: COM_TEST loses its unsaved changes and shows the file.\n" +
            "Keep my changes: COM_TEST stays as it is, saving writes over the file."], _asked);
        Assert.Same(document, tab.Document);
        Assert.True(document.IsDirty);
        Assert.Equal(edited, ShownScript(tab));
        Assert.Equal(edited, ((IAsset)graph).GetData<BehaviourGraphData>().Graph);
        Assert.Same(crashDocument, crashTab.Document);

        // Saving writes over the other program's script, which isn't a change from elsewhere either
        tab.SaveTab();
        await LongerThanTheWatcherWaits();
        Assert.Equal(edited, await File.ReadAllTextAsync(graph.FullDataPath));
        Assert.Same(document, tab.Document);
        Assert.Same(crashDocument, crashTab.Document);
        Assert.Single(_asked);
        window.Close();
    }

    // A chunk's moved instances while its scenery comes from Blender, a game object's edits while the script of its slot changes: the
    // changes stay unsaved, undoable, in the editor made again
    [AvaloniaFact]
    public async Task UnsavedChangesToWhatDidNotChangeStayThroughAReload()
    {
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var crash = Create<GameObject>("Crash", AssetDataFactory.CreateGameObjectData);
        ((IAsset)crash).GetData<GameObjectData>().BehaviourSlots.Add(graph.URI);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, crash);
        var document = tab.Document!;
        const string pack = "Root.AssetData.BehaviourPack";
        var packBefore = document.PropertyGraph.Find(pack)!.GetValue();
        document.PropertyGraph.Find(pack)!.SetValue("EDITED_PACK");

        await File.WriteAllTextAsync(graph.FullDataPath, EditedElsewhere);
        await WaitUntil(() => tab.IsLoaded && tab.Document != document);

        Assert.Empty(_asked);
        var reloaded = tab.Document!;
        Assert.NotSame(document, reloaded);
        Assert.Equal(EditedElsewhere, reloaded.PropertyGraph.Find("Root.AssetData.BehaviourSlots[0][data].AssetData.Graph")!.GetValue());
        Assert.Equal("EDITED_PACK", reloaded.PropertyGraph.Find(pack)!.GetValue());
        Assert.True(reloaded.IsDirty);
        Assert.Equal("Crash*", tab.Title);
        reloaded.Undo();
        Assert.Equal(packBefore, reloaded.PropertyGraph.Find(pack)!.GetValue());
        Assert.False(reloaded.IsDirty);
        reloaded.Redo();
        reloaded.Save();
        Assert.Contains("EDITED_PACK", await File.ReadAllTextAsync(crash.FullDataPath));
        window.Close();
    }

    // Undoing a step into what got read again would put the old script into the other program's
    [AvaloniaFact]
    public async Task AHistoryWithStepsInWhatGotReadAgainStartsOver()
    {
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var crash = Create<GameObject>("Crash", AssetDataFactory.CreateGameObjectData);
        ((IAsset)crash).GetData<GameObjectData>().BehaviourSlots.Add(graph.URI);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, crash);
        var document = tab.Document!;
        const string slotScript = "Root.AssetData.BehaviourSlots[0][data].AssetData.Graph";
        document.PropertyGraph.Find(slotScript)!.SetValue(BehaviourGraphData.Template.Replace("COM_RENAME_ME", "COM_EDITED_IN_TT_LAB"));
        document.Save();

        await File.WriteAllTextAsync(graph.FullDataPath, EditedElsewhere);
        await WaitUntil(() => tab.IsLoaded && tab.Document != document);

        var reloaded = tab.Document!;
        Assert.Equal(EditedElsewhere, reloaded.PropertyGraph.Find(slotScript)!.GetValue());
        Assert.False(reloaded.History.CanUndo);
        Assert.Equal("Reloaded COM_TEST", reloaded.History.Root.Description);
        Assert.False(reloaded.IsDirty);
        window.Close();
    }

    [AvaloniaFact]
    public async Task AnEditorWithUnsavedChangesLosesThemWhenReloaded()
    {
        _reload = true;
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, graph);
        var document = tab.Document!;
        document.PropertyGraph.Find(Script)!.SetValue(BehaviourGraphData.Template.Replace("COM_RENAME_ME", "COM_EDITED_IN_TT_LAB"));

        await File.WriteAllTextAsync(graph.FullDataPath, EditedElsewhere);
        await WaitUntil(() => tab.IsLoaded && tab.Document != document);

        Assert.Single(_asked);
        Assert.Equal(EditedElsewhere, ShownScript(tab));
        Assert.False(tab.Document!.IsDirty);
        Assert.Equal("COM_TEST", tab.Title);
        window.Close();
    }

    // A file its program failed writing, or another kind of model exported over the asset: the editors keep what they have, unsaved
    // changes included, and the file isn't taken for TT Lab's
    [AvaloniaFact]
    public async Task AFileThatDoesNotReadLeavesTheEditorsAsTheyAre()
    {
        var crash = Create<GameObject>("Crash", AssetDataFactory.CreateGameObjectData);
        var (resources, window) = ShowEditors();
        var tab = await Open(resources, crash);
        var document = tab.Document!;
        document.PropertyGraph.Find("Root.AssetData.BehaviourPack")!.SetValue("EDITED_PACK");
        var data = ((IAsset)crash).GetData<GameObjectData>();

        await File.WriteAllTextAsync(crash.FullDataPath, "{ \"Name\": \"Cra");
        await LongerThanTheWatcherWaits();
        await LongerThanTheWatcherWaits();

        Assert.Empty(_asked);
        Assert.Same(document, tab.Document);
        Assert.True(document.IsDirty);
        Assert.Same(data, ((IAsset)crash).GetData<GameObjectData>());
        Assert.Equal("EDITED_PACK", data.BehaviourPack);
        Assert.False(AssetFileStamps.IsAsRecorded(crash.FullDataPath));
        window.Close();
    }

    // A change to an asset no editor shows lets go of its data, the next read takes the file
    [AvaloniaFact]
    public async Task DataNoEditorShowsIsReadAgain()
    {
        var graph = Create<BehaviourGraph>("COM_TEST", AssetDataFactory.CreateBehaviourData);
        var (_, window) = ShowEditors();
        Assert.Equal(BehaviourGraphData.Template, ((IAsset)graph).GetData<BehaviourGraphData>().Graph);

        await File.WriteAllTextAsync(graph.FullDataPath, EditedElsewhere);
        await WaitUntil(() => !graph.IsLoaded);

        Assert.Equal(EditedElsewhere, ((IAsset)graph).GetData<BehaviourGraphData>().Graph);
        window.Close();
    }

    [AvaloniaFact]
    public void AFileIsTheOneTTLabLastReadOrWroteUntilAnotherProgramWritesIt()
    {
        var path = Path.Combine(_project.Root, "stamped.lab");
        File.WriteAllText(path, "first");
        Assert.False(AssetFileStamps.IsAsRecorded(path));

        AssetFileStamps.Record(path);
        Assert.True(AssetFileStamps.IsAsRecorded(path));
        Assert.True(AssetFileStamps.IsAsRecorded(Path.Combine(_project.Root, ".", "stamped.lab")));

        File.WriteAllText(path, "second, longer");
        Assert.False(AssetFileStamps.IsAsRecorded(path));

        // The same length written later
        AssetFileStamps.Record(path);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddSeconds(1));
        Assert.False(AssetFileStamps.IsAsRecorded(path));

        File.Delete(path);
        Assert.False(AssetFileStamps.IsAsRecorded(path));
    }

    // Windows keeps write times to its timer's tick: another program writing as many bytes right after TT Lab saved got the same length
    // and time, and the editors never saw it
    [AvaloniaFact]
    public void AWriteOfTheSameLengthAtTheSameTimeIsToldApartByItsContent()
    {
        var path = Path.Combine(_project.Root, "same_tick.lab");
        File.WriteAllText(path, "COM_EDITED_IN_TT_LAB");
        AssetFileStamps.Record(path);
        var writeTime = File.GetLastWriteTimeUtc(path);

        File.WriteAllText(path, "COM_EDITED_ELSEWHERE");
        File.SetLastWriteTimeUtc(path, writeTime);

        Assert.False(AssetFileStamps.IsAsRecorded(path));
    }

    // Builds write plenty, none of it an asset's data
    [AvaloniaFact]
    public async Task TheWatcherOnlyTellsAboutAssetsDataFiles()
    {
        var written = new List<string>();
        using var watcher = new ProjectTreeWatcher(_project.Project.ProjectPath, () => { }, paths => written.AddRange(paths));
        var script = Path.Combine(_project.AssetsPath, "script.lab");
        Directory.CreateDirectory(Path.Combine(_project.Project.ProjectPath, "build"));
        await File.WriteAllTextAsync(Path.Combine(_project.Project.ProjectPath, "build", "chunk.lab"), "built");
        await File.WriteAllTextAsync(Path.Combine(_project.AssetsPath, "asset.json"), "{}");
        await File.WriteAllTextAsync(script, "written");

        await WaitUntil(() => written.Count > 0);
        await LongerThanTheWatcherWaits();

        Assert.Equal([script], written);
    }
}
