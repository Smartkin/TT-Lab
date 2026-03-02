using System;
using System.Reactive.Disposables.Fluent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using Dock.Model.Core;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TextMateSharp.Grammars;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class LogView : ReactiveUserControl<LogViewModel>
{
    public LogView()
    {
        InitializeComponent();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, viewModel => viewModel.Text, view => view.LogText.Text).DisposeWith(disposables);
            this.WhenAnyValue(x => x.LogText.LineCount)
                .BindTo(this, x => x.ViewModel!.LinesAmount).DisposeWith(disposables);
        });
    }

    private void LogText_OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var logBox = (TextEditor)sender!;
        Log.SetViewModel(ViewModel!);
        
        var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        var textMateInstallation = logBox.InstallTextMate(registryOptions);
        logBox.Options = new TextEditorOptions
        {
            AllowScrollBelowDocument = false,
            EnableHyperlinks = true,
        };
        textMateInstallation.SetGrammar(registryOptions.GetScopeByExtension(".log"));
        logBox.TextChanged += LogBoxOnTextChanged;
    }

    private void LogBoxOnTextChanged(object? sender, EventArgs e)
    {
        var logBox = (TextEditor)sender!;
        logBox.ScrollToEnd();
    }
}