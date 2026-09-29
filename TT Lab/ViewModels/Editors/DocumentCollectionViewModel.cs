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

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _canAddItem = true;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string? _addItemHint;

    public override ReactiveCommand<Unit, Unit> AddCommand => CreateNewItemCommand;

    public const string ItemIndexAsChars = "DOCUMENT_COLLECTION_ITEM_INDEX_AS_CHARS";
    public const string ItemCaptionPrefix = "DOCUMENT_COLLECTION_FIELD_ITEM_CAPTION_PREFIX";
    public const string IsCollectionEditable = "DOCUMENT_COLLECTION_IS_EDITABLE";
    // The most elements the game takes, like a material's 4 shaders
    public const string MaxCount = "DOCUMENT_COLLECTION_MAX_COUNT";
    
    public DocumentCollectionViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
    }
    
    [ReactiveCommand(CanExecute = nameof(CanAddItemChanges))]
    private void CreateNewItem()
    {
        if (Property.IsFull)
        {
            return;
        }

        _isAddingItem = true;
        var data = Property.AddElement();
        _isAddingItem = false;
        if (data == null)
        {
            return;
        }

        AddItem(data);
        UpdateCanAddItem();
    }

    private IObservable<bool> CanAddItemChanges => this.WhenAnyValue(x => x.CanAddItem);

    private void UpdateCanAddItem()
    {
        CanAddItem = !Property.IsFull;
        AddItemHint = Property.MaxElements is { } max ? $"The game takes at most {max}" : null;
    }

    private void AddItem(PropertyNode data)
    {
        var editor = EditorDescRegistry.GetDesc(Document, data).Construct();
        editor.Orientation = Avalonia.Controls.Dock.Left;
        editor.Closed += () => ItemClosed(editor);
        FollowName(editor);
        editor.Caption = CaptionOf(editor, data.Index ?? Nodes.Count);

        AddNode(editor);
    }

    // Elements with a name of their own, like the default chunk's 255 particle systems, show it next to their index and follow it
    private static PropertyNode? NameOf(DocumentNodeViewModel item) => item.Property.FindChild(".Name") is { PropertyType: var type } name && type == typeof(string) ? name : null;

    private string CaptionOf(DocumentNodeViewModel item, int index)
    {
        var caption = GetItemCaption(index);
        return NameOf(item)?.GetValue() is string { Length: > 0 } name ? $"{caption} · {name}" : caption;
    }

    private void FollowName(DocumentNodeViewModel item)
    {
        if (NameOf(item) is not { } name)
        {
            return;
        }

        void Rename() => item.Caption = CaptionOf(item, item.Property.Index ?? 0);
        name.Changed += Rename;
        item.Closed += () => name.Changed -= Rename;
    }

    // Elements can get added or removed from elsewhere, like instances placed or deleted in the viewport
    protected override void PropertyOnChanged()
    {
        base.PropertyOnChanged();
        UpdateCanAddItem();

        if (_isAddingItem || !_actuallyRemoved)
        {
            return;
        }

        var elements = Property.Children.ToHashSet();
        var removed = Nodes.Where(node => !elements.Contains(node.Property)).ToList();
        foreach (var node in removed)
        {
            RemoveNode(node);
        }

        var shown = Nodes.Select(node => node.Property).ToHashSet();
        var added = Property.Children.Where(child => !shown.Contains(child)).ToList();
        foreach (var child in added)
        {
            AddItem(child);
        }

        // Elements put in between move the ones after them to other paths
        if (removed.Count != 0 || added.Count != 0)
        {
            ReindexNodes(0);
        }
    }

    protected override void ApplyEditorAttributes()
    {
        base.ApplyEditorAttributes();
        
        CanChangeCollection = GetEditorParameter(IsCollectionEditable, true);
        IsCollection = CanChangeCollection;
        UpdateCanAddItem();
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
            item.Caption = CaptionOf(item, itemIndex++);
            item.Closed += () => ItemClosed(item);
            FollowName(item);
        }

        _actuallyRemoved = true;
    }

    protected override void OnCollapsed(CompositeDisposable disposables)
    {
        _actuallyRemoved = false;
        
        base.OnCollapsed(disposables);
    }

    private bool _actuallyRemoved = false;
    private bool _isAddingItem;
    private bool _indexItemsAsChars;
    private string? _itemPrefix;

    protected override void ReindexNodes(int fromIdx)
    {
        base.ReindexNodes(fromIdx);
        
        for (var i = fromIdx; i < Nodes.Count; i++)
        {
            var item = Nodes[i];
            item.Caption = CaptionOf(item, i);
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