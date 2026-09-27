using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors.Code;

namespace TT_Lab.Views.Editors.Code;

public partial class OgiAnimationFieldView : DocumentBaseView<OgiAnimationFieldViewModel>
{
    public OgiAnimationFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Animations, view => view.AnimationChoices.ItemsSource).DisposeWith(disposables);
        this.OneWayBind(ViewModel, viewModel => viewModel.CanChooseAnimation, view => view.AnimationChoices.IsEnabled).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.SelectedAnimation, view => view.AnimationChoices.SelectedItem).DisposeWith(disposables);
    }
}
