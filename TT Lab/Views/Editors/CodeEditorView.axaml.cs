using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using ReactiveUI.Avalonia;
using ReactiveUI.Validation.Extensions;
using TextMateSharp.Grammars;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class CodeEditorView : DocumentBaseView<CodeEditorViewModel>
{
    public CodeEditorView()
    {
        InitializeComponent();
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.Code, view => view.Editor.Text).DisposeWith(disposables);
        
        this.BindValidation(ViewModel, viewModel => viewModel.Code, view => view.CodeParsingError.Text).DisposeWith(disposables);
    }
}