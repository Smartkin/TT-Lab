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
        var itemCaption = GetItemCaption(itemIndex);

        var data = Property.AddElement();
        if (data == null)
        {
            return;
        }
        
        var editor = EditorDescRegistry.GetDesc(Document, data).Construct();
        editor.Caption = itemCaption;
        editor.Orientation = Avalonia.Controls.Dock.Left;
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
            item.Orientation = Avalonia.Controls.Dock.Left;
            item.Caption = GetItemCaption(itemIndex++);
            item.Closed += () => ItemClosed(item);
        }

        _actuallyRemoved = true;
    }

    protected override void OnCollapsed(CompositeDisposable disposables)
    {
        _actuallyRemoved = false;
        
        base.OnCollapsed(disposables);
    }

    private bool _actuallyRemoved = false;
    private bool _indexItemsAsChars;
    private string? _itemPrefix;

    protected override void ReindexNodes(int fromIdx)
    {
        base.ReindexNodes(fromIdx);
        
        for (var i = fromIdx; i < Nodes.Count; i++)
        {
            var item = Nodes[i];
            item.Caption = GetItemCaption(i);
        }
    }
    
    private void ItemClosed(DocumentNodeViewModel item)
    {
        var property = item.Property;
        RemoveNode(item);

        if (_actuallyRemoved)
        {
            Property.RemoveElement(property);
            ReindexNodes(property.Index!.Value);
        }
    }

    private string GetItemCaption(int i)
    {
        var itemCaption = $"{i}";
        if (_indexItemsAsChars)
        {
            itemCaption = ((char)(i + 32)).ToString();
        }
        
        if (!string.IsNullOrEmpty(_itemPrefix))
        {
            itemCaption = $"{_itemPrefix} {i}";
        }

        return itemCaption;
    }
}