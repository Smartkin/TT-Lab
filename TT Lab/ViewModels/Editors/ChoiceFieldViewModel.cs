using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A whole number, of whatever width the property has, picked from the values the game gives a meaning to
/// </summary>
public partial class ChoiceFieldViewModel : DocumentDataViewModel<object>
{
    private readonly Func<IReadOnlyList<NamedChoice>> _listChoices;
    private readonly Func<int, NamedChoice> _find;
    private readonly PropertyNode? _follows;
    // Set while the choices and the selection show the value: a combo box whose list changes hands its old selection back
    private bool _isShowing;

    [Reactive]
    private NamedChoice? _selectedChoice;

    [Reactive]
    private IReadOnlyList<NamedChoice> _choices = [];

    public ChoiceFieldViewModel(DocumentViewModel document, PropertyNode node, IReadOnlyList<NamedChoice> choices, Func<int, NamedChoice> find,
        params DocumentNodeViewModel[] dependencies) : this(document, node, () => choices, find, null, dependencies)
    {
    }

    /// <summary>
    /// Choices that depend on another value of the same owner (an object's sub types on its type), listed again when it changes
    /// </summary>
    public ChoiceFieldViewModel(DocumentViewModel document, PropertyNode node, Func<IReadOnlyList<NamedChoice>> choices, Func<int, NamedChoice> find,
        PropertyNode? follows, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _listChoices = choices;
        _find = find;
        _follows = follows;
        ShowChoices();
    }

    private int Current => (int)Convert.ToInt64(CurrentValue);

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.SelectedChoice)
            .Skip(1)
            .WhereNotNull()
            .Where(choice => !_isShowing && choice.Value != Current)
            .Subscribe(choice => SetCurrentValue(Convert.ChangeType(choice.Value, Property.PropertyType)))
            .DisposeWith(disposables);
        if (_follows != null)
        {
            _follows.Changed += ShowChoices;
            Disposable.Create(() => _follows.Changed -= ShowChoices).DisposeWith(disposables);
            ShowChoices();
        }
    }

    protected override void OnCurrentValueChanged()
    {
        ShowChoices();
    }

    private void ShowChoices()
    {
        var shown = new List<NamedChoice>(_listChoices());
        var current = _find(Current);
        // A value the game has no meaning for is still shown, so it can be put right
        if (!shown.Contains(current))
        {
            shown.Add(current);
        }

        _isShowing = true;
        try
        {
            if (!shown.SequenceEqual(Choices))
            {
                Choices = shown;
            }

            SelectedChoice = current;
        }
        finally
        {
            _isShowing = false;
        }
    }
}
