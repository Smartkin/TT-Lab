using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class BoolFieldView : DocumentBaseView<BoolFieldViewModel>
{
    public BoolFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.IsChecked, view => view.BoolField.IsChecked).DisposeWith(disposables);
    }
}