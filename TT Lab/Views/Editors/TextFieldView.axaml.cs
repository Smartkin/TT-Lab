using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Validation.Extensions;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class TextFieldView : DocumentBaseView<TextFieldViewModel>
{
    public TextFieldView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.Text, view => view.TextField.Text).DisposeWith(disposables);

        this.BindValidation(ViewModel, viewModel => viewModel.Text, view => view.TextFieldError.Text).DisposeWith(disposables);
    }
}