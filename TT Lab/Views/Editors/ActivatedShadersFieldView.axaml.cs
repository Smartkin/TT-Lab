using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class ActivatedShadersFieldView : DocumentBaseView<ActivatedShadersFieldViewModel>
{
    public ActivatedShadersFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Types, view => view.TypesContainer.ItemsSource).DisposeWith(disposables);
    }
}
