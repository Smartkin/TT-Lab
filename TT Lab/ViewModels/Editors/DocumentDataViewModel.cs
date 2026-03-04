using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentDataViewModel<T> : DocumentPartViewModel
{
    [Reactive]
    private T _data;

    protected T InitialData;

    protected DocumentDataViewModel(DocumentViewModel document, T data) : base(document)
    {
        _data = data;
        InitialData = data;
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        if (FieldLinks != null)
        {
            foreach (var (field, links) in FieldLinks)
            {
                var viewModel = Document.GetViewModel(field);
                if (!viewModel.HasValue)
                {
                    continue;
                }
                
                var unwrappedVm = viewModel.Value;
                unwrappedVm.WhenAnyValue(x => x.DataVersion).Subscribe(x =>
                {
                    foreach (var link in links)
                    {
                        link.DataChanged(this, unwrappedVm);
                    }
                }).DisposeWith(disposables);
            }
        }
        
        this.WhenAnyValue(x => x.Data)
            .Skip(1)
            .Subscribe(_ =>
        {
            Document.IsDirty = true;
            DataVersion++;
        }).DisposeWith(disposables);
    }

    public override object? GetFinalData()
    {
        return Data;
    }
}