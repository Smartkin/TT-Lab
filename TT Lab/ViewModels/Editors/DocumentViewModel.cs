using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Threading.Tasks;
using DynamicData;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;


namespace TT_Lab.ViewModels.Editors;

public partial class DocumentViewModel : ReactiveObject
{
    /// <summary>
    /// Any document saved, what plays the project's files (PCSX2) builds them again
    /// </summary>
    public static event Action<DocumentViewModel>? Saved;

    public ReactiveLifecycle Lifecycle { get; } = new();
    public DocumentRootViewModel Root { get; }
    public IDocumentModel DocumentModel { get; }
    public PropertyGraph.PropertyGraph PropertyGraph { get; }
    public ViewportViewModel? Viewport { get; }

    public ReadOnlyObservableCollection<LabURI> Uris;

    /// <summary>
    /// Editors of the document's own asset that get a pane of their own next to the other editors: code and text, which need the room
    /// </summary>
    public IReadOnlyList<DocumentNodeViewModel> SideEditors { get; }

    public bool HasSideEditors => SideEditors.Count > 0;

    public Avalonia.Controls.GridLength SidePaneWidth => HasSideEditors ? new Avalonia.Controls.GridLength(3, Avalonia.Controls.GridUnitType.Star) : new Avalonia.Controls.GridLength(0);

    private readonly HashSet<PropertyNode> _sideNodes = [];

    private readonly SourceCache<LabURI, string> _resourcesInDocument;
    private readonly OpenDialogueCommand.DialogueResult _dialogueResult = new();
    // Linked assets are edited within the same document, all of them have to be saved along with it
    private readonly HashSet<IAsset> _changedAssets = new(ReferenceEqualityComparer.Instance);
    private DocumentNodeViewModel? _highlightedNode;

    [Reactive]
    private bool _isDirty;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canUndo;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canRedo;

    /// <summary>
    /// What the document's editors changed, every document keeps its own
    /// </summary>
    public UndoHistory History { get; }

    private readonly ParticleSystemLinks _particleSystemLinks;

    [Reactive]
    private bool _isReady;

    [Reactive]
    private DocumentNodeViewModel? _inspector;

    private readonly InspectorTrail _inspectorTrail = new();

    /// <summary>
    /// The inspector's trail, what following links went through, the inspected node marked
    /// </summary>
    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<InspectorCrumb> _inspectorCrumbs = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canInspectBack;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canInspectForward;

    /// <summary>
    /// The asset other chunks share that the inspected node is this chunk's version of, its own editor edits it for every chunk
    /// </summary>
    [Reactive(SetModifier = AccessModifier.Private)]
    private IAsset? _inspectedSharedAsset;

    public enum DocumentClosing
    {
        CloseAndSave,
        CloseAndNotSave,
        DoNotClose,
    }

    public DocumentViewModel(IDocumentModel documentModel, ViewportViewModel? viewport = null, ChunkOverrideSession? overrides = null)
    {
        _resourcesInDocument = new SourceCache<LabURI, String>(uri => uri);
        _resourcesInDocument.AddOrUpdate(LabURI.Empty);
        Viewport = viewport;
        
        // A scene shows what it's reading while it loads
        PropertyGraph = PropertyGraphBuilder.Build(documentModel, viewport == null ? null : viewport.ReportRead, overrides);
        History = new UndoHistory(PropertyGraph);
        PropertyGraph.Changed += change =>
        {
            if (GetOwningAsset(change.Node) is { } asset)
            {
                _changedAssets.Add(asset);
                if (asset is SerializableAsset { OverriddenAsset: not null } view)
                {
                    PropertyGraph.Overrides?.Invalidate(view);
                }
            }

            History.Record(change);
            IsDirty = !History.IsApplying || !History.IsAtSavePoint;
            // Once whatever made the change is done, an undo can take a node out and put it back in a step after
            if (Inspector != null && !_isFollowingInspected)
            {
                _isFollowingInspected = true;
                Avalonia.Threading.Dispatcher.UIThread.Post(FollowInspected);
            }
        };
        _particleSystemLinks = new ParticleSystemLinks(PropertyGraph);
        History.Changed += () =>
        {
            CanUndo = History.CanUndo;
            CanRedo = History.CanRedo;
            if (!History.IsApplying)
            {
                IsDirty = !History.IsAtSavePoint;
            }
        };

        foreach (var trackerExploredUri in PropertyGraph.Tracker.ExploredUris)
        {
            _resourcesInDocument.AddOrUpdate(trackerExploredUri);
        }
        
        _resourcesInDocument.Connect().Bind(out Uris).Subscribe();
        
        Root = new DocumentRootViewModel(this, PropertyGraph.Root)
        {
            Caption = documentModel.DocumentName,
            EditorName = documentModel.GetType().Name,
            IsExpanded = true,
            Depth = 0,
            IsAssetRoot = documentModel is IAsset,
        };
        DocumentModel = documentModel;

        // The asset's own editors only, what it links to is edited where the link is
        FindSideNodes(PropertyGraph.Root);
        SideEditors = _sideNodes.Select(node =>
        {
            var editor = EditorDescRegistry.GetDesc(this, node).Construct();
            editor.IsInSidePane = true;
            editor.Depth = 1;
            return editor;
        }).ToList();
    }

    private void FindSideNodes(PropertyNode node)
    {
        foreach (var child in node.Children)
        {
            if (child.Path.EndsWith("[data]"))
            {
                continue;
            }

            if (EditorDescRegistry.GetDesc(this, child).ShowsInSidePane)
            {
                _sideNodes.Add(child);
                continue;
            }

            FindSideNodes(child);
        }
    }

    /// <summary>
    /// Whether the node's editor is in the side pane, and so left out of the editors of what it's in
    /// </summary>
    public bool ShowsInSidePane(PropertyNode node) => _sideNodes.Contains(node);

    public void Initialize()
    {
        Lifecycle.Initialize();
        IsReady = true;
    }

    /// <summary>
    /// Shows the node expanded in the inspector, expanding it down to the focused node below it which then gets highlighted and expanded
    /// </summary>
    private bool _isFollowingInspected;

    // The inspected node taken out of the graph: the data of a resource that got deleted or whose placing got undone, or of a link
    // pointed elsewhere. Editing it changed nothing the document has, and its steps' paths pointed at whatever came to its place, so
    // the inspector goes to what took its place, or closes when nothing did
    internal void FollowInspected()
    {
        _isFollowingInspected = false;
        if (Inspector?.Property is not { } inspected || IsInGraph(inspected))
        {
            return;
        }

        var detached = new Stack<PropertyNode>();
        var anchor = inspected;
        while (anchor.Parent != null && !IsInGraph(anchor))
        {
            detached.Push(anchor);
            anchor = anchor.Parent;
        }

        PropertyNode? replacement = anchor;
        while (replacement != null && detached.Count > 0)
        {
            var node = detached.Pop();
            if (node.Index == null)
            {
                replacement = replacement.FindChild(node.Segment);
                continue;
            }

            // Another element may have come to the index of one taken out, and a node out of the graph has no value to ask for. Links are
            // known again by the asset their data is, like a resource put back by undo
            var asset = node.FindChild("[data]")?.Target as IAsset;
            replacement = asset == null ? null : replacement.Children.FirstOrDefault(child => child.FindChild("[data]")?.Target == asset);
        }

        if (replacement != null && replacement != anchor)
        {
            _inspectorTrail.ReplaceCurrent(replacement);
            ShowInInspector(replacement, null);
            return;
        }

        // Nothing took its place: back along the trail to what's still there, or nothing
        _inspectorTrail.Prune(IsInGraph);
        ShowInInspector(_inspectorTrail.Current, null);
    }

    private bool IsInGraph(PropertyNode node)
    {
        var current = node;
        while (current.Parent != null)
        {
            if (!current.Parent.Children.Contains(current))
            {
                return false;
            }

            current = current.Parent;
        }

        return current == PropertyGraph.Root;
    }

    /// <summary>
    /// Inspects what got picked (in the scene, among the resources), the inspector's trail starts over. Picking what's already inspected
    /// keeps the trail
    /// </summary>
    public void OpenInspector(PropertyNode? docToInspect, PropertyNode? focus = null)
    {
        if (docToInspect == null || Inspector?.Property != docToInspect)
        {
            _inspectorTrail.Start(docToInspect);
        }

        ShowInInspector(docToInspect, focus);
    }

    /// <summary>
    /// Inspects where a link of what's inspected leads, a step along the trail the inspector goes back on
    /// </summary>
    public void FollowInInspector(PropertyNode node, PropertyNode? focus = null)
    {
        if (Inspector == null)
        {
            _inspectorTrail.Start(node);
        }
        else
        {
            _inspectorTrail.Follow(node);
        }

        ShowInInspector(node, focus);
    }

    [ReactiveCommand(CanExecute = nameof(CanInspectBackChanges))]
    public void InspectBack() => GoAlongTrail(trail => trail.Back());

    [ReactiveCommand(CanExecute = nameof(CanInspectForwardChanges))]
    public void InspectForward() => GoAlongTrail(trail => trail.Forward());

    public void InspectCrumb(InspectorCrumb crumb) =>
        GoAlongTrail(trail => crumb.Index < trail.Nodes.Count && trail.Nodes[crumb.Index] == crumb.Node ? trail.GoTo(crumb.Index) : null);

    /// <summary>
    /// The shared asset the inspector shows this chunk's version of, in its own editor
    /// </summary>
    [ReactiveCommand]
    public void OpenSharedAsset()
    {
        if (InspectedSharedAsset is { } shared)
        {
            Locator.Current.GetService<ILabManager>()?.OpenEditor(shared);
        }
    }

    private IObservable<bool> CanInspectBackChanges => this.WhenAnyValue(x => x.CanInspectBack);

    private IObservable<bool> CanInspectForwardChanges => this.WhenAnyValue(x => x.CanInspectForward);

    // What left the graph since is passed over, there's nothing of it to inspect
    private void GoAlongTrail(Func<InspectorTrail, PropertyNode?> step)
    {
        step(_inspectorTrail);
        _inspectorTrail.Prune(IsInGraph);
        ShowInInspector(_inspectorTrail.Current, null);
    }

    private void ShowInInspector(PropertyNode? docToInspect, PropertyNode? focus)
    {
        Highlight(null);
        // Selecting another part of what's already inspected keeps everything expanded the way it is
        if (docToInspect != null && Inspector?.Property == docToInspect)
        {
            RevealInInspector(focus);
            UpdateInspectorTrail();
            return;
        }

        if (Inspector != null)
        {
            Inspector.IsVisible = false;
        }
        Inspector?.Close();

        if (docToInspect != null)
        {
            var inspector = EditorDescRegistry.GetDesc(this, docToInspect).Construct();
            // An inspected asset's top is the whole asset, named like its tab. Set before it's shown: the Inspector panel's view of the
            // last one takes it over right away
            if (docToInspect.Target is IAsset asset && (docToInspect.Parent == null || docToInspect.Segment == "[data]"))
            {
                inspector.Caption = asset.Alias;
                inspector.IsAssetRoot = true;
            }

            Inspector = inspector;
            Lifecycle.Register(Inspector);
        }
        else
        {
            Inspector = null;
        }

        if (Inspector != null)
        {
            Inspector.IsVisible = true;
            RevealInInspector(focus);
        }

        UpdateInspectorTrail();
    }

    private void UpdateInspectorTrail()
    {
        CanInspectBack = _inspectorTrail.CanGoBack;
        CanInspectForward = _inspectorTrail.CanGoForward;
        InspectorCrumbs = _inspectorTrail.Nodes.Select((node, index) => InspectorCrumb.Of(index, node, index == _inspectorTrail.Position)).ToList();
        InspectedSharedAsset = Inspector?.Property.GetValue() is SerializableAsset { OverriddenAsset: { } shared } ? shared : null;
    }

    /// <summary>
    /// A resource of the chunk the document is of, where the chunk's resources have it: what the scene and the resources panel inspect
    /// </summary>
    public PropertyNode? FindChunkResource(LabURI uri)
    {
        if (DocumentModel is not LevelChunk)
        {
            return null;
        }

        return PropertyGraph.Root.FindChild($".{nameof(LevelChunk.ChunkResources)}")?.Children.FirstOrDefault(element => Equals(element.GetValue(), uri))?.FindChild("[data]");
    }

    private void RevealInInspector(PropertyNode? focus)
    {
        if (Inspector is DocumentCompositeViewModel composite)
        {
            if (focus == null)
            {
                composite.IsExpanded = true;
            }
            else
            {
                composite.Reveal(focus);
            }

            return;
        }

        if (focus != null && Inspector?.Property == focus)
        {
            Highlight(Inspector);
        }
    }

    internal void Highlight(DocumentNodeViewModel? node)
    {
        if (_highlightedNode != null)
        {
            _highlightedNode.IsHighlighted = false;
        }

        _highlightedNode = node;
        if (node != null)
        {
            node.IsHighlighted = true;
        }
    }

    public void AddResource(LabURI uri)
    {
        if (uri == LabURI.Empty)
        {
            return;
        }
        
        _resourcesInDocument.AddOrUpdate(uri);
    }

    public void RemoveResource(LabURI? uri)
    {
        if (uri == null || uri == LabURI.Empty)
        {
            return;
        }
        
        _resourcesInDocument.Remove((string)uri);
    }

    public Boolean CanClose()
    {
        return Root.CanClose();
    }

    public async Task<DocumentClosing> CanCloseDocument()
    {
        if (!Root.CanClose())
        {
            return DocumentClosing.DoNotClose;
        }
        
        if (!IsDirty)
        {
            return DocumentClosing.CloseAndNotSave;
        }
        
        // Created on demand, every window holds on to a native window until it's closed
        var unsavedChangesDialogue = new UnsavedChangesDialogue(_dialogueResult, DocumentModel.DocumentName);
        await unsavedChangesDialogue.ShowDialog(MiscUtils.GetMainWindow());

        var result = MiscUtils.ConvertEnum<UnsavedChangesDialogue.AnswerResult>(_dialogueResult.Result);
        switch (result)
        {
            case UnsavedChangesDialogue.AnswerResult.YES:
                return DocumentClosing.CloseAndSave;
            case UnsavedChangesDialogue.AnswerResult.DISCARD:
                return DocumentClosing.CloseAndNotSave;
            case UnsavedChangesDialogue.AnswerResult.CANCEL:
            default:
                return DocumentClosing.DoNotClose;
        }
    }

    /// <summary>
    /// What a document made again over the same data keeps (another program changed files it shows): the assets its unsaved changes are
    /// in, a chunk's views with what got edited in them, the particle systems renamed and the history, unless a step of it changed what's
    /// read again
    /// </summary>
    internal sealed record Carried(bool IsDirty, IReadOnlyList<IAsset> ChangedAssets, ChunkOverrideSession? Overrides, UndoHistory? History,
        ParticleSystemLinks ParticleSystemLinks, string Reloaded);

    /// <summary>
    /// Whether the unsaved changes are in any of the assets, a chunk's views standing for theirs
    /// </summary>
    internal bool HasChangesIn(IReadOnlySet<LabURI> assets) =>
        _changedAssets.Any(asset => assets.Contains(asset.URI) || asset is SerializableAsset { OverriddenAsset: { } shared } && assets.Contains(shared.URI));

    internal Carried Carry(IReadOnlySet<LabURI> reread, string reloaded)
    {
        PropertyGraph.Overrides?.Forget(reread);
        var rereadNodes = new List<string>();
        FindNodesOf(PropertyGraph.Root, reread, rereadNodes);
        var history = History.Touches(path => rereadNodes.Any(node => IsWithin(path, node))) ? null : History;
        return new Carried(IsDirty, _changedAssets.ToList(), PropertyGraph.Overrides, history, _particleSystemLinks, reloaded);
    }

    // The nodes of the assets, a chunk's views of them included
    private static void FindNodesOf(PropertyNode node, IReadOnlySet<LabURI> assets, List<string> found)
    {
        if (node.Target is IAsset asset && (assets.Contains(asset.URI) || asset is SerializableAsset { OverriddenAsset: { } shared } && assets.Contains(shared.URI)))
        {
            found.Add(node.Path);
            return;
        }

        foreach (var child in node.Children)
        {
            FindNodesOf(child, assets, found);
        }
    }

    private static bool IsWithin(string path, string node) =>
        path.StartsWith(node, StringComparison.Ordinal) && (path.Length == node.Length || path[node.Length] is '.' or '[');

    internal void TakeOver(Carried carried)
    {
        foreach (var asset in carried.ChangedAssets)
        {
            _changedAssets.Add(asset);
        }

        _particleSystemLinks.TakeOver(carried.ParticleSystemLinks);
        if (carried.History != null)
        {
            History.Adopt(carried.History);
        }
        else
        {
            // Unsaved changes stay unsaved however far back the history goes
            History.Clear($"Reloaded {carried.Reloaded}");
            if (!carried.IsDirty)
            {
                History.MarkSaved();
            }
        }

        IsDirty = carried.IsDirty;
    }

    public void Save()
    {
        // What the chunk's views of shared assets differ in becomes the chunk's overrides, which get saved with the chunk
        foreach (var view in _changedAssets.OfType<SerializableAsset>().Where(asset => asset.OverriddenAsset != null))
        {
            PropertyGraph.Overrides?.Save(view);
        }

        DocumentModel.Save();
        foreach (var asset in _changedAssets.Where(asset => asset != DocumentModel && !asset.MarkedForDeletion && asset is not SerializableAsset { OverriddenAsset: not null }))
        {
            asset.Save();
        }

        _changedAssets.Clear();
        History.MarkSaved();
        IsDirty = false;
        _particleSystemLinks.Saved();
        Saved?.Invoke(this);
    }

    public void Undo()
    {
        History.Undo();
    }

    public void Redo()
    {
        History.Redo();
    }

    // The closest asset up the graph, linked assets are nodes of the graph that target the asset itself
    internal static IAsset? GetOwningAsset(PropertyNode? node)
    {
        while (node != null)
        {
            if (node.Target is IAsset asset)
            {
                return asset;
            }

            node = node.Parent;
        }

        return null;
    }
    
}