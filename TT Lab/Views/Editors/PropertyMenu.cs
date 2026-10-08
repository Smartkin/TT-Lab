using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

/// <summary>
/// Right clicking a property's name copies its values and everything under them or pastes copied ones (grayed out with the reason when
/// they can't go there), and in a chunk's view of a shared asset offers what can be done with the chunk's own value
/// </summary>
/// <remarks>
/// Made when asked for, documents have thousands of editors
/// </remarks>
internal static class PropertyMenu
{
    public static void Show(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: DocumentNodeViewModel node } caption)
        {
            return;
        }

        e.Handled = true;
        Open(caption, node);
    }

    // The clipboard is read first, whether the values can be pasted shows when the menu opens
    private static async void Open(Control caption, DocumentNodeViewModel node)
    {
        var whyNot = await node.WhyValuesCantBePastedAsync();
        new ContextMenu { ItemsSource = Items(node, whyNot) }.Open(caption);
    }

    /// <param name="whyNot">Why the clipboard's values can't be pasted into the node, none when they can</param>
    internal static List<object> Items(DocumentNodeViewModel node, string? whyNot)
    {
        var copy = new MenuItem { Header = "Copy values" };
        copy.Click += async (_, _) => await node.CopyValuesAsync();
        var paste = new MenuItem { Header = whyNot == null ? "Paste values" : Unavailable("Paste values", whyNot), IsEnabled = whyNot == null };
        paste.Click += async (_, _) => await node.PasteValuesAsync();
        var items = new List<object> { copy, paste };
        if (node.CanOverride)
        {
            var revert = new MenuItem { Header = "Put back the shared value", IsEnabled = node.IsOverridden };
            revert.Click += (_, _) => node.RevertOverride();
            var apply = new MenuItem { Header = "Make this the shared value for every chunk", IsEnabled = node.IsOverridden };
            apply.Click += (_, _) => node.ApplyOverrideToAsset();
            items.Add(new Separator());
            items.Add(revert);
            items.Add(apply);
        }

        return items;
    }

    private static StackPanel Unavailable(string header, string reason)
    {
        return new StackPanel
        {
            Orientation = Orientation.Vertical,
            Children =
            {
                new TextBlock { Text = header },
                new TextBlock { Text = reason, FontStyle = FontStyle.Italic, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 },
            },
        };
    }
}
