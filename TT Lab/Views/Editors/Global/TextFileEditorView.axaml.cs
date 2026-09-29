using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using ReactiveUI;
using TT_Lab.ViewModels.Editors.Global;

namespace TT_Lab.Views.Editors.Global;

public partial class TextFileEditorView : DocumentBaseView<TextFileEditorViewModel>
{
    // Glyphs are as tall as the editor's lines, which are bigger while they're shown so the glyphs stay readable
    private const double TextFontSize = 14.0;
    private const double GlyphFontSize = 20.0;

    private PsfGlyphGenerator? _generator;
    private int _caretAfterChange;
    private bool _loaded;

    // Keeps state of its own about what it shows
    public override bool CanBeRecycled => false;

    public TextFileEditorView()
    {
        InitializeComponent();
        Editor.TextArea.TextEntering += OnTextEntering;
        // The editor scrolls itself, letting its bring into view requests through makes the document's scroll viewers jump around on every click
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
    }

    // The height of the editor outside of the side pane
    private const double InlineHeight = 640.0;

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        this.OneWayBind(ViewModel, viewModel => viewModel.IsInSidePane, view => view.Editor.Height, inPane => inPane ? double.NaN : InlineHeight).DisposeWith(disposables);
        // The document's history is the editor's undo, like the code editor's: the editor's own stack would undo the same typing
        // again or the loading of the text. Text from outside (undo, another editor of the value) replaces only what differs,
        // which keeps the rest of the text where it is and puts the caret at the change
        Editor.Document.UndoStack.SizeLimit = 0;
        this.WhenAnyValue(view => view.ViewModel!.Text)
            .Where(text => text != null)
            .Subscribe(ApplyText!)
            .DisposeWith(disposables);

        Observable.FromEventPattern(handler => Editor.TextChanged += handler, handler => Editor.TextChanged -= handler)
            .Subscribe(_ =>
            {
                _caretAfterChange = Editor.CaretOffset;
                ViewModel!.Text = Editor.Text;
            })
            .DisposeWith(disposables);

        // Typing on keeps changing the same step of the history, moving the caret somewhere else ends it
        Editor.TextArea.Caret.PositionChanged += OnCaretMoved;
        Disposable.Create(() => Editor.TextArea.Caret.PositionChanged -= OnCaretMoved).DisposeWith(disposables);

        this.WhenAnyValue(view => view.ViewModel!.Glyphs)
            .Subscribe(glyphs =>
            {
                var generators = Editor.TextArea.TextView.ElementGenerators;
                if (_generator != null)
                {
                    generators.Remove(_generator);
                    _generator = null;
                }

                if (glyphs != null)
                {
                    _generator = new PsfGlyphGenerator(glyphs);
                    generators.Add(_generator);
                }

                Editor.FontSize = glyphs != null ? GlyphFontSize : TextFontSize;
                Editor.TextArea.TextView.Redraw();
            })
            .DisposeWith(disposables);
    }

    private void ApplyText(string text)
    {
        var isLoad = !_loaded;
        _loaded = true;
        var document = Editor.Document;
        var current = document.Text;
        if (current == text)
        {
            return;
        }

        var prefix = 0;
        var shortest = Math.Min(current.Length, text.Length);
        while (prefix < shortest && current[prefix] == text[prefix])
        {
            prefix++;
        }

        var suffix = 0;
        while (suffix < shortest - prefix && current[current.Length - 1 - suffix] == text[text.Length - 1 - suffix])
        {
            suffix++;
        }

        document.Replace(prefix, current.Length - prefix - suffix, text.Substring(prefix, text.Length - prefix - suffix));
        if (isLoad)
        {
            Editor.CaretOffset = 0;
            return;
        }

        Editor.CaretOffset = text.Length - suffix;
        _caretAfterChange = Editor.CaretOffset;
        Editor.TextArea.Caret.BringCaretToView();
    }

    // Typing moves the caret as well, so the user moved it when it isn't where the last text change left it. Looked at once the
    // input is done, since the two events come in either order
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

    // What isn't Latin-1 can't be saved in the game's text files
    private static void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (e.Text != null && e.Text.Any(character => character > 'ÿ'))
        {
            e.Handled = true;
        }
    }

    private void Insert(object? sender, RoutedEventArgs e)
    {
        var text = (sender as Control)?.Tag switch
        {
            char character => character.ToString(),
            string value => value,
            _ => null
        };
        if (text == null)
        {
            return;
        }

        Editor.TextArea.PerformTextInput(text);
        Editor.TextArea.Focus();
    }
}
