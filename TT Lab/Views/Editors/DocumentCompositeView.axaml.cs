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
    // A virtualizing panel scrolls by estimating the size of the items it hasn't made from the ones it has. That's way off for items that
    // expand, like a list of structs with one of them expanded, and makes the list jump around while scrolling, so those are only virtualized
    // when there are too many to make all of them
    private const int VirtualizeExpandableAbove = 100;
    private static readonly ITemplate<Panel?> StackingPanel = new FuncTemplate<Panel?>(() => new StackPanel());
    private static readonly ITemplate<Panel?> VirtualizingPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());

    public DocumentCompositeView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Decided from the property's elements, the nodes only get made once it's expanded
        var isVirtualized = !ViewModel!.HasExpandableItems || ViewModel.Property.Children.Count > VirtualizeExpandableAbove;
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
    }

    private void Caption_OnContextRequested(object? sender, Avalonia.Controls.ContextRequestedEventArgs e) => OverrideMenu.Show(sender, e);
}
