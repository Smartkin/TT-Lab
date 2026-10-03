using Avalonia.Controls;
using Avalonia.VisualTree;

namespace TT_Lab.Controls;

/// <summary>
/// Popups are drawn in the window (OverlayPopups), so a combo box's dropdown is in the window's visual tree: its items asking to be
/// brought into view (the selected one gets the focus as it opens) scrolled every scroll viewer around the combo box towards them, the
/// inspector a bit further up at every click on one. The dropdown scrolls itself before a request gets out of it, the ones that do stop
/// at their combo box
/// </summary>
public static class ComboBoxDropDowns
{
    private static bool _isRegistered;

    public static void KeepScrollRequestsInside()
    {
        if (_isRegistered)
        {
            return;
        }

        _isRegistered = true;
        Control.RequestBringIntoViewEvent.AddClassHandler<ComboBox>((combo, e) =>
        {
            if (e.TargetObject is { } target && target != combo && !combo.IsVisualAncestorOf(target))
            {
                e.Handled = true;
            }
        });
    }
}
