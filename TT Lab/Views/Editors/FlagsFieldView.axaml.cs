using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class FlagsFieldView : DocumentBaseView<FlagsFieldViewModel>
{
    public FlagsFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.BoolFields, view => view.FlagsContainer.ItemsSource).DisposeWith(disposables);
    }
}