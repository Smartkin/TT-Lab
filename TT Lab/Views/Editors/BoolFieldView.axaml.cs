using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class BoolFieldView : ReactiveUserControl<BoolFieldViewModel>
{
    public BoolFieldView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, viewModel => viewModel.IsChecked, view => view.BoolField.IsChecked).DisposeWith(disposables);
        });
    }
}