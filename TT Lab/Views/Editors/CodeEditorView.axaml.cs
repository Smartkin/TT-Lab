using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using ReactiveUI.Validation.Extensions;
using TextMateSharp.Grammars;
using TT_Lab.AgentLab;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

public partial class CodeEditorView : DocumentBaseView<CodeEditorViewModel>
{
    private static readonly string[] MonospaceFonts =
    [
        "Cascadia Code", "Cascadia Mono", "Consolas", "JetBrains Mono", "DejaVu Sans Mono", "Adwaita Mono", "Liberation Mono", "Noto Sans Mono",
        "Ubuntu Mono", "Menlo", "Courier New"
    ];

    // Avalonia logs a warning listing every installed font whenever a font in a fallback list isn't installed, and it does so for every
    // text run it measures. With a missing font in the list the editor spent all its time logging, so only an installed font is given
    private static readonly Lazy<FontFamily> MonospaceFont = new(() =>
    {
        var installedFonts = FontManager.Current.SystemFonts.Select(font => font.Name).ToList();
        var installed = new HashSet<string>(installedFonts, StringComparer.OrdinalIgnoreCase);
        var name = MonospaceFonts.FirstOrDefault(installed.Contains)
                   ?? installedFonts.FirstOrDefault(font => font.Contains("Mono", StringComparison.OrdinalIgnoreCase));
        return name != null ? new FontFamily(name) : FontFamily.Default;
    });

    private readonly ErrorLineRenderer _errorLineRenderer = new();

    public CodeEditorView()
    {
        InitializeComponent();
        Editor.FontFamily = MonospaceFont.Value;

        Editor.Options = new TextEditorOptions
        {
            AllowScrollBelowDocument = false,
            ConvertTabsToSpaces = true,
            IndentationSize = 4
        };
        Editor.TextArea.TextView.BackgroundRenderers.Add(_errorLineRenderer);

        // The editor scrolls itself, letting its bring into view requests through makes the document's scroll viewers jump around on every click
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        var registryOptions = new AgentLabRegistryOptions(ThemeName.DarkPlus);
        var textMate = Editor.InstallTextMate(registryOptions);
        if (ViewModel!.IsAgentLabCode)
        {
            textMate.SetGrammar(AgentLabRegistryOptions.ScopeName);
            new AgentLabCodeCompletion(Editor, ViewModel.ActionDefinitionsFile, ViewModel.IsAgentLabCommandList).DisposeWith(disposables);
        }

        Disposable.Create(textMate, installation => installation.Dispose()).DisposeWith(disposables);

        // Replacing the text moves the caret and scroll back to the start so it's only done when the code didn't come from the editor
        this.WhenAnyValue(view => view.ViewModel!.Code)
            .Where(code => code != null && code != Editor.Text)
            .Subscribe(code => Editor.Document = new TextDocument(code))
            .DisposeWith(disposables);

        Observable.FromEventPattern(handler => Editor.TextChanged += handler, handler => Editor.TextChanged -= handler)
            .Subscribe(_ => ViewModel!.Code = Editor.Text)
            .DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.CodeStatus)
            .Subscribe(status =>
            {
                _errorLineRenderer.Line = status.IsError ? status.Line : 0;
                Editor.TextArea.TextView.InvalidateLayer(_errorLineRenderer.Layer);
            })
            .DisposeWith(disposables);

        this.BindValidation(ViewModel, viewModel => viewModel.Code, view => view.CodeParsingError.Text).DisposeWith(disposables);
    }

    private sealed class ErrorLineRenderer : IBackgroundRenderer
    {
        private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.FromArgb(0x50, 0xE0, 0x30, 0x30));

        public int Line { get; set; }

        public KnownLayer Layer => KnownLayer.Background;

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (Line <= 0 || textView.Document == null || Line > textView.Document.LineCount)
            {
                return;
            }

            var visualLine = textView.GetVisualLine(Line);
            if (visualLine == null)
            {
                return;
            }

            var top = visualLine.VisualTop - textView.ScrollOffset.Y;
            drawingContext.FillRectangle(ErrorBrush, new Rect(0, top, textView.Bounds.Width, visualLine.Height));
        }
    }
}
