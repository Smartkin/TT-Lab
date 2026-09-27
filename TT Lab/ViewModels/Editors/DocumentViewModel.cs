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
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentViewModel : ReactiveObject
{
    public ReactiveLifecycle Lifecycle { get; } = new();
    public DocumentRootViewModel Root { get; }
    public IDocumentModel DocumentModel { get; }
    public PropertyGraph.PropertyGraph PropertyGraph { get; }
    public ViewportViewModel? Viewport { get; }

    public ReadOnlyObservableCollection<LabURI> Uris;

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

    [Reactive]
    private bool _isReady;

    [Reactive]
    private DocumentNodeViewModel? _inspector;

    public enum DocumentClosing
    {
        CloseAndSave,
        CloseAndNotSave,
        DoNotClose,
    }

    public DocumentViewModel(IDocumentModel documentModel, ViewportViewModel? viewport = null)
    {
        _resourcesInDocument = new SourceCache<LabURI, String>(uri => uri);
        _resourcesInDocument.AddOrUpdate(LabURI.Empty);
        Viewport = viewport;
        
        PropertyGraph = PropertyGraphBuilder.Build(documentModel);
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
        };
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
            Depth = 0
        };
        DocumentModel = documentModel;
    }

    public void Initialize()
    {
        Lifecycle.Initialize();
        IsReady = true;
    }

    /// <summary>
    /// Shows the node expanded in the inspector, expanding it down to the focused node below it which then gets highlighted and expanded
    /// </summary>
    public void OpenInspector(PropertyNode? docToInspect, PropertyNode? focus = null)
    {
        Highlight(null);
        // Selecting another part of what's already inspected keeps everything expanded the way it is
        if (docToInspect != null && Inspector?.Property == docToInspect)
        {
            RevealInInspector(focus);
            return;
        }

        if (Inspector != null)
        {
            Inspector.IsVisible = false;
        }
        Inspector?.Close();

        if (docToInspect != null)
        {
            Inspector = EditorDescRegistry.GetDesc(this, docToInspect).Construct();
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
    }

    public void Undo() => History.Undo();

    public void Redo() => History.Redo();

    // The closest asset up the graph, linked assets are nodes of the graph that target the asset itself
    private static IAsset? GetOwningAsset(PropertyNode? node)
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