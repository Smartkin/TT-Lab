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
    public DocumentCompositeView()
    {
        InitializeComponent();
    }

    // Its items are bound to the view model's nodes when it's activated
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
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

    private void Caption_OnContextRequested(object? sender, Avalonia.Controls.ContextRequestedEventArgs e) => PropertyMenu.Show(sender, e);
}
