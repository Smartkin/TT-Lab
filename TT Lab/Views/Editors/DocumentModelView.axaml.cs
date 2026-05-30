using System.Reactive.Disposables;
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

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Nodes,
            view => view.EditorsContainer.ItemsSource).DisposeWith(disposables);

        this.OneWayBind(ViewModel, viewModel => viewModel.Constructors,
            view => view.ConstructibleTypesContainer.ItemsSource).DisposeWith(disposables);
    }

    private void ConstructorClicked(object? sender, RoutedEventArgs e)
    {
        ConstructNewInstance.Flyout?.Hide();
    }
}