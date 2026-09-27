using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using Dock.Model.ReactiveUI.Controls;
using Newtonsoft.Json.Linq;
using TT_Lab.Extensions;

namespace TT_Lab.ViewModels;

public class DockFactory : Factory
{
    private const string DocumentsAreaPaneId = "DocumentsAreaPane";
    private const string DocumentsPaneId = "DocumentsPane";
    private const string LogPaneId = "LogPane";
    private const string ProjectTreePaneId = "ProjectTreePane";
    private const double LogPaneProportion = 0.15;
    private const double ProjectTreePaneProportion = 0.2;
    private static readonly HashSet<string> AllowedLayoutAssemblies = ["Dock.Model", "Dock.Model.ReactiveUI"];

    private readonly IDockSerializer _serializer;
    private readonly ScenesEditorsViewModel _scenes;
    private readonly ResourcesEditorsViewModel _resources;
    private readonly LogViewModel _log;
    private readonly ProjectTreeViewModel _projectTree;
    private readonly ChunkResourcesViewModel _chunkResources;
    private readonly ChunkInspectorViewModel _chunkInspector;
    private readonly HistoryViewModel _history;
    private readonly Dictionary<IDockable, string> _panelDefaultDocks;
    private readonly HashSet<string> _allowedPanelTypes;
    private bool _isClosingWindow;

    public DockFactory(IDockSerializer serializer, ScenesEditorsViewModel scenes, ResourcesEditorsViewModel resources,
        LogViewModel log, ProjectTreeViewModel projectTree, ChunkResourcesViewModel chunkResources, ChunkInspectorViewModel chunkInspector,
        HistoryViewModel history)
    {
        _serializer = serializer;
        _scenes = scenes;
        _resources = resources;
        _log = log;
        _projectTree = projectTree;
        _chunkResources = chunkResources;
        _chunkInspector = chunkInspector;
        _history = history;
        _panelDefaultDocks = new Dictionary<IDockable, string>
        {
            [scenes] = DocumentsPaneId,
            [resources] = DocumentsPaneId,
            [log] = LogPaneId,
            [history] = LogPaneId,
            [projectTree] = ProjectTreePaneId,
            [chunkResources] = ProjectTreePaneId,
            [chunkInspector] = ProjectTreePaneId
        };
        _allowedPanelTypes = _panelDefaultDocks.Keys
            .Select(panel => $"{panel.GetType().FullName}, {panel.GetType().Assembly.GetName().Name}")
            .ToHashSet();

        HideDocumentsOnClose = true;
    }

    public IRootDock? MainLayout { get; private set; }

    public override IRootDock CreateLayout()
    {
        var documentsPane = new DocumentDock
        {
            Id = DocumentsPaneId,
            IsCollapsable = false,
            VisibleDockables = CreateList<IDockable>(_scenes, _resources),
            ActiveDockable = _scenes
        };
        var logPane = new DocumentDock
        {
            Id = LogPaneId,
            Proportion = LogPaneProportion,
            VisibleDockables = CreateList<IDockable>(_log, _history),
            ActiveDockable = _log
        };
        var projectTreePane = new DocumentDock
        {
            Id = ProjectTreePaneId,
            Proportion = ProjectTreePaneProportion,
            VisibleDockables = CreateList<IDockable>(_projectTree, _chunkResources, _chunkInspector),
            ActiveDockable = _projectTree
        };
        var documentsArea = new ProportionalDock
        {
            Id = DocumentsAreaPaneId,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(documentsPane, new ProportionalDockSplitter(), logPane)
        };
        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(documentsArea, new ProportionalDockSplitter(), projectTreePane)
        };

        var root = CreateRootDock();
        root.Id = "Root";
        root.IsCollapsable = false;
        root.VisibleDockables = CreateList<IDockable>(mainLayout);
        root.ActiveDockable = mainLayout;
        root.DefaultDockable = mainLayout;

        return AdoptLayout(root);
    }

    public string SerializeLayout()
    {
        var layout = MainLayout ?? throw new InvalidDataException("There is no layout to save");
        if (layout.Windows != null)
        {
            foreach (var window in layout.Windows)
            {
                window.Save();
            }
        }

        return _serializer.Serialize(layout);
    }

    public void ValidateLayout(string json)
    {
        // Layouts are deserialized with type name handling so only Dock's own model and our panels may be instantiated
        var descendants = ((JContainer)JToken.Parse(json)).Descendants();
        foreach (var typeProperty in descendants.OfType<JProperty>().Where(property => property.Name == "$type"))
        {
            var typeName = typeProperty.Value.ToString();
            var assemblyName = typeName[(typeName.IndexOf(',') + 1)..].Trim();
            if (!AllowedLayoutAssemblies.Contains(assemblyName) && !_allowedPanelTypes.Contains(typeName))
            {
                throw new InvalidDataException($"Layout contains an unsupported type {typeName}");
            }
        }
    }

    public IRootDock DeserializeLayout(string json)
    {
        ValidateLayout(json);
        var layout = _serializer.Deserialize<IRootDock>(json) ?? throw new InvalidDataException("Layout is empty");
        return AdoptLayout(layout);
    }

    public void ShowPanel(IDockable panel)
    {
        if (MainLayout == null)
        {
            return;
        }

        if (MainLayout.EnumerateDockables().All(entry => entry.Dockable != panel))
        {
            var defaultDockId = _panelDefaultDocks[panel];
            var target = FindDockInLayout(panel.OriginalOwner)
                         ?? FindDockInLayout(defaultDockId)
                         ?? RecreatePanelDock(defaultDockId)
                         ?? FindDockInLayout(DocumentsPaneId)
                         ?? MainLayout.EnumerateDocks().OfType<IDocumentDock>().FirstOrDefault();
            if (target == null)
            {
                Log.WriteLine($"Couldn't find a place to open {panel.Title} in, try resetting the layout", Log.LogType.Warning);
                return;
            }

            MainLayout.HiddenDockables?.Remove(panel);
            panel.OriginalOwner = null;
            AddDockable(target, panel);
        }

        SetActiveDockable(panel);
        if (panel.Owner is IDock owner)
        {
            SetFocusedDockable(owner, panel);
        }
    }

    /// <summary>
    /// Brings a panel's tab to the front without taking the focus from where the user is, opening the panel when it's closed
    /// </summary>
    public void RevealPanel(IDockable panel)
    {
        if (MainLayout == null)
        {
            return;
        }

        if (!IsInLayout(panel))
        {
            ShowPanel(panel);
            return;
        }

        SetActiveDockable(panel);
    }

    public bool IsInLayout(IDockable panel)
    {
        return MainLayout != null && MainLayout.EnumerateDockables().Any(entry => entry.Dockable == panel);
    }

    public override void HideDockable(IDockable dockable)
    {
        var owner = dockable.Owner as IDock;
        base.HideDockable(dockable);
        if (MainLayout == null || !_panelDefaultDocks.ContainsKey(dockable))
        {
            return;
        }

        // Panels closed inside a floating window get hidden in that window's root which is thrown away together with the window
        if (dockable.Owner is IRootDock hiddenIn && hiddenIn != MainLayout)
        {
            hiddenIn.HiddenDockables?.Remove(dockable);
            MainLayout.HiddenDockables ??= CreateList<IDockable>();
            MainLayout.HiddenDockables.Add(dockable);
            dockable.Owner = MainLayout;
        }

        // Hiding doesn't collapse the emptied pane. Collapsing inside a closing window would try to remove that window again
        if (owner != null && !_isClosingWindow)
        {
            CollapseDock(owner);
        }
    }

    public override void CloseWindow(IDockWindow window)
    {
        if (window.Layout == null)
        {
            return;
        }

        _isClosingWindow = true;
        try
        {
            foreach (var (_, dockable) in window.Layout.EnumerateDockables().ToList())
            {
                if (!_panelDefaultDocks.ContainsKey(dockable))
                {
                    CloseDockable(dockable);
                    continue;
                }

                // A freshly loaded layout may have already taken the panel out of this window
                if (!dockable.IsDescendantOf(window.Layout))
                {
                    continue;
                }

                HideDockable(dockable);
            }
        }
        finally
        {
            _isClosingWindow = false;
        }
    }

    private IRootDock AdoptLayout(IRootDock layout)
    {
        layout.Factory = this;
        MainLayout = layout;
        return layout;
    }

    private IDock? RecreatePanelDock(string dockId)
    {
        var (anchor, operation, proportion) = dockId switch
        {
            LogPaneId => (FindDockInLayout(DocumentsPaneId), DockOperation.Bottom, LogPaneProportion),
            ProjectTreePaneId => (FindDockInLayout(DocumentsAreaPaneId) ?? FindDockInLayout(DocumentsPaneId), DockOperation.Right, ProjectTreePaneProportion),
            _ => (null, DockOperation.Fill, double.NaN)
        };
        if (anchor == null)
        {
            return null;
        }

        var paneDock = new DocumentDock
        {
            Id = dockId,
            VisibleDockables = CreateList<IDockable>()
        };
        SplitToDock(anchor, paneDock, operation);
        if (FindDockInLayout(paneDock) == null)
        {
            return null;
        }

        // Splitting gives both docks half of the anchor's space, restore the pane's default share of it instead
        var sharedProportion = anchor.Proportion + paneDock.Proportion;
        paneDock.Proportion = double.IsNaN(sharedProportion) ? proportion : sharedProportion * proportion;
        paneDock.CollapsedProportion = paneDock.Proportion;
        if (!double.IsNaN(sharedProportion))
        {
            anchor.Proportion = sharedProportion - paneDock.Proportion;
            anchor.CollapsedProportion = anchor.Proportion;
        }

        return paneDock;
    }

    private IDock? FindDockInLayout(IDockable? dock)
    {
        return dock is IDock candidate && MainLayout!.EnumerateDocks().Contains(candidate) ? candidate : null;
    }

    private IDock? FindDockInLayout(string id)
    {
        return MainLayout!.EnumerateDocks().FirstOrDefault(dock => dock.Id == id);
    }
}
