using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TT_Lab.Controls;

/// <summary>
/// Keeps the keyboard focus in a view whose shortcuts need it (an editor's tab, the chunk's panels). Avalonia drops the focus altogether
/// when the element that had it leaves the visual tree, the X of a list's element taking its row away with it, and Ctrl+Z went nowhere
/// until a click inside put the focus back. Once a click or a key inside the view (and what it ran) leaves nothing focused, the view
/// takes the focus: the routes of those were made before anything got taken out, so they still reach it
/// </summary>
public static class FocusKeeper
{
    public static void KeepFocusIn(InputElement view)
    {
        void Check(object? sender, RoutedEventArgs e) => Dispatcher.UIThread.Post(() => TakeLostFocus(view), DispatcherPriority.Input);

        view.AddHandler(InputElement.PointerReleasedEvent, Check, RoutingStrategies.Bubble, handledEventsToo: true);
        view.AddHandler(InputElement.KeyDownEvent, Check, RoutingStrategies.Bubble, handledEventsToo: true);
        view.AddHandler(InputElement.KeyUpEvent, Check, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void TakeLostFocus(InputElement view)
    {
        if (view.IsEffectivelyVisible && TopLevel.GetTopLevel(view) is { FocusManager: { } focus } && focus.GetFocusedElement() == null)
        {
            view.Focus();
        }
    }
}
