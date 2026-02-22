using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Validation.Extensions;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class TextFieldView : ReactiveUserControl<TextFieldViewModel>
{
    public TextFieldView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, viewModel => viewModel.Text, view => view.TextField.Text).DisposeWith(disposables);

            this.BindValidation(ViewModel, viewModel => viewModel.Text, view => view.TextFieldError.Text).DisposeWith(disposables);
        });
    }
}