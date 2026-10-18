using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dock.Model.Controls;
using Dock.Model.ReactiveUI.Controls;
using ReactiveUI;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels;

public abstract class EditorsViewerViewModel : Document
{
    public event Action<TabbedEditorViewModel>? EditorClosed;

    /// <summary>
    /// One of the editors or the viewer itself got activated or focused, even when it already was the active one
    /// </summary>
    public event Action? Used;

    // Every viewer's uses counted together, the one used last has the highest
    private static long _uses;

    private TabbedEditorViewModel? _activeEditor;

    protected EditorsViewerViewModel(string id, string title)
    {
        Id = id;
        Title = title;

        TabsFactory = new EditorTabsFactory();
        TabsFactory.EditorClosed += editor =>
        {
            UpdateActiveEditor();
            EditorClosed?.Invoke(editor);
        };
        TabsFactory.ActiveDockableChanged += (_, _) =>
        {
            UpdateActiveEditor();
            RaiseUsed();
        };
        TabsFactory.FocusedDockableChanged += (_, _) =>
        {
            UpdateActiveEditor();
            RaiseUsed();
        };
        TabsFactory.DockableRemoved += (_, _) => UpdateActiveEditor();
        EditorsLayout = TabsFactory.CreateLayout();
        TabsFactory.InitLayout(EditorsLayout);

        SaveActiveEditorCommand = ReactiveCommand.Create(() => TabsFactory.GetActiveEditor()?.SaveTab());
        CloseActiveEditorCommand = ReactiveCommand.Create(() => TabsFactory.GetActiveEditor()?.RequestClose());
        UndoActiveEditorCommand = ReactiveCommand.Create(() => TabsFactory.GetActiveEditor()?.Document?.Undo());
        RedoActiveEditorCommand = ReactiveCommand.Create(() => TabsFactory.GetActiveEditor()?.Document?.Redo());
    }

    public ReactiveCommand<Unit, Unit> SaveActiveEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> CloseActiveEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoActiveEditorCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoActiveEditorCommand { get; }

    public EditorTabsFactory TabsFactory { get; }

    public IRootDock EditorsLayout { get; }

    public IEnumerable<TabbedEditorViewModel> Tabs => TabsFactory.GetEditors();

    /// <summary>
    /// Editor last interacted with, panels outside the tabs show its parts
    /// </summary>
    public TabbedEditorViewModel? ActiveEditor
    {
        get => _activeEditor;
        private set => this.RaiseAndSetIfChanged(ref _activeEditor, value);
    }

    // Dock keeps pointing at a closed editor as the focused one
    private void UpdateActiveEditor()
    {
        var active = TabsFactory.GetActiveEditor();
        ActiveEditor = active != null && Tabs.Contains(active) ? active : Tabs.FirstOrDefault();
    }

    /// <summary>
    /// When the viewer was last used, counted over every viewer: 0 until it's used
    /// </summary>
    public long LastUsed { get; private set; }

    // The viewer itself got switched to or clicked into: its own tabs tell nothing then, the active one stays the active one
    internal void NoteUsed() => RaiseUsed();

    private void RaiseUsed()
    {
        LastUsed = Interlocked.Increment(ref _uses);
        Used?.Invoke();
    }

    /// <summary>
    /// The viewer of the two worked in last, the scenes' until either is used. What panels outside the tabs follow: the History panel,
    /// the Project Tree's Locate and the Discord presence
    /// </summary>
    internal static EditorsViewerViewModel LastUsedOf(EditorsViewerViewModel scenes, EditorsViewerViewModel resources)
    {
        return resources.LastUsed > scenes.LastUsed ? resources : scenes;
    }

    /// <summary>
    /// The active editor of the viewer worked in last, the other one's when that one has none open
    /// </summary>
    internal static TabbedEditorViewModel? CurrentEditor(EditorsViewerViewModel scenes, EditorsViewerViewModel resources)
    {
        var used = LastUsedOf(scenes, resources);
        var other = used == scenes ? resources : scenes;
        return used.ActiveEditor ?? other.ActiveEditor;
    }

    /// <summary>
    /// The viewer worked in last, again whenever either of them is used
    /// </summary>
    internal static IObservable<EditorsViewerViewModel> FollowLastUsed(EditorsViewerViewModel scenes, EditorsViewerViewModel resources)
    {
        return Observable.Defer(() => Observable.Merge(UsesOf(scenes), UsesOf(resources))
            .Select(_ => LastUsedOf(scenes, resources))
            .StartWith(LastUsedOf(scenes, resources)));
    }

    private static IObservable<Unit> UsesOf(EditorsViewerViewModel viewer)
    {
        return Observable.FromEvent(handler => viewer.Used += handler, handler => viewer.Used -= handler);
    }

    public void OpenEditor(IAsset asset)
    {
        var openedTab = Tabs.FirstOrDefault(tab => tab.EditableResource == asset.URI);
        if (openedTab != null)
        {
            // Opening the active tab again changes nothing Dock tells about, it's still the editor turned to
            TabsFactory.ActivateEditor(openedTab);
            RaiseUsed();
            return;
        }

        TabsFactory.AddEditor(CreateTab(asset));
    }

    public async Task<bool> CloseAllTabs(bool discardChanges = false)
    {
        foreach (var tab in Tabs.ToList())
        {
            if (!await tab.CloseTab(discardChanges))
            {
                return false;
            }

            TabsFactory.RemoveEditor(tab);
        }

        return true;
    }

    public async Task<bool> CloseTabsReferencing(IReadOnlySet<LabURI> assets)
    {
        foreach (var tab in Tabs.Where(tab => tab.GetReferencedAssets().Any(assets.Contains)).ToList())
        {
            if (!await tab.CloseTab())
            {
                return false;
            }

            TabsFactory.RemoveEditor(tab);
        }

        return true;
    }

    public void Save()
    {
        foreach (var tab in Tabs)
        {
            tab.SaveTab();
        }
    }

    protected abstract TabbedEditorViewModel CreateTab(IAsset asset);
}
