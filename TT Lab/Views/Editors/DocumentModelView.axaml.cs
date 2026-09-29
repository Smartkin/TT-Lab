using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class DocumentModelView : DocumentBaseView<DocumentModelViewModel>
{
    public DocumentModelView()
    {
        InitializeComponent();
    }

    // The constructors' flyout and the scroll requests are bound from the view model
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Nodes,
            view => view.EditorsContainer.ItemsSource).DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.ScrollRequest)
            .WhereNotNull()
            .Subscribe(_ => BringNodeIntoView(EditorsContainer, ViewModel!.Nodes, ViewModel.TakeScrollRequest()))
            .DisposeWith(disposables);

        this.OneWayBind(ViewModel, viewModel => viewModel.Constructors,
            view => view.ConstructibleTypesContainer.ItemsSource).DisposeWith(disposables);
    }

    private void ConstructorClicked(object? sender, RoutedEventArgs e)
    {
        ConstructNewInstance.Flyout?.Hide();
    }

    private void Caption_OnContextRequested(object? sender, Avalonia.Controls.ContextRequestedEventArgs e) => OverrideMenu.Show(sender, e);
}
