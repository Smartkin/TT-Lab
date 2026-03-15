using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
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

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Nodes,
            view => view.EditorsContainer.ItemsSource).DisposeWith(disposables);

        if (ViewModel?.AddCommand != null)
        {
            this.BindCommand(ViewModel, viewModel => viewModel.AddCommand, view => view.AddNewItem,
                nameof(AddNewItem.Click));
        }
    }
}