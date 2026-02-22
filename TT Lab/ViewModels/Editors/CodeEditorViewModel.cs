using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace TT_Lab.ViewModels.Editors;

public partial class CodeEditorViewModel(DocumentViewModel document, string code) : DocumentDataViewModel<string>(document, code)
{
    [Reactive]
    private string _code = code;

    protected override void OnInitialized(CompositeDisposable disposables)
    {
        base.OnInitialized(disposables);

        this.WhenAnyValue(x => x.Code)
            .Subscribe(code =>
            {
                Data = code;
            }).DisposeWith(disposables);
    }
}