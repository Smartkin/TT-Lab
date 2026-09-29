using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class DocumentCompositeView : DocumentBaseView<DocumentCompositeViewModel>
{
    // A virtualizing panel takes the items it hasn't made for the average size of the ones it has. That's way off for items that expand,
    // like a list of structs with one of them expanded: the list jumped around while scrolling, and with the expanded item near the end
    // of a long list the extent flipped between the average with and without it as the panel made and dropped it, the scroll viewer
    // clamped its offset each time and the layout cycled for seconds (the default chunk's 255 particle systems). So only lists of plain
    // fields, whose rows are all the same size, get virtualized, whatever the length of the list
    private static readonly ITemplate<Panel?> StackingPanel = new FuncTemplate<Panel?>(() => new StackPanel());
    private static readonly ITemplate<Panel?> VirtualizingPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());

    public DocumentCompositeView()
    {
        InitializeComponent();
    }

    // The panel is picked from the view model's items
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Decided from the property's elements, the nodes only get made once it's expanded
        var isVirtualized = ViewModel is DocumentCollectionViewModel && !ViewModel.HasExpandableItems;
        var panel = isVirtualized ? VirtualizingPanel : StackingPanel;
        if (EditorsContainer.ItemsPanel != panel)
        {
            EditorsContainer.ItemsPanel = panel;
        }

        this.OneWayBind(ViewModel, viewModel => viewModel.Nodes,
            view => view.EditorsContainer.ItemsSource).DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.ScrollRequest)
            .WhereNotNull()
            .Subscribe(_ => BringNodeIntoView(EditorsContainer, ViewModel!.Nodes, ViewModel.TakeScrollRequest()))
            .DisposeWith(disposables);

        if (ViewModel?.AddCommand != null)
        {
            this.BindCommand(ViewModel, viewModel => viewModel.AddCommand, view => view.AddNewItem,
                nameof(AddNewItem.Click));
        }

        // A list the game takes a number of elements of says so on its add button
        ToolTip.SetTip(AddNewItem, (ViewModel as DocumentCollectionViewModel)?.AddItemHint);
    }

    private void Caption_OnContextRequested(object? sender, Avalonia.Controls.ContextRequestedEventArgs e) => OverrideMenu.Show(sender, e);
}
