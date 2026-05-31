using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
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

    private readonly Subject<Unit> _valueChanged = new();

    protected DocumentDataViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _currentValueHelper = Observable.Merge(
                this.WhenAnyValue(x => x.Property).Select(_ => Unit.Default),
                _valueChanged
            )
            .Select(_ => GetCurrentValue())
            .ToProperty(this, nameof(CurrentValue));

        SetValueCommand = ReactiveCommand.CreateFromObservable<T?, Unit>(value =>
        {
            Property.SetValue(value);
            _valueChanged.OnNext(Unit.Default);
            OnCurrentValueChanged();
            return Observable.Empty<Unit>();
        });
    }

    protected sealed override void PropertyOnChanged()
    {
        base.PropertyOnChanged();
        
        SetValueCommand.Execute(GetCurrentValue());
    }

    protected virtual void OnCurrentValueChanged()
    {
    }

    protected virtual T? GetCurrentValue()
    {
        return Property.GetValue<T>();
    }

    protected virtual void SetCurrentValue(T? value)
    {
        SetValueCommand.Execute(value);
    }
}