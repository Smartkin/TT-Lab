using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using SharpGLTF.Schema2;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public partial class DocumentDataViewModel<T> : DocumentNodeViewModel
{
    [ObservableAsProperty]
    private T? _currentValue;
    
    public ReactiveCommand<T?, Unit> SetValueCommand { get; }

    protected DocumentDataViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _currentValueHelper = this.WhenAnyValue(x => x.Property)
            .Select(GetCurrentValue).ToProperty(this, x => x.CurrentValue);

        SetValueCommand = ReactiveCommand.CreateFromObservable<T?, Unit>(value =>
        {
            SetCurrentValue(value);
            OnCurrentValueChanged();
            return Observable.Empty<Unit>();
        });
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        
        Property.Changed += NodeOnChanged;
        Disposable.Create(Property, (n) => n.Changed -= NodeOnChanged).DisposeWith(FullDeactivationDisposables);
    }

    protected virtual void OnCurrentValueChanged()
    {
        this.RaisePropertyChanged(nameof(CurrentValue));
    }

    protected virtual void NodeOnChanged()
    {
        this.RaisePropertyChanged(nameof(CurrentValue));
    }

    protected override void PropertyOnChanged()
    {
        base.PropertyOnChanged();
        
        this.RaisePropertyChanged(nameof(CurrentValue));
    }

    protected virtual T? GetCurrentValue(PropertyNode node)
    {
        return node.GetValue<T>();
    }

    protected virtual void SetCurrentValue(T? value)
    {
        Property.SetValue(value);
    }
}