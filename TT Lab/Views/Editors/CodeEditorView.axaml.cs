using System.Reactive.Disposables;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TextMateSharp.Grammars;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class CodeEditorView : DocumentBaseView<CodeEditorViewModel>
{
    public CodeEditorView()
    {
        InitializeComponent();
        
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMateInstallation = Editor.InstallTextMate(registryOptions);
        textMateInstallation.SetGrammar(registryOptions.GetScopeByExtension(".cs"));
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.Bind(ViewModel, viewModel => viewModel.Code, view => view.Editor.Text);
    }
}