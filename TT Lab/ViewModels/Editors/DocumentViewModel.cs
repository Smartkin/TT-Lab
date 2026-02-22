using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentViewModel : DocumentPartViewModel
{
    private readonly SourceCache<DocumentPartViewModel, string> _documentEditors;
    private UnsavedChangesDialogue _unsavedChangesDialogue;
    private readonly OpenDialogueCommand.DialogueResult _dialogueResult;
    private DocumentViewModel? _parent;
    
    public IDocumentModel DocumentModel { get; }
    public DocumentViewModel? Parent
    {
        get => _parent;
        init
        {
            _parent = value;
            _parent?.AddChild(this);
        }
    }

    [Reactive]
    private bool _isDirty;

    public enum DocumentClosing
    {
        CloseAndSave,
        CloseAndNotSave,
        DoNotClose
    }
    
    public ReadOnlyObservableCollection<DocumentPartViewModel> DocumentEditors;

    public DocumentViewModel(IDocumentModel documentModel) : base(null)
    {
        SetDocument(this);
        DocumentModel = documentModel;
        
        EditorName = DocumentModel.Name;
        _documentEditors = new SourceCache<DocumentPartViewModel, String>(model => model.EditorName);
        
        _dialogueResult = new OpenDialogueCommand.DialogueResult();
        _unsavedChangesDialogue = new UnsavedChangesDialogue(_dialogueResult, DocumentModel.Name);
    }

    public Optional<DocumentPartViewModel> GetViewModel(string propName)
    {
        var lookUp = _documentEditors.Lookup(propName);
        if (!lookUp.HasValue && _parent != null)
        {
            return _parent.GetViewModel(propName);
        }
        
        return lookUp;
    }

    public async Task<DocumentClosing> CanClose()
    {
        if (!IsDirty)
        {
            return DocumentClosing.CloseAndNotSave;
        }
        
        await _unsavedChangesDialogue.ShowDialog(MiscUtils.GetMainWindow());
        _unsavedChangesDialogue = new UnsavedChangesDialogue(_dialogueResult, DocumentModel.Name);
        
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

    public override void Save(string propName)
    {
        foreach (var (prop, editor) in _documentEditors.KeyValues)
        {
            editor.Save(prop);
        }
        
        DocumentModel.Save();
    }
    
    public DocumentPartViewModel CreateDocumentEditor(string editorName, MemberInfo forObject, EditableAttribute editorAttribute, 
        object editorData)
    {
        var editorParams = GetEditorParams(forObject);
        int editorId;
        if (editorParams.TryGetValue(EditorExplicitOrder, out var explicitId))
        {
            editorId = (int)explicitId;
        }
        else
        {
            editorId = GetNewEditorId();
        }
        var editor = (DocumentPartViewModel)System.Activator.CreateInstance(editorAttribute.EditorType, this, editorData)!;
        editor.Id = editorId;
        editor.EditorName = editorName;
        editor.Caption = string.IsNullOrEmpty(editorAttribute.Caption) ? editorName : editorAttribute.Caption;
        editor.Hint = editorAttribute.Hint;
        editor.Orientation = editorAttribute.EditorOrientation;
        editor.EditorParameters = editorParams;
        editor.Metadata = forObject;

        return editor;
    }

    public Dictionary<string, object> GetEditorParams(MemberInfo provider)
    {
        return provider.GetCustomAttributes<EditorParamAttribute>().ToDictionary(x => x.Param, x => x.Value);
    }

    private void ExtractEditableFields(IDocumentModel documentModel)
    {
        var assetProps = documentModel.GetType().GetProperties();
        CreateEditorsForProps(documentModel, assetProps);
        
        var docEditor = documentModel.GetType().GetCustomAttribute<EditableAttribute>();
        if (docEditor != null)
        {
            _documentEditors.AddOrUpdate(CreateDocumentEditor($"{documentModel.Name}__DOCUMENT_EDITOR", documentModel.GetType(), docEditor, documentModel));
        }
    }

    private void CreateEditorsForProps(object data, PropertyInfo[] props)
    {
        foreach (var prop in props)
        {
            var editableAttribute = prop.GetCustomAttribute<EditableAttribute>();
            if (editableAttribute == null)
            {
                continue;
            }
            
            _documentEditors.AddOrUpdate(CreateDocumentEditor(prop.Name, prop, editableAttribute, prop.GetValue(data)!));
        }
    }

    private class DocComparer : IComparer<DocumentPartViewModel>
    {
        public Int32 Compare(DocumentPartViewModel? x, DocumentPartViewModel? y)
        {
            return x.Id - y.Id;
        }
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        _documentEditors.Connect().ObserveOn(AvaloniaScheduler.Instance)
            .SortAndBind(out DocumentEditors, new DocComparer()).Subscribe().DisposeWith(disposables);

        this.WhenAnyValue<DocumentViewModel, bool>(nameof(IsDirty))
            .Subscribe(b =>
            {
                if (_parent != null)
                {
                    _parent.IsDirty = b;
                }
            }).DisposeWith(disposables);
        
        DocumentModel.DisposeWith(disposables);
        
        RxSchedulers.TaskpoolScheduler.Schedule(DocumentModel, (_, assetState) =>
        {
            ExtractEditableFields(assetState);
            return Disposable.Empty;
        });
    }

    protected override void OnClosed(CompositeDisposable disposables)
    {
        base.OnClosed(disposables);
        
        foreach (var (prop, editor) in _documentEditors.KeyValues)
        {
            editor.Close();
        }
        
        _documentEditors.DisposeWith(disposables);
    }

    public override Object? GetData()
    {
        return DocumentModel;
    }

    private const int CHILD_DOCUMENTS_IDS = 100000;
    private void AddChild(DocumentViewModel document)
    {
        document.Id = CHILD_DOCUMENTS_IDS + GetNewEditorId();
        _documentEditors.AddOrUpdate(document);
    }

    public const string EditorExplicitOrder = "DOCUMENT_EXPLICIT_ORDER";
}