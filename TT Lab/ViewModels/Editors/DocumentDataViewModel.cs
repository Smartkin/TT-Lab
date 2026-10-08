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
        ShowCurrentValue();
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

    // Only shows the node's value again. Setting what it read back changed nothing while getters give back the value they keep, but a
    // value made anew every time (the scenery's bounds) never equals the last one: it set itself again until the stack ran out
    protected sealed override void PropertyOnChanged()
    {
        base.PropertyOnChanged();

        ShowCurrentValue();
    }

    // Editors only hear their node while they're shown: one shown again (a list's row scrolled back into view, a chunk's inspector switched
    // back to) showed the value it had when it was hidden
    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        if (!PropertyNode.IsSameValue(_currentValue, GetCurrentValue()))
        {
            ShowCurrentValue();
        }
    }

    private void ShowCurrentValue()
    {
        CurrentValue = GetCurrentValue();
        OnCurrentValueChanged();
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