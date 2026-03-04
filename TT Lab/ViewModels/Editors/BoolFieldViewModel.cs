using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace TT_Lab.ViewModels.Editors;

public partial class BoolFieldViewModel(DocumentViewModel document, bool data) : DocumentDataViewModel<bool>(document, data)
{
    [Reactive]
    private bool _isChecked = data;

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        this.WhenAnyValue(x => x.IsChecked)
            .Subscribe(b =>
            {
                Data = b;
            }).DisposeWith(disposables);
    }
}