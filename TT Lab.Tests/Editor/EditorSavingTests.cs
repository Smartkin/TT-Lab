using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors;
using TT_Lab.Views;
using TT_Lab.Views.Composite;

namespace TT_Lab.Tests.Editor;

[Collection(ProjectCollection.Name)]
public sealed class EditorSavingTests : IDisposable
{
    private const string BehaviourPack = "Root.AssetData.BehaviourPack";

    private readonly TestProject _project = new();
    private readonly Folder _folder;

    public EditorSavingTests()
    {
        var package = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(package.Name, "Code"));
        _folder = _project.GetFolder(package, "Code");
    }

    public void Dispose() => _project.Dispose();

    private T Create<T>(string name, Func<IAsset, AssetCreationStatus> dataCreator) where T : IAsset
    {
        return (T)AssetFactory.CreateAsset(typeof(T), _folder, name, string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<T>(), dataCreator)!;
    }

    private GameObject CreateGameObject(string name) => Create<GameObject>(name, AssetDataFactory.CreateGameObjectData);

    private static bool IsSaved(IAsset asset, string text) => File.ReadAllText(asset.FullDataPath).Contains(text);

    private static async Task<TabbedEditorViewModel> WaitUntilLoaded(TabbedEditorViewModel tab)
    {
        for (var i = 0; i < 250 && !tab.IsLoaded; i++)
        {
            await Task.Delay(20);
        }

        Assert.True(tab.IsLoaded);
        // Lets the views of the loaded document get created
        await Task.Delay(50);
        return tab;
    }

    // Closing a tab waits for its document, which a busy machine gets to after any fixed delay
    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            await Task.Delay(20);
        }
    }

    private static string Dump(UndoHistory.Entry entry) => $"{entry.Description}{(entry.IsOpen ? "*" : "")}{(entry.Children.Count > 0 ? " > [" + string.Join(" | ", entry.Children.Select(Dump)) + "]" : "")}";

    private static Window Show(EditorsViewerViewModel viewer)
    {
        var window = new Window { Content = new EditorsViewerView { DataContext = viewer }, Width = 1200, Height = 800 };
        window.Show();
        return window;
    }

    // Clicking a label, which can't take the focus itself, still has to move the focus into the editor
    private static void ClickIntoEditor(Window window)
    {
        var editor = window.GetVisualDescendants().OfType<ResourceEditorTabView>().Single(view => view.IsEffectivelyVisible);
        var label = editor.GetVisualDescendants().OfType<TextBlock>().First(text => text.IsEffectivelyVisible && text.Bounds.Width > 0);
        var point = label.TranslatePoint(new Point(2, 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Assert.True(editor.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void SavingClearsTheUnsavedChangesMark()
    {
        var crash = CreateGameObject("Crash");
        // Editor tabs load the data before building the document the same way
        ((IAsset)crash).GetData<GameObjectData>();
        var document = new DocumentViewModel(crash);
        document.PropertyGraph.Find(BehaviourPack)!.SetValue("EDITED_PACK");
        Assert.True(document.IsDirty);

        document.Save();

        Assert.False(document.IsDirty);
        Assert.True(IsSaved(crash, "EDITED_PACK"));
    }

    // Linked assets are edited within the document of the asset linking them
    [AvaloniaFact]
    public void SavingSavesEditedLinkedAssets()
    {
        var ogi = Create<OGI>("Skeleton", AssetDataFactory.CreateOgiData);
        var crash = CreateGameObject("Crash");
        ((IAsset)crash).GetData<GameObjectData>().ModelSlots.Add(new ModelSlot { Ogi = ogi.URI });
        var document = new DocumentViewModel(crash);

        document.PropertyGraph.Find("Root.AssetData.ModelSlots[0].Ogi[data].Alias")!.SetValue("Renamed skeleton");
        document.Save();

        Assert.Contains("Renamed skeleton", File.ReadAllText(Path.Combine(ogi.FullPath, $"{ogi.Name}.json")));
    }

    [AvaloniaFact]
    public async Task CtrlSSavesTheEditor()
    {
        var crash = CreateGameObject("Crash");
        var viewer = new ResourcesEditorsViewModel();
        var window = Show(viewer);
        viewer.OpenEditor(crash);
        var tab = await WaitUntilLoaded(viewer.Tabs.Single());
        ClickIntoEditor(window);
        tab.Document!.PropertyGraph.Find(BehaviourPack)!.SetValue("EDITED_PACK");
        Assert.Equal("Crash*", tab.Title);

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);

        Assert.True(IsSaved(crash, "EDITED_PACK"));
        Assert.Equal("Crash", tab.Title);
    }

    // A package's settings open as a tab like any asset, closing it used to crash
    [AvaloniaFact]
    public async Task PackageSettingsOpenAndClose()
    {
        var viewer = new ResourcesEditorsViewModel();
        var window = Show(viewer);
        viewer.OpenEditor(_project.Project.GlobalPackagePS2);
        var tab = await WaitUntilLoaded(viewer.Tabs.Single());
        Assert.NotNull(tab.Document);
        ClickIntoEditor(window);

        window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
        await WaitUntil(() => !viewer.Tabs.Any());

        Assert.Empty(viewer.Tabs);
        Assert.True(_project.AssetManager.DoesAssetExist(_project.Project.GlobalPackagePS2.URI));
    }

    [AvaloniaFact]
    public async Task CtrlWClosesTheEditor()
    {
        var viewer = new ResourcesEditorsViewModel();
        var window = Show(viewer);
        viewer.OpenEditor(CreateGameObject("Crash"));
        await WaitUntilLoaded(viewer.Tabs.Single());
        ClickIntoEditor(window);

        window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
        await WaitUntil(() => !viewer.Tabs.Any());

        Assert.Empty(viewer.Tabs);
    }

    // Closing an editor used to take the focus with it, the next Ctrl+W did nothing until something got clicked
    [AvaloniaFact]
    public async Task CtrlWClosesEditorsOneAfterAnother()
    {
        var viewer = new ResourcesEditorsViewModel();
        var window = Show(viewer);
        foreach (var name in new[] { "Crash", "Aku", "Coco" })
        {
            viewer.OpenEditor(CreateGameObject(name));
        }

        foreach (var tab in viewer.Tabs.ToList())
        {
            await WaitUntilLoaded(tab);
        }

        ClickIntoEditor(window);

        for (var closed = 1; closed <= 3; closed++)
        {
            window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
            await WaitUntil(() => viewer.Tabs.Count() == 3 - closed);
            Assert.Equal(3 - closed, viewer.Tabs.Count());
        }
    }

    [AvaloniaFact]
    public async Task CtrlZAndCtrlYUndoAndRedoInTheEditor()
    {
        var crash = CreateGameObject("Crash");
        var viewer = new ResourcesEditorsViewModel();
        var window = Show(viewer);
        viewer.OpenEditor(crash);
        var tab = await WaitUntilLoaded(viewer.Tabs.Single());
        ClickIntoEditor(window);
        var pack = tab.Document!.PropertyGraph.Find(BehaviourPack)!;
        var original = pack.GetValue();
        pack.SetValue("EDITED_PACK");

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(original, pack.GetValue());
        Assert.Equal("Crash", tab.Title);

        window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
        Assert.Equal("EDITED_PACK", pack.GetValue());
        Assert.Equal("Crash*", tab.Title);
    }

    // The panel shows the undo tree of the editor last worked in, picking a step takes the document there
    [AvaloniaFact]
    public async Task HistoryPanelShowsTheEditorsTree()
    {
        var crash = CreateGameObject("Crash");
        var resources = new ResourcesEditorsViewModel();
        var history = new HistoryViewModel(new ScenesEditorsViewModel(), resources);
        Show(resources);
        resources.OpenEditor(crash);
        var tab = await WaitUntilLoaded(resources.Tabs.Single());
        var document = tab.Document!;
        Assert.Same(document, history.Document);
        var pack = document.PropertyGraph.Find(BehaviourPack)!;
        foreach (var value in new[] { "FIRST", "SECOND" })
        {
            using (document.History.BeginGroup())
            {
                pack.SetValue(value);
            }
        }

        Assert.Equal(["Opened", "Crash › BehaviourPack = \"FIRST\"", "Crash › BehaviourPack = \"SECOND\""], history.Entries.Select(entry => entry.Description));
        Assert.True(history.Entries[^1].IsCurrent);

        history.SelectedEntry = history.Entries[1];
        Assert.Equal("FIRST", pack.GetValue());
        Assert.True(history.Entries[1].IsCurrent);

        // Changing after undoing starts a branch, the undone step stays
        using (document.History.BeginGroup())
        {
            pack.SetValue("THIRD");
        }

        Assert.Equal(4, history.Entries.Count);
        Assert.Equal(0.4, history.Entries[2].Opacity);
        Assert.StartsWith("\u21B3", history.Entries[3].Description);
        Assert.True(history.Entries[3].Indent > 0);
    }

    // The code editor's Ctrl+Z is the document's history: the text follows it, and picking a step in the History panel changes the text
    [AvaloniaFact]
    public async Task UndoingInTheCodeEditorRestoresItsText()
    {
        var crash = CreateGameObject("Crash");
        var resources = new ResourcesEditorsViewModel();
        var history = new HistoryViewModel(new ScenesEditorsViewModel(), resources);
        var window = Show(resources);
        resources.OpenEditor(crash);
        var tab = await WaitUntilLoaded(resources.Tabs.Single());
        var pack = tab.Document!.PropertyGraph.Find(BehaviourPack)!;
        tab.Document.OpenInspector(tab.Document.PropertyGraph.Find("Root.AssetData"), pack);
        Dispatcher.UIThread.RunJobs();
        var editors = window.GetVisualDescendants().OfType<TextEditor>().ToList();
        Assert.True(editors.Count == 1, string.Join(", ", window.GetVisualDescendants().Select(visual => visual.GetType().Name).Distinct()));
        var editor = editors[0];
        var original = editor.Text;
        editor.TextArea.Focus();
        editor.CaretOffset = editor.Document.TextLength;
        foreach (var character in "xyz")
        {
            window.KeyTextInput(character.ToString());
        }

        Assert.Equal(original + "xyz", pack.GetValue());

        // Loading the text isn't a step, so one undo is back at the start and another changes nothing
        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.True(original == editor.Text, Dump(tab.Document.History.Root));
        Assert.Equal(original, pack.GetValue());
        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        Assert.Equal(original, editor.Text);

        window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
        Assert.Equal(original + "xyz", editor.Text);
        Assert.Equal(editor.Document.TextLength, editor.CaretOffset);

        history.SelectedEntry = history.Entries[0];
        Assert.Equal(original, editor.Text);
        Assert.False(tab.Document.IsDirty);
    }

    // Typing keeps changing the step the document is at, so the panel keeps its rows and only swaps that one
    [AvaloniaFact]
    public async Task HistoryPanelKeepsItsRowsWhileTyping()
    {
        var crash = CreateGameObject("Crash");
        var resources = new ResourcesEditorsViewModel();
        var history = new HistoryViewModel(new ScenesEditorsViewModel(), resources);
        var panel = new Window { Content = new HistoryView { DataContext = history }, Width = 400, Height = 300 };
        panel.Show();
        Show(resources);
        resources.OpenEditor(crash);
        var tab = await WaitUntilLoaded(resources.Tabs.Single());
        var pack = tab.Document!.PropertyGraph.Find(BehaviourPack)!;
        pack.SetValue("F");
        var rows = history.Entries;
        var typed = rows[^1];
        pack.SetValue("FI");
        pack.SetValue("FIR");
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.Same(rows, history.Entries);
        Assert.Equal(2, rows.Count);
        Assert.NotSame(typed, rows[^1]);
        Assert.Equal("Crash › BehaviourPack = \"FIR\"", rows[^1].Description);
        Assert.True(rows[^1].IsCurrent);
        Assert.Same(rows[^1], history.SelectedEntry);
        Assert.Same(rows[^1], panel.GetVisualDescendants().OfType<ListBox>().Single().SelectedItem);
        panel.Close();
    }

    [AvaloniaFact]
    public async Task ShortcutsWorkFromTheTabStrip()
    {
        var crash = CreateGameObject("Crash");
        var viewer = new ResourcesEditorsViewModel();
        var window = Show(viewer);
        viewer.OpenEditor(crash);
        var tab = await WaitUntilLoaded(viewer.Tabs.Single());
        tab.Document!.PropertyGraph.Find(BehaviourPack)!.SetValue("EDITED_PACK");
        window.GetVisualDescendants().OfType<InputElement>().First(element => element.GetType().Name == "DocumentTabStripItem").Focus();

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Control);
        Assert.True(IsSaved(crash, "EDITED_PACK"));

        window.KeyPressQwerty(PhysicalKey.W, RawInputModifiers.Control);
        await WaitUntil(() => !viewer.Tabs.Any());
        Assert.Empty(viewer.Tabs);
    }

    [AvaloniaFact]
    public async Task SaveAllSavesEveryEditor()
    {
        var crash = CreateGameObject("Crash");
        var aku = CreateGameObject("Aku");
        var resources = new ResourcesEditorsViewModel();
        var editors = new EditorsViewModel(new ScenesEditorsViewModel(), resources, Locator.Current.GetService<ProjectManager>()!);
        Show(resources);
        resources.OpenEditor(crash);
        resources.OpenEditor(aku);
        var tabs = resources.Tabs.ToList();
        foreach (var tab in tabs)
        {
            await WaitUntilLoaded(tab);
            tab.Document!.PropertyGraph.Find(BehaviourPack)!.SetValue($"EDITED_{tab.AssetName}");
        }

        editors.Save();

        Assert.True(IsSaved(crash, "EDITED_Crash"));
        Assert.True(IsSaved(aku, "EDITED_Aku"));
        Assert.All(tabs, tab => Assert.False(tab.Document!.IsDirty));
    }
}
