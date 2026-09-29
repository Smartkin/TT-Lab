using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class AngleFieldView : DocumentBaseView<AngleFieldViewModel>
{
    public AngleFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.DegreesText, view => view.DegreesField.Text).DisposeWith(disposables);
    }
}
