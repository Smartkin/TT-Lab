using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public class DocumentDataViewModel<T> : DocumentNodeViewModel
{
    private T? _currentValue;
    private ReactiveCommand<T?, Unit>? _setValueCommand;

    // Made when first used, most editors of a long list never get to change anything
    public ReactiveCommand<T?, Unit> SetValueCommand => _setValueCommand ??= ReactiveCommand.CreateFromObservable<T?, Unit>(value =>
    {
        Property.SetValue(value);
        CurrentValue = GetCurrentValue();
        OnCurrentValueChanged();
        return Observable.Empty<Unit>();
    });

    protected DocumentDataViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _currentValue = GetCurrentValue();
    }

    public T? CurrentValue
    {
        get => _currentValue;
        private set => this.RaiseAndSetIfChanged(ref _currentValue, value);
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