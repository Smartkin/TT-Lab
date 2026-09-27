using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

public partial class LayoutFieldViewModel : DocumentDataViewModel<Int32?>
{
    [Reactive]
    private ChunkLayout? _selectedLayout;

    public LayoutFieldViewModel(DocumentViewModel document, PropertyNode node, params DocumentNodeViewModel[] dependencies) : base(document, node, dependencies)
    {
        var layouts = ChunkLayouts.GetChoices(node.Target as IAsset).ToList();
        var current = ChunkLayouts.Find(CurrentValue);
        // Whatever it's in can always be seen, even when it's somewhere it shouldn't be
        if (current != null && !layouts.Contains(current))
        {
            layouts.Add(current);
        }

        Layouts = layouts;
        _selectedLayout = current;
    }

    public IReadOnlyList<ChunkLayout> Layouts { get; }

    // Assets outside of layouts have none to pick and collision surfaces only have theirs
    public bool CanChooseLayout => CurrentValue != null && Layouts.Count > 1;

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        this.WhenAnyValue(x => x.SelectedLayout)
            .Skip(1)
            .WhereNotNull()
            .Where(layout => layout.Id != CurrentValue)
            .Subscribe(layout => SetCurrentValue(layout.Id))
            .DisposeWith(disposables);
    }

    protected override void OnCurrentValueChanged()
    {
        SelectedLayout = ChunkLayouts.Find(CurrentValue);
        this.RaisePropertyChanged(nameof(CanChooseLayout));
    }
}
