using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class SplineSampleFieldView : DocumentBaseView<SplineSampleFieldViewModel>
{
    public SplineSampleFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.IsKey, view => view.KeyField.IsChecked).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.OffsetText, view => view.OffsetField.Text).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.ShareText, view => view.ShareField.Text).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.IsKey, view => view.OffsetField.IsEnabled).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.IsKey, view => view.ShareField.IsEnabled).DisposeWith(disposables);
    }
}
