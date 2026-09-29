using System;
using Twinsanity.AgentLab;
using Avalonia.Interactivity;
using Avalonia.Input;
using System.Threading.Tasks;
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
using Avalonia.Threading;
using Splat;
using TT_Lab.AgentLab;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;

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
    private int _caretAfterChange;
    private bool _loaded;

    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

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

    // The height of the editor outside of the side pane
    private const double InlineHeight = 640.0;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.IsInSidePane, view => view.Editor.Height, inPane => inPane ? double.NaN : InlineHeight).DisposeWith(disposables);
        var registryOptions = new AgentLabRegistryOptions(ThemeName.DarkPlus);
        var textMate = Editor.InstallTextMate(registryOptions);
        if (ViewModel!.IsAgentLabCode)
        {
            textMate.SetGrammar(AgentLabRegistryOptions.ScopeName);
            var definitions = ViewModel.ActionDefinitionsFile;
            // the first completion parses the definitions, better done now than on the first keystroke
            Task.Run(() => AgentLabCompletion.Warm(definitions));
            new AgentLabCodeCompletion(Editor, definitions, ViewModel.IsAgentLabCommandList, ViewModel.GetBehaviourSuggestions).DisposeWith(disposables);
            new AgentLabHoverHints(Editor, definitions, DescribeBehaviour).DisposeWith(disposables);
            new AgentLabNavigation(Editor, ViewModel.FindBehaviour, OpenAsset).DisposeWith(disposables);
        }

        Disposable.Create(textMate, installation => installation.Dispose()).DisposeWith(disposables);

        // The document's history is the editor's undo: Ctrl+Z goes to it (the tabs bind the key), it sets the code back and the text
        // follows. The editor's own stack would undo the same typing a second time, or the loading of the text
        Editor.Document.UndoStack.SizeLimit = 0;

        // Code that didn't come from the editor (the document's history, another editor of the value) replaces only what differs, which
        // keeps the rest of the text where it is and puts the caret at the change
        this.WhenAnyValue(view => view.ViewModel!.Code)
            .Where(code => code != null)
            .Subscribe(ApplyCode)
            .DisposeWith(disposables);

        Observable.FromEventPattern(handler => Editor.TextChanged += handler, handler => Editor.TextChanged -= handler)
            .Subscribe(_ =>
            {
                _caretAfterChange = Editor.CaretOffset;
                ViewModel!.Code = Editor.Text;
            })
            .DisposeWith(disposables);

        // Typing on keeps changing the same step of the history, moving the caret somewhere else ends it
        Editor.TextArea.Caret.PositionChanged += OnCaretMoved;
        Disposable.Create(() => Editor.TextArea.Caret.PositionChanged -= OnCaretMoved).DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.CodeStatus)
            .Subscribe(status =>
            {
                _errorLineRenderer.Line = status.IsError ? status.Line : 0;
                Editor.TextArea.TextView.InvalidateLayer(_errorLineRenderer.Layer);
            })
            .DisposeWith(disposables);

        this.BindValidation(ViewModel, viewModel => viewModel.Code, view => view.CodeParsingError.Text).DisposeWith(disposables);
    }

    private void ApplyCode(string code)
    {
        var isLoad = !_loaded;
        _loaded = true;
        var document = Editor.Document;
        var current = document.Text;
        if (current == code)
        {
            return;
        }

        var prefix = 0;
        var shortest = Math.Min(current.Length, code.Length);
        while (prefix < shortest && current[prefix] == code[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < shortest - prefix && current[current.Length - 1 - suffix] == code[code.Length - 1 - suffix])
        {
            suffix++;
        }

        document.Replace(prefix, current.Length - prefix - suffix, code.Substring(prefix, code.Length - prefix - suffix));
        if (isLoad)
        {
            // the text getting loaded, the editor starts at its top
            Editor.CaretOffset = 0;
            return;
        }

        Editor.CaretOffset = code.Length - suffix;
        _caretAfterChange = Editor.CaretOffset;
        Editor.TextArea.Caret.BringCaretToView();
    }

    // Typing moves the caret as well, so the user moved it when it isn't where the last text change left it. Looked at once the
    // input is done, since the two events come in either order, and by the offset because the editor raises the event again when
    // it lays the line out
    private void OnCaretMoved(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Editor.CaretOffset == _caretAfterChange)
            {
                return;
            }

            _caretAfterChange = Editor.CaretOffset;
            ViewModel?.EndTypingStep();
        }, DispatcherPriority.Background);
    }

    private string DescribeBehaviour(string reference)
    {
        return ViewModel?.FindBehaviour(reference) is { } graph
            ? $"{graph.URI}\nCtrl+click or F12 opens it"
            : "No behaviour of this name in the package or the packages it depends on";
    }

    private static void OpenAsset(IAsset asset) => Locator.Current.GetService<ILabManager>()!.OpenEditor(asset);

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
