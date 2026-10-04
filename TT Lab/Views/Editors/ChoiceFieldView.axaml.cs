using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class ChoiceFieldView : DocumentBaseView<ChoiceFieldViewModel>
{
    public ChoiceFieldView()
    {
        InitializeComponent();
    }

    // The combo box drops its selection when its items change
    protected override bool RebindsOnRecycle => true;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.Choices, view => view.Choices.ItemsSource).DisposeWith(disposables);
        this.Bind(ViewModel, viewModel => viewModel.SelectedChoice, view => view.Choices.SelectedItem).DisposeWith(disposables);
    }
}
