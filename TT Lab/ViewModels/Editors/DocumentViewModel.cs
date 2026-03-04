using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentViewModel : DocumentPartViewModel
{
    private readonly SourceCache<DocumentPartViewModel, string> _documentEditors;
    private UnsavedChangesDialogue _unsavedChangesDialogue;
    private readonly OpenDialogueCommand.DialogueResult _dialogueResult;
    private readonly DocumentViewModel? _parent;
    private readonly Type? _itemType;
    private readonly Type? _itemEditorType;
    private readonly IEnumerable? _itemList;

    public IDocumentModel DocumentModel { get; }
    public DocumentViewModel? Parent
    {
        get => _parent;
        init => _parent = value;
    }

    [Reactive]
    private bool _isDirty;

    [Reactive]
    private bool _isEditable;

    [Reactive]
    private bool _useCaption = true;

    [Reactive]
    private bool _useUri;

    public override bool UseDefaultCaption => false;

    public bool IsExpanded { init; get; } = false;

    public enum DocumentClosing
    {
        CloseAndSave,
        CloseAndNotSave,
        DoNotClose,
    }
    
    public ReadOnlyObservableCollection<DocumentPartViewModel> DocumentEditors;

    public DocumentViewModel(DocumentViewModel? parent, IDocumentModel documentModel) : base(parent)
    {
        SetDocument(this);
        DocumentModel = documentModel;
        
        EditorName = DocumentModel.DocumentName;
        _documentEditors = new SourceCache<DocumentPartViewModel, String>(model => model.EditorName);

        Parent = parent;
        if (_parent == null)
        {
            _dialogueResult = new OpenDialogueCommand.DialogueResult();
            _unsavedChangesDialogue = new UnsavedChangesDialogue(_dialogueResult, DocumentModel.DocumentName);
        }

        _parent?.AddChild(this);
    }

    private DocumentViewModel(DocumentViewModel parent, string listName, Type itemEditorType, Type itemType, IEnumerable list) : base(parent)
    {
        SetDocument(this);
        DocumentModel = parent.DocumentModel;

        _isEditable = true;
        _itemType = itemType;
        _itemList = list;
        _itemEditorType = itemEditorType;
        
        EditorName = listName;
        _documentEditors = new SourceCache<DocumentPartViewModel, String>(model => model.EditorName);

        Parent = parent;
    }

    public void AddDocument(DocumentViewModel document)
    {
        _documentEditors.AddOrUpdate(document);
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

    public override Boolean CanClose()
    {
        return _documentEditors.Items.All(d => d.CanClose());
    }

    public async Task<DocumentClosing> CanCloseDocument()
    {
        if (!_documentEditors.Items.All(d => d.CanClose()))
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

    public override void Save()
    {
        foreach (var editor in _documentEditors.Items)
        {
            editor.Save();
        }
        
        if (_itemList != null)
        {
            base.Save();
            return;
        }
        
        DocumentModel.Save();
    }
    
    private DocumentPartViewModel CreateDocumentEditor(string editorName, MemberInfo forMember, EditableAttribute editorAttribute, 
        object editorData)
    {
        var editorParams = GetEditorParams(forMember);
        int editorId;
        if (editorParams.TryGetValue(EditorExplicitOrder, out var explicitId))
        {
            editorId = (int)explicitId;
        }
        else
        {
            editorId = GetNewEditorId();
        }

        // TODO: We should probably not use activator to create and use something better and more type safe but whatever for now
        var editor = (DocumentPartViewModel)System.Activator.CreateInstance(editorAttribute.EditorType!, this, editorData)!;
        editor.Id = editorId;
        editor.EditorName = editorName;
        editor.SaveLocation = forMember switch
        {
            PropertyInfo propInfo => propInfo.Name,
            Type type => type.Name,
            _ => string.Empty
        };
        editor.Caption = string.IsNullOrEmpty(editorAttribute.Caption) ? editorName : editorAttribute.Caption;
        editor.Hint = editorAttribute.Hint;
        editor.FieldLinks = GetFieldReactors(forMember);
        editor.Orientation = editorAttribute.EditorOrientation;
        editor.EditorParameters = editorParams;
        ApplyAttributeWrappers(editor, forMember);
        editor.Metadata = forMember;
        editor.PropertyType = forMember switch
        {
            PropertyInfo propInfo => propInfo.PropertyType,
            Type type => type,
            _ => editor.PropertyType
        };

        return editor;
    }

    private Dictionary<string, List<IFieldChange>> GetFieldReactors(MemberInfo provider)
    {
        var links = provider.GetCustomAttributes<EditorLinkedFieldAttribute>();
        var result = new Dictionary<string, List<IFieldChange>>();
        foreach (var link in links)
        {
            if (!result.TryGetValue(link.LinkedField, out var linkList))
            {
                result[link.LinkedField] = [];
                linkList = result[link.LinkedField];
            }
            
            linkList.Add((IFieldChange)System.Activator.CreateInstance(link.ActionChange)!);
        }
        return result;
    }

    private Dictionary<string, object> GetEditorParams(MemberInfo provider)
    {
        return provider.GetCustomAttributes<EditorParamAttribute>().ToDictionary(x => x.Param, x => x.Value);
    }

    private static void ApplyAttributeWrappers(DocumentPartViewModel documentPart, MemberInfo provider)
    {
        var wrappers = provider.GetCustomAttributes<EditorParamWrapperBaseAttribute>();
        foreach (var wrapper in wrappers)
        {
            wrapper.ApplyTo(documentPart);
        }
    }
    
    private void ExtractEditorsFromCollection(IEnumerable collection)
    {
        IsEditable = GetEditorParameter(IsCollectionEditable, true);
        
        var itemIndex = 0;
        foreach (var item in collection)
        {
            var itemCaption = $"{itemIndex}";
            var useCharIndex = GetEditorParameter(ItemIndexAsChars, false);
            if (useCharIndex)
            {
                itemCaption = ((char)(itemIndex + 32)).ToString();
            }
            if (EditorParameters.TryGetValue(ItemCaptionPrefix, out var caption))
            {
                itemCaption = $"{caption} {itemCaption}";
            }

            var documentName = $"{Caption}_ITEM_{itemCaption}";
            
            var itemType = item.GetType();
            if (itemType.IsGenericType)
            {
                var resultItemEditorType = DetermineEditorType(itemType.GetGenericArguments()[0]);
                if (EditorParameters.TryGetValue(ItemEditorType, out var itemEditorType))
                {
                    resultItemEditorType = (Type)itemEditorType;
                }
                var collectionDocument = new DocumentViewModel(this, documentName, resultItemEditorType, itemType.GetGenericArguments()[0], (IEnumerable)item);
                collectionDocument.Caption = itemCaption;
                collectionDocument.SaveLocation = SaveLocation;
                collectionDocument.IsPartOfCollection = true;
                collectionDocument.EditorParameters = EditorParameters;
                collectionDocument.FieldLinks = FieldLinks;
                collectionDocument.Metadata = Metadata;
                ApplyAttributeWrappers(collectionDocument, Metadata);
                _documentEditors.AddOrUpdate(collectionDocument);
                continue;
            }
            
            var editableAttribute = new EditableAttribute
            {
                EditorType = _itemEditorType ?? DetermineEditorType(_itemType!),
                Caption = itemCaption,
            };

            var editor = CreateDocumentEditor(documentName, Metadata, editableAttribute, item);
            editor.SaveLocation = SaveLocation;
            editor.IsPartOfCollection = true;
            editor.Closed += () =>
            {
                RemoveItem(editor);
            };
            
            _documentEditors.AddOrUpdate(editor);
            
            itemIndex++;
        }
    }

    private void ExtractEditableFields(IDocumentModel documentModel)
    {
        var assetProps = documentModel.GetType().GetProperties();
        CreateEditorsForProps(documentModel, assetProps);
        
        var docEditor = documentModel.GetType().GetCustomAttribute<EditableAttribute>();
        if (docEditor == null)
        {
            return;
        }
        
        docEditor.EditorType ??= DetermineEditorType(documentModel.GetType());
        _documentEditors.AddOrUpdate(CreateDocumentEditor($"{documentModel.DocumentName}__DOCUMENT_EDITOR", documentModel.GetType(), docEditor, documentModel));
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

            if (prop.PropertyType.IsGenericType || prop.PropertyType.IsArray)
            {
                var propData = (IList)prop.GetValue(data)!;
                var elemType = propData.GetType().GetElementType() ?? propData.GetType().GetGenericArguments()[0];
                var editorParams = GetEditorParams(prop);
                var resultItemEditorType = DetermineEditorType(elemType);
                if (editorParams.TryGetValue(ItemEditorType, out var itemEditorType))
                {
                    resultItemEditorType = (Type)itemEditorType;
                }
                var collectionDocument = new DocumentViewModel(this, prop.Name, resultItemEditorType, elemType, propData);
                collectionDocument.EditorParameters = editorParams;
                collectionDocument.SaveLocation = prop.Name;
                collectionDocument.FieldLinks = GetFieldReactors(prop);
                collectionDocument.Metadata = prop;
                collectionDocument.Caption = string.IsNullOrEmpty(editableAttribute.Caption) ? prop.Name : editableAttribute.Caption;
                _documentEditors.AddOrUpdate(collectionDocument);
                continue;
            }
            
            editableAttribute.EditorType ??= DetermineEditorType(prop.PropertyType);
            _documentEditors.AddOrUpdate(CreateDocumentEditor(prop.Name, prop, editableAttribute, prop.GetValue(data)!));
        }
    }

    private class DocComparer : IComparer<DocumentPartViewModel>
    {
        public Int32 Compare(DocumentPartViewModel? x, DocumentPartViewModel? y)
        {
            if (x == null || y == null)
            {
                return 0;
            }
            
            return x.Id - y.Id;
        }
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        _documentEditors.Connect().ObserveOn(AvaloniaScheduler.Instance)
            .SortAndBind(out DocumentEditors, new DocComparer()).Subscribe().DisposeWith(disposables);

        _documentEditors.Connect().ObserveOn(AvaloniaScheduler.Instance)
            .Subscribe(x =>
            {
                var list = x.ToList();
                foreach (var item in list)
                {
                    if (item.Reason != ChangeReason.Add)
                    {
                        continue;
                    }
                    
                    item.Current.Depth = Depth + 1;
                }
            }).DisposeWith(disposables);

        if (_itemList != null)
        {
            var itemsAmount = _itemList.Cast<Object?>().Count();
            _documentEditors.Connect()
                .Skip(itemsAmount)
                .Subscribe(x =>
                {
                    var list = x.ToList();
                    var newIdx = 0;
                    foreach (var item in list)
                    {
                        if (item.Reason == ChangeReason.Remove)
                        {
                            continue;
                        }

                        item.Current.Caption =
                            $"{item.Current.GetEditorParameter(ItemCaptionPrefix, "")!} {newIdx}";
                        newIdx++;
                    }
                    
                    IsDirty = true;
                }).DisposeWith(disposables);
        }

        this.WhenAnyValue<DocumentViewModel, bool>(nameof(IsDirty))
            .Where(x => x)
            .Subscribe(b =>
            {
                if (Parent != null)
                {
                    Parent.IsDirty = b;
                }
            }).DisposeWith(disposables);
        
        DocumentModel.DisposeWith(disposables);

        if (_itemType == null)
        {
            RxSchedulers.TaskpoolScheduler.Schedule(DocumentModel, (_, assetState) =>
            {
                ExtractEditableFields(assetState);
                return Disposable.Empty;
            });
        }
        else
        {
            RxSchedulers.TaskpoolScheduler.Schedule(_itemList!, (_, list) =>
            {
                ExtractEditorsFromCollection(list);
                return Disposable.Empty;
            });
        }
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

    public override Object? GetFinalData()
    {
        if (_itemList == null)
        {
            return DocumentModel;
        }
        
        var newList = (IList)System.Activator.CreateInstance(_itemList.GetType())!;
        var items = _documentEditors.Items.Select(documentPartViewModel => documentPartViewModel.GetFinalData()).ToList();
        foreach (var item in items)
        {
            newList.Add(item);
        }
        
        return newList;
    }

    private const int CHILD_DOCUMENTS_IDS = 100000;
    private void AddChild(DocumentViewModel document)
    {
        document.Id = CHILD_DOCUMENTS_IDS + GetNewEditorId();
    }
    
    [ReactiveCommand]
    private void CreateNewItem()
    {
        var itemIndex = DocumentEditors.Count;
        var itemCaption = $"{itemIndex}";
        var useCharIndex = GetEditorParameter(ItemIndexAsChars, false);
        if (useCharIndex)
        {
            itemCaption = ((char)(itemIndex + 32)).ToString();
        }
        
        if (EditorParameters.TryGetValue(ItemCaptionPrefix, out var caption))
        {
            itemCaption = $"{caption} {itemIndex}";
        }
        
        var editableAttribute = new EditableAttribute
        {
            EditorType = _itemEditorType ?? DetermineEditorType(_itemType!),
            Caption = itemCaption,
        };

        var data = System.Activator.CreateInstance(_itemType!);
        var editor = CreateDocumentEditor($"{Caption}_ITEM_{itemIndex}", Metadata, editableAttribute, data!);
        editor.IsPartOfCollection = true;
        editor.Closed += () =>
        {
            RemoveItem(editor);
        };
        _documentEditors.AddOrUpdate(editor);
    }

    private void RemoveItem(DocumentPartViewModel item)
    {
        _documentEditors.Remove(item);
    }

    private static Type DetermineEditorType(Type itemType)
    {
        if (itemType == typeof(Boolean))
        {
            return typeof(BoolFieldViewModel);
        }

        if (itemType.IsEnum)
        {
            return itemType.GetCustomAttribute<FlagsAttribute>() != null ? typeof(FlagsFieldViewModel) : typeof(EnumFieldViewModel);
        }

        if (itemType == typeof(LabURI))
        {
            return typeof(UriLinkViewModel);
        }

        if (itemType == typeof(Vector2))
        {
            return typeof(Vector2FieldViewModel);
        }

        if (itemType == typeof(Vector3))
        {
            return typeof(Vector3FieldViewModel);
        }

        if (itemType == typeof(Vector4))
        {
            return typeof(Vector4FieldViewModel);
        }

        if (itemType == typeof(Matrix4))
        {
            return typeof(Matrix4FieldViewModel);
        }

        if (itemType == typeof(VectorCharacterData))
        {
            return typeof(VectorCharacterDataViewModel);
        }
        
        if (itemType == typeof(Byte) || itemType == typeof(SByte) || itemType == typeof(Int16) ||
            itemType == typeof(Int32) || itemType == typeof(Int64) || itemType == typeof(Int128)
            || itemType == typeof(UInt16) || itemType == typeof(UInt32) || itemType == typeof(UInt64) || itemType == typeof(UInt128)
            || itemType == typeof(Single) || itemType == typeof(Double) || itemType == typeof(Decimal)
            || itemType == typeof(String))
        {
            return typeof(TextFieldViewModel);
        }
        
        return typeof(DocumentViewModel);
    }

    public const string EditorExplicitOrder = "DOCUMENT_EXPLICIT_ORDER";
    public const string PartOfCollection = "DOCUMENT_PART_OF_COLLECTION";
    public const string ItemEditorType = "DOCUMENT_COLLECTION_ITEM_EDITOR_TYPE";
    public const string ItemIndexAsChars = "DOCUMENT_COLLECTION_ITEM_INDEX_AS_CHARS";
    public const string ItemCaptionPrefix = "DOCUMENT_COLLECTION_FIELD_ITEM_CAPTION_PREFIX";
    public const string IsCollectionEditable = "DOCUMENT_COLLECTION_IS_EDITABLE";
}