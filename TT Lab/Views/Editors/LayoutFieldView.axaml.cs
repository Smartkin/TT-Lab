using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class LayoutFieldView : DocumentBaseView<LayoutFieldViewModel>
{
    public LayoutFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Layouts, view => view.LayoutChoices.ItemsSource).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.CanChooseLayout, view => view.LayoutChoices.IsEnabled).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.SelectedLayout, view => view.LayoutChoices.SelectedItem).DisposeWith(disposables);
    }
}
