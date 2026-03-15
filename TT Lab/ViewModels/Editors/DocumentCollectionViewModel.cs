using System;
using System.Collections;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentCollectionViewModel : DocumentCompositeViewModel
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canChangeCollection = true;

    public override ReactiveCommand<Unit, Unit> AddCommand => CreateNewItemCommand;

    public const string ItemIndexAsChars = "DOCUMENT_COLLECTION_ITEM_INDEX_AS_CHARS";
    public const string ItemCaptionPrefix = "DOCUMENT_COLLECTION_FIELD_ITEM_CAPTION_PREFIX";
    public const string IsCollectionEditable = "DOCUMENT_COLLECTION_IS_EDITABLE";
    
    public DocumentCollectionViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
    }
    
    [ReactiveCommand]
    private void CreateNewItem()
    {
        var itemIndex = Nodes.Count;
        var itemCaption = $"{itemIndex}";
        if (_indexItemsAsChars)
        {
            itemCaption = ((char)(itemIndex + 32)).ToString();
        }
        
        if (!string.IsNullOrEmpty(_itemPrefix))
        {
            itemCaption = $"{_itemPrefix} {itemIndex}";
        }

        var data = Property.AddElement();
        if (data == null)
        {
            return;
        }
        
        var editor = EditorDescRegistry.GetDesc(Document, data).Construct();
        editor.Caption = itemCaption;
        editor.Closed += () => ItemClosed(editor);
        
        AddNode(editor);
    }

    protected override void ApplyEditorAttributes()
    {
        base.ApplyEditorAttributes();
        
        CanChangeCollection = GetEditorParameter(IsCollectionEditable, true);
        IsCollection = CanChangeCollection;
    }

    protected override void OnExpanded(CompositeDisposable disposables)
    {
        base.OnExpanded(disposables);
        
        _indexItemsAsChars = GetEditorParameter(ItemIndexAsChars, false);
        _itemPrefix = GetEditorParameter<string>(ItemCaptionPrefix);

        var itemIndex = 0;
        foreach (var item in Nodes)
        {
            var itemCaption = $"{itemIndex}";
            if (_indexItemsAsChars)
            {
                itemCaption = ((char)(itemIndex + 32)).ToString();
            }
        
            if (!string.IsNullOrEmpty(_itemPrefix))
            {
                itemCaption = $"{_itemPrefix} {itemIndex}";
            }

            itemIndex++;
            
            item.Caption = itemCaption;
            item.Closed += () => ItemClosed(item);
        }
    }

    private bool _indexItemsAsChars;
    private string? _itemPrefix;
    
    private void ItemClosed(DocumentNodeViewModel item)
    {
        RemoveNode(item);
    }
}