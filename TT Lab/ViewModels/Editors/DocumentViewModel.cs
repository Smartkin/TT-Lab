using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
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

    public ReadOnlyObservableCollection<LabURI> Uris;

    private readonly SourceCache<LabURI, string> _resourcesInDocument;
    private UnsavedChangesDialogue _unsavedChangesDialogue;
    private readonly OpenDialogueCommand.DialogueResult _dialogueResult = new();

    [Reactive]
    private bool _isDirty;

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

    public DocumentViewModel(IDocumentModel documentModel)
    {
        _resourcesInDocument = new SourceCache<LabURI, String>(uri => uri);
        _resourcesInDocument.AddOrUpdate(LabURI.Empty);
        _resourcesInDocument.Connect().Bind(out Uris);
        
        PropertyGraph = PropertyGraphBuilder.Build(documentModel);
        PropertyGraph.Changed += _ => IsDirty = true;
        
        Root = new DocumentRootViewModel(this, PropertyGraph.Root)
        {
            Caption = documentModel.DocumentName,
            EditorName = documentModel.GetType().Name,
            IsExpanded = true,
            Depth = 0
        };
        DocumentModel = documentModel;

        RxSchedulers.MainThreadScheduler.Schedule(this, (_, state) =>
        {
            state._unsavedChangesDialogue = new UnsavedChangesDialogue(_dialogueResult, DocumentModel.DocumentName);
            return Disposable.Empty;
        });
    }

    public void Initialize()
    {
        Lifecycle.Initialize();
        IsReady = true;
    }

    public void OpenInspector(PropertyNode? docToInspect)
    {
        Inspector?.Close();

        if (docToInspect != null)
        {
            Inspector = EditorDescRegistry.GetDesc(this, docToInspect).Construct();
            Lifecycle.Register(Inspector);
        }

        if (Inspector != null)
        {
            Inspector.IsVisible = true;
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

    public void RemoveResource(LabURI uri)
    {
        if (uri == LabURI.Empty)
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
        
        await _unsavedChangesDialogue.ShowDialog(MiscUtils.GetMainWindow());
        _unsavedChangesDialogue = new UnsavedChangesDialogue(_dialogueResult, DocumentModel.DocumentName);
        
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
        DocumentModel.Save();
    }
    
}