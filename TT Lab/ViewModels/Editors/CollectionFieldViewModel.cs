using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reflection;
using DynamicData;
using ReactiveUI;
using TT_Lab.Assets;
using TT_Lab.Attributes;

namespace TT_Lab.ViewModels.Editors;

public class CollectionFieldViewModel : DocumentPartViewModel
{
    private readonly IEnumerable _dataList;
    private readonly SourceList<DocumentPartViewModel> _collection;

    public ReadOnlyObservableCollection<DocumentPartViewModel> Collection;
    
    public CollectionFieldViewModel(DocumentViewModel document, IEnumerable dataList) : base(document)
    {
        _dataList = dataList;
        _collection = new SourceList<DocumentPartViewModel>();
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        var itemsAmount = _dataList.Cast<Object?>().Count();

        _collection.Connect().ObserveOn(RxApp.MainThreadScheduler)
            .Bind(out Collection)
            .Skip(itemsAmount)
            .Subscribe(x =>
        {
            Document.IsDirty = true;
        }).DisposeWith(disposables);

        if (EditorParameters.TryGetValue(ItemEditorType, out var itemEditor))
        {
            _itemEditorType = (Type)itemEditor;
        }
        
        RxSchedulers.TaskpoolScheduler.Schedule(_dataList, (scheduler, list) =>
        {
            ExtractEditorsFromCollection(list);
            
            return Disposable.Empty;
        });
    }

    protected override void OnClosed(CompositeDisposable disposables)
    {
        base.OnClosed(disposables);

        foreach (var item in _collection.Items)
        {
            item.Close();
        }
        
        _collection.DisposeWith(disposables);
    }

    public override object GetData()
    {
        return _collection.Items.Select(documentPartViewModel => documentPartViewModel.GetData()).ToList();
    }

    private void ExtractEditorsFromCollection(IEnumerable collection)
    {
        var itemIndex = 0;
        foreach (var item in collection)
        {
            var itemCaption = $"Item {itemIndex}";
            if (EditorParameters.TryGetValue(ItemCaptionPrefix, out var caption))
            {
                itemCaption = $"{caption} {itemIndex}";
            }
            
            var editableAttribute = new EditableAttribute
            {
                EditorType = _itemEditorType,
                Caption = itemCaption,
            };

            _collection.Add(Document.CreateDocumentEditor($"{Caption}_ITEM_{itemIndex}", Metadata, editableAttribute, item));
            
            itemIndex++;
        }
    }

    public override bool UseDefaultCaption => false;

    public const string ItemEditorType = "COLLECTION_FIELD_ITEM_EDITOR_TYPE";
    public const string ItemCaptionPrefix = "COLLECTION_FIELD_ITEM_CAPTION_PREFIX";
    public const string IsEditable = "COLLECTION_FIELD_IS_EDITABLE";

    private Type _itemEditorType = typeof(TextFieldViewModel);
}