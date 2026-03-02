using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentDataViewModel<T> : DocumentPartViewModel
{
    [Reactive(SetModifier = AccessModifier.Protected)]
    private T _data;

    protected T InitialData;

    protected DocumentDataViewModel(DocumentViewModel document, T data) : base(document)
    {
        _data = data;
        InitialData = data;
    }

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        this.WhenAnyValue(x => x.Data)
            .Skip(1)
            .Subscribe(_ =>
        {
            Document.IsDirty = true;
        }).DisposeWith(disposables);
    }

    public override object? GetFinalData()
    {
        return Data;
    }
}