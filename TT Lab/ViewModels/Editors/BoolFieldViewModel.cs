using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public partial class BoolFieldViewModel(
    DocumentViewModel document,
    PropertyNode node,
    params DocumentNodeViewModel[] dependencies) : DocumentDataViewModel<bool>(document, node, dependencies)
{
    [Reactive]
    private bool _isChecked = node.GetValue<bool>();

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        
        IsChecked = CurrentValue;
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        
        this.WhenAnyValue(x => x.IsChecked)
            .Skip(1)
            .InvokeCommand(SetValueCommand).DisposeWith(disposables);
    }
}