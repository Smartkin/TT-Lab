using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace TT_Lab.Controls;

/// <summary>
/// Popups are drawn in their window (OverlayPopups) and the Simple theme's tooltips neither wrap nor limit their width, so a hint longer
/// than its window went on past the window's edge where it couldn't be read (the PCSX2 arguments' in the Preferences). Tooltips wrap their
/// text within a reading width and the width of the window they're in
/// </summary>
public static class ToolTips
{
    public const double ReadingWidth = 480;
    // Some of the window on either side, the popup gets placed within the window
    public const double WindowMargin = 16;
    private const double LeastWidth = 120;

    public static readonly IValueConverter WidthIn = new FuncValueConverter<double, double>(windowWidth =>
        double.IsFinite(windowWidth) && windowWidth > 0 ? Math.Max(LeastWidth, Math.Min(ReadingWidth, windowWidth - WindowMargin * 2)) : ReadingWidth);

    // The window is found up the logical tree: a tooltip's popup is the hovered control's, and in the visual tree a popup of its own would be
    // a top level as wide as the tooltip
    public static Styles FitInWindows() =>
    [
        new Style(selector => selector.OfType<ToolTip>())
        {
            Setters =
            {
                new Setter(Layoutable.MaxWidthProperty, new Binding($"{nameof(Visual.Bounds)}.{nameof(Rect.Width)}")
                {
                    RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(TopLevel), Tree = TreeType.Logical },
                    Converter = WidthIn
                })
            }
        },
        new Style(selector => selector.OfType<ToolTip>().Descendant().OfType<TextBlock>())
        {
            Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) }
        }
    ];
}
