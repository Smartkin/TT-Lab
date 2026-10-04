using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Avalonia.Controls.Recycling;
using Avalonia.Threading;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels;

public class EditorTabsFactory : DockFactoryBase
{
    private static readonly FieldInfo? ActivationOrderField = typeof(DockControl).GetField("_activationOrder", BindingFlags.Instance | BindingFlags.NonPublic);
    
    private IDocumentDock? _editorsDock;

    public event Action<TabbedEditorViewModel>? EditorClosed;

    static EditorTabsFactory()
    {
        Debug.Assert(ActivationOrderField != null, "Dock's DockControl no longer has _activationOrder, closed editors will leak through its activation history");
    }

    public IRootDock? EditorsLayout { get; private set; }

    public override IRootDock CreateLayout()
    {
        _editorsDock = new DocumentDock
        {
            Id = "EditorsDock",
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>()
        };

        var root = CreateRootDock();
        root.Id = "EditorsRoot";
        root.IsCollapsable = false;
        root.VisibleDockables = CreateList<IDockable>(_editorsDock);
        root.ActiveDockable = _editorsDock;
        root.DefaultDockable = _editorsDock;
        root.Factory = this;
        EditorsLayout = root;

        return root;
    }

    public IEnumerable<TabbedEditorViewModel> GetEditors()
    {
        return EditorsLayout?.EnumerateDockables().Select(entry => entry.Dockable).OfType<TabbedEditorViewModel>() ?? [];
    }

    // Editors can be split into several docks, the one last interacted with is the focused one
    public TabbedEditorViewModel? GetActiveEditor()
    {
        return EditorsLayout?.FocusedDockable as TabbedEditorViewModel ?? _editorsDock?.ActiveDockable as TabbedEditorViewModel;
    }

    public void AddEditor(TabbedEditorViewModel editor)
    {
        AddDockable(_editorsDock!, editor);
        ActivateEditor(editor);
    }

    public void ActivateEditor(TabbedEditorViewModel editor)
    {
        SetActiveDockable(editor);
        if (editor.Owner is IDock owner)
        {
            SetFocusedDockable(owner, editor);
        }
    }

    public void RemoveEditor(TabbedEditorViewModel editor)
    {
        RemoveDockable(editor, true);
        ForgetDockControlReferences(editor);
    }

    public override async void CloseDockable(IDockable dockable)
    {
        if (dockable is not TabbedEditorViewModel editor)
        {
            base.CloseDockable(dockable);
            return;
        }

        var canClose = await editor.CloseTab();
        if (!canClose)
        {
            return;
        }

        base.CloseDockable(dockable);
        ForgetDockControlReferences(editor);
        EditorClosed?.Invoke(editor);

        // Collecting right here would still see the editor through this method's locals and its views that haven't detached yet
        Dispatcher.UIThread.Post(MiscUtils.CollectReleasedMemory, DispatcherPriority.Background);
    }

    private void ForgetDockControlReferences(IDockable dockable)
    {
        foreach (var dockControl in DockControls.OfType<DockControl>())
        {
            // Dock caches a view per dockable and only prunes it when a whole DockControl is torn down
            (ControlRecyclingDataTemplate.GetControlRecycling(dockControl) as ControlRecycling)?.Remove(dockable);
            // HACK: Dock records every activated dockable and only prunes that history when showing its dockable selector, there's no public way to do it
            (ActivationOrderField?.GetValue(dockControl) as IDictionary<IDockable, long>)?.Remove(dockable);
        }
    }

    public override void CloseWindow(IDockWindow window)
    {
        if (window.Layout == null || _editorsDock == null)
        {
            return;
        }

        // Dock editors back instead of closing them, the window is already gone so an unsaved changes prompt couldn't be cancelled
        foreach (var (_, dockable) in window.Layout.EnumerateDockables().ToList())
        {
            if (dockable is not TabbedEditorViewModel editor || !editor.IsDescendantOf(window.Layout))
            {
                continue;
            }

            RemoveDockable(editor, false);
            AddDockable(_editorsDock, editor);
        }
    }
}
