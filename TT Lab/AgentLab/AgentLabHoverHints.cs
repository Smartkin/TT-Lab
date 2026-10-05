using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using Twinsanity.AgentLab;

namespace TT_Lab.AgentLab;

/// <summary>
/// Tooltip with what the word under the pointer is: a keyword, an attribute, an action or condition with its parameters, a
/// field of a packed argument, an enum value or something the script declares
/// </summary>
public sealed class AgentLabHoverHints : IDisposable
{
    private readonly TextEditor _editor;
    private readonly string _actionDefinitionsFile;
    private readonly Func<string, string>? _describeBehaviour;
    private string? _shownWord;

    public AgentLabHoverHints(TextEditor editor, string actionDefinitionsFile, Func<string, string>? describeBehaviour = null)
    {
        _editor = editor;
        _actionDefinitionsFile = actionDefinitionsFile;
        _describeBehaviour = describeBehaviour;
        ToolTip.SetPlacement(_editor.TextArea, PlacementMode.Pointer);
        ToolTip.SetShowDelay(_editor.TextArea, 0);
        _editor.TextArea.TextView.PointerHover += OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped += OnPointerHoverStopped;
        _editor.TextArea.TextEntered += OnTextEntered;
    }

    public void Dispose()
    {
        _editor.TextArea.TextView.PointerHover -= OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped -= OnPointerHoverStopped;
        _editor.TextArea.TextEntered -= OnTextEntered;
        Hide();
    }

    /// <summary>
    /// The hint for the word at an offset of the editor's text, null when there's none
    /// </summary>
    public AgentLabHover? GetHover(int offset)
    {
        var text = _editor.Document.Text;
        var hover = AgentLabCompletion.GetHover(text, offset, _actionDefinitionsFile);
        if (hover != null && AgentLabCompletion.GetDeclaration(text, offset) != null)
        {
            return new AgentLabHover(hover.Title, $"{hover.Description}, Ctrl+click or F12 goes to it");
        }

        if (hover is not { IsBehaviourReference: true } || _describeBehaviour == null || AgentLabCompletion.GetBehaviourReference(text, offset) is not { } reference)
        {
            return hover;
        }

        // The editor knows the project, so the hint says where the behaviour is or that there's none
        return new AgentLabHover(hover.Title, _describeBehaviour(reference.Reference), true);
    }

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        var textView = _editor.TextArea.TextView;
        var position = textView.GetPosition(e.GetPosition(textView) + textView.ScrollOffset);
        if (position == null)
        {
            Hide();
            return;
        }

        var offset = _editor.Document.GetOffset(position.Value.Location);
        var hover = GetHover(offset);
        if (hover == null)
        {
            Hide();
            return;
        }

        if (_shownWord == hover.Title && ToolTip.GetIsOpen(_editor.TextArea))
        {
            return;
        }

        _shownWord = hover.Title;
        ToolTip.SetTip(_editor.TextArea, CreateContent(hover));
        ToolTip.SetIsOpen(_editor.TextArea, true);
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e) => Hide();

    private void OnTextEntered(object? sender, TextInputEventArgs e) => Hide();

    private void Hide()
    {
        if (_shownWord == null)
        {
            return;
        }

        _shownWord = null;
        ToolTip.SetIsOpen(_editor.TextArea, false);
        ToolTip.SetTip(_editor.TextArea, null);
    }

    private static Control CreateContent(AgentLabHover hover)
    {
        var text = new TextBlock { MaxWidth = 640, TextWrapping = TextWrapping.Wrap };
        text.Inlines!.Add(new Run(hover.Title) { FontWeight = FontWeight.Bold });
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(new Run(hover.Description));
        return text;
    }
}
