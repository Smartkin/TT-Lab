using Avalonia.Controls;
using Avalonia.Interactivity;
using TT_Lab.ViewModels.Editors;

namespace TT_Lab.Views.Editors;

/// <summary>
/// Right clicking the name of an editor of a chunk's view of a shared asset offers what can be done with the chunk's own value
/// </summary>
/// <remarks>
/// Made when asked for, documents have thousands of editors and only the ones of chunks' views have it
/// </remarks>
internal static class OverrideMenu
{
    public static void Show(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Control { DataContext: DocumentNodeViewModel { CanOverride: true } node } caption)
        {
            return;
        }

        var revert = new MenuItem { Header = "Put back the shared value", IsEnabled = node.IsOverridden };
        revert.Click += (_, _) => node.RevertOverride();
        var apply = new MenuItem { Header = "Make this the shared value for every chunk", IsEnabled = node.IsOverridden };
        apply.Click += (_, _) => node.ApplyOverrideToAsset();
        var menu = new ContextMenu { ItemsSource = new object[] { revert, apply } };
        menu.Open(caption);
        e.Handled = true;
    }
}
