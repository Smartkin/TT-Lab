using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using Twinsanity.AgentLab;

namespace TT_Lab.AgentLab;

/// <summary>
/// Completion list and parameter hints for behaviour scripts in a text editor
/// </summary>
/// <remarks>
/// The list opens once a word starts being typed, after the characters that start something with a known set of values
/// (attributes, <c>execute</c>, <c>if</c>, assignments) and on Ctrl+Space. Parameter hints show while the caret is within an
/// action's or a condition's parentheses and on Ctrl+Shift+Space.
/// </remarks>
public sealed class AgentLabCodeCompletion : IDisposable
{
    private readonly TextEditor _editor;
    private readonly string _actionDefinitionsFile;
    private readonly bool _commandsOnly;
    private readonly Func<IEnumerable<AgentLabCompletionItem>>? _behaviours;
    private CompletionWindow? _completionWindow;
    private OverloadInsightWindow? _signatureWindow;
    // The library hands out the same item objects every time, so their rows are made once
    private readonly Dictionary<AgentLabCompletionItem, AgentLabCompletionData> _rows = new(ReferenceEqualityComparer.Instance);

    public AgentLabCodeCompletion(TextEditor editor, string actionDefinitionsFile, bool commandsOnly = false, Func<IEnumerable<AgentLabCompletionItem>>? behaviours = null)
    {
        _editor = editor;
        _actionDefinitionsFile = actionDefinitionsFile;
        _commandsOnly = commandsOnly;
        _behaviours = behaviours;
        _editor.TextArea.TextEntering += OnTextEntering;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.Caret.PositionChanged += OnCaretMoved;
        _editor.TextArea.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    internal CompletionWindow? CompletionWindow => _completionWindow;
    internal OverloadInsightWindow? SignatureWindow => _signatureWindow;

    public void Dispose()
    {
        _editor.TextArea.TextEntering -= OnTextEntering;
        _editor.TextArea.TextEntered -= OnTextEntered;
        _editor.TextArea.Caret.PositionChanged -= OnCaretMoved;
        _editor.TextArea.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        _completionWindow?.Close();
        _signatureWindow?.Close();
    }

    private static bool IsIdentifierChar(char character) => char.IsLetterOrDigit(character) || character == '_';

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        e.Handled = true;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            UpdateSignature(true);
            return;
        }

        _completionWindow?.Close();
        ShowCompletion(_ => true, false);
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completionWindow == null || string.IsNullOrEmpty(e.Text) || IsIdentifierChar(e.Text[0]))
        {
            return;
        }

        // Starting a call or ending a statement takes the selected suggestion, anything else means the word is done
        if (e.Text[0] is '(' or ';')
        {
            _completionWindow.CompletionList.RequestInsertion(e);
            return;
        }

        _completionWindow.Close();
    }

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var character = e.Text[0];
        if (_completionWindow == null)
        {
            switch (character)
            {
                case var _ when IsIdentifierChar(character) && !char.IsAsciiDigit(character) && IsStartOfWord():
                    ShowCompletion(_ => true, true);
                    break;
                case '[':
                    ShowCompletion(_ => true, false);
                    break;
                // Within an attribute's or a state's parentheses only, a call's parameters can be anything
                case '(':
                    ShowCompletion(items => items.All(item => item.Kind is AgentLabCompletionKind.State or AgentLabCompletionKind.ControlPacket or AgentLabCompletionKind.EnumValue or AgentLabCompletionKind.Behaviour), false);
                    break;
                case ' ' or '=' when IsAfterValueStart():
                    ShowCompletion(_ => true, false);
                    break;
            }
        }

        if (character is '(' or ',' or ')')
        {
            UpdateSignature(character == '(');
        }
    }

    // The parameter hint follows the caret: it opens once the caret is inside a call, wherever it got there from
    private void OnCaretMoved(object? sender, EventArgs e) => UpdateSignature(true);

    private bool IsStartOfWord()
    {
        var offset = _editor.CaretOffset;
        return offset < 2 || !IsIdentifierChar(_editor.Document.GetCharAt(offset - 2));
    }

    // Right after the words and symbols that are followed by one of a known set of values
    private bool IsAfterValueStart()
    {
        var offset = _editor.CaretOffset;
        var lineStart = _editor.Document.GetLineByOffset(offset).Offset;
        var before = _editor.Document.GetText(lineStart, offset - lineStart).TrimEnd();
        if (before.EndsWith('='))
        {
            return !before.EndsWith("==") && !before.EndsWith(">=") && !before.EndsWith("<=");
        }

        var wordStart = before.Length;
        while (wordStart > 0 && IsIdentifierChar(before[wordStart - 1]))
        {
            wordStart--;
        }

        var word = before[wordStart..];
        return word is "execute" or "if";
    }

    private void ShowCompletion(Func<IReadOnlyList<AgentLabCompletionItem>, bool> shouldShow, bool closeWhenWordIsDeleted)
    {
        var document = _editor.Document;
        var caret = _editor.CaretOffset;
        var result = AgentLabCompletion.GetCompletions(document.Text, caret, _actionDefinitionsFile, _commandsOnly, _behaviours);
        if (result.Items.Count == 0 || !shouldShow(result.Items))
        {
            return;
        }

        var window = new CompletionWindow(_editor.TextArea)
        {
            StartOffset = result.StartOffset,
            CloseWhenCaretAtBeginning = closeWhenWordIsDeleted,
            MinWidth = 280
        };
        // The list inserts on any press within it by default, which includes grabbing its scroll bar. Only releasing the
        // pointer over a suggestion inserts it instead
        window.CompletionList.CompletionAcceptAction = CompletionAcceptAction.DoubleTapped;
        var listBoxWithHandler = (Control?)null;
        void AttachReleaseHandler()
        {
            var listBox = window.CompletionList.ListBox;
            if (listBox == null || listBox == listBoxWithHandler)
            {
                return;
            }

            listBox.AddHandler(InputElement.PointerReleasedEvent, OnSuggestionReleased, RoutingStrategies.Bubble, true);
            listBoxWithHandler = listBox;
        }

        AttachReleaseHandler();
        window.CompletionList.TemplateApplied += (_, _) => AttachReleaseHandler();
        window.Closed += (_, _) =>
        {
            if (_completionWindow == window)
            {
                _completionWindow = null;
            }
        };
        _completionWindow = window;
        window.Show();

        // The list box only counts its items when they change while it's shown, and the arrow keys never go past that count.
        // Filling the list before showing it left the count at zero until something got typed
        foreach (var item in result.Items)
        {
            if (!_rows.TryGetValue(item, out var row))
            {
                _rows[item] = row = new AgentLabCompletionData(item);
            }

            window.CompletionList.CompletionData.Add(row);
        }

        window.CompletionList.SelectItem(document.GetText(result.StartOffset, caret - result.StartOffset));
    }

    private void OnSuggestionReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton != MouseButton.Left || _completionWindow == null)
        {
            return;
        }

        var item = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true);
        if (item?.DataContext is not ICompletionData data)
        {
            return;
        }

        _completionWindow.CompletionList.SelectedItem = data;
        _completionWindow.CompletionList.RequestInsertion(e);
    }

    private void UpdateSignature(bool open)
    {
        var signature = AgentLabCompletion.GetSignature(_editor.Document.Text, _editor.CaretOffset, _actionDefinitionsFile);
        if (signature == null)
        {
            _signatureWindow?.Close();
            return;
        }

        if (_signatureWindow == null)
        {
            if (!open)
            {
                return;
            }

            var window = new OverloadInsightWindow(_editor.TextArea)
            {
                StartOffset = _editor.CaretOffset,
                // The caret moving around within the call keeps the hint, it's closed once the caret leaves the call
                CloseAutomatically = false
            };
            window.Closed += (_, _) =>
            {
                if (_signatureWindow == window)
                {
                    _signatureWindow = null;
                }
            };
            _signatureWindow = window;
            window.Provider = new SignatureProvider(signature);
            window.Show();
            return;
        }

        _signatureWindow.Provider = new SignatureProvider(signature);
    }

    private sealed class SignatureProvider(AgentLabSignature signature) : IOverloadProvider
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public int SelectedIndex { get; set; }

        public int Count => 1;

        public string? CurrentIndexText => null;

        public object CurrentHeader => CreateHeader();

        public object CurrentContent => signature.Parameters.Count == 0
            ? "No parameters"
            : $"Parameter {Math.Min(signature.CurrentParameter + 1, signature.Parameters.Count)} of {signature.Parameters.Count}";

        private TextBlock CreateHeader()
        {
            var header = new TextBlock();
            var inlines = header.Inlines!;
            inlines.Add(new Run($"{(signature.Kind == AgentLabCompletionKind.Action ? "action" : "condition")} {signature.Name}("));
            for (var i = 0; i < signature.Parameters.Count; i++)
            {
                if (i > 0)
                {
                    inlines.Add(new Run(", "));
                }

                inlines.Add(i == signature.CurrentParameter
                    ? new Run(signature.Parameters[i]) { FontWeight = FontWeight.Bold, Foreground = Brushes.Gold }
                    : new Run(signature.Parameters[i]));
            }

            inlines.Add(new Run(")"));
            return header;
        }
    }
}
