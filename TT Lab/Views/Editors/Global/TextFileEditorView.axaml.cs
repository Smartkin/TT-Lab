using System;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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

    public TextFileEditorView()
    {
        InitializeComponent();
        Editor.TextArea.TextEntering += OnTextEntering;
        // The editor scrolls itself, letting its bring into view requests through makes the document's scroll viewers jump around on every click
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
    }

    protected override void HandleActivation(CompositeDisposable disposables)
    {
        // Replacing the text moves the caret and scroll back to the start so it's only done when the text didn't come from the editor
        this.WhenAnyValue(view => view.ViewModel!.Text)
            .Where(text => text != null && text != Editor.Text)
            .Subscribe(text => Editor.Document = new TextDocument(text))
            .DisposeWith(disposables);

        Observable.FromEventPattern(handler => Editor.TextChanged += handler, handler => Editor.TextChanged -= handler)
            .Subscribe(_ => ViewModel!.Text = Editor.Text)
            .DisposeWith(disposables);

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
