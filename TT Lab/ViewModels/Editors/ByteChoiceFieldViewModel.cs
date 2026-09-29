using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A byte picked from the values the game gives a meaning to
/// </summary>
public partial class ByteChoiceFieldViewModel : DocumentDataViewModel<Byte>
{
    private readonly Func<int, NamedChoice> _find;

    [Reactive]
    private NamedChoice? _selectedChoice;

    public ByteChoiceFieldViewModel(DocumentViewModel document, PropertyNode node, IReadOnlyList<NamedChoice> choices, Func<int, NamedChoice> find,
        params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        _find = find;
        var shown = new List<NamedChoice>(choices);
        var current = find(CurrentValue);
        // A value the game has no meaning for is still shown, so it can be put right
        if (!shown.Contains(current))
        {
            shown.Add(current);
        }

        Choices = shown;
        _selectedChoice = current;
    }

    public IReadOnlyList<NamedChoice> Choices { get; }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        this.WhenAnyValue(x => x.SelectedChoice)
            .Skip(1)
            .WhereNotNull()
            .Where(choice => choice.Value != CurrentValue)
            .Subscribe(choice => SetCurrentValue((Byte)choice.Value))
            .DisposeWith(disposables);
    }

    protected override void OnCurrentValueChanged()
    {
        SelectedChoice = _find(CurrentValue);
    }
}
