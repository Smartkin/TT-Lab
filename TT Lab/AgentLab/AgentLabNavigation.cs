using System;
using Avalonia.Input;
using Avalonia.Interactivity;
using AvaloniaEdit;
using TT_Lab.Assets;
using Twinsanity.AgentLab;

namespace TT_Lab.AgentLab;

/// <summary>
/// Ctrl+click or F12 on the behaviour a state names opens that behaviour
/// </summary>
public sealed class AgentLabNavigation : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Func<string, IAsset?> _resolve;
    private readonly Action<IAsset> _open;

    public AgentLabNavigation(TextEditor editor, Func<string, IAsset?> resolve, Action<IAsset> open)
    {
        _editor = editor;
        _resolve = resolve;
        _open = open;
        _editor.TextArea.TextView.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        _editor.TextArea.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public void Dispose()
    {
        _editor.TextArea.TextView.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        _editor.TextArea.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }

    /// <summary>
    /// Opens the behaviour referred to at the offset, false when there's none or it can't be found
    /// </summary>
    public bool GoToDefinition(int offset)
    {
        var reference = AgentLabCompletion.GetBehaviourReference(_editor.Document.Text, offset);
        if (reference == null)
        {
            return false;
        }

        var asset = _resolve(reference.Reference);
        if (asset == null)
        {
            return false;
        }

        _open(asset);
        return true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var textView = _editor.TextArea.TextView;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control) || !e.GetCurrentPoint(textView).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var position = textView.GetPosition(e.GetPosition(textView) + textView.ScrollOffset);
        if (position != null && GoToDefinition(_editor.Document.GetOffset(position.Value.Location)))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12 && e.KeyModifiers == KeyModifiers.None && GoToDefinition(_editor.CaretOffset))
        {
            e.Handled = true;
        }
    }
}
