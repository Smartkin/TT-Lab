using Avalonia.Controls;
using TT_Lab.Assets;
using TT_Lab.Tools;
using TT_Lab.Util;

namespace TT_Lab.ViewModels.ResourceTree;

/// <summary>
/// A disc file's row: double clicking opens it with its twinstudio tool
/// </summary>
public class DiscFileElementViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null) : ResourceTreeElementViewModel(asset, parent)
{
    private DiscFile File => GetAsset<DiscFile>();

    public override void CreateEditor()
    {
        OpenWithTool();
    }

    protected override void CreateContextMenu()
    {
        RegisterMenuItem(new MenuItemSettings { Header = $"Open with {ExternalTools.ToolName(File.Tool)}", Action = OpenWithTool });
        RegisterMenuItem(new MenuItemSettings { Header = "Copy path", Action = () => CopyPath(File.MainPath) });
        RegisterMenuItem(new MenuItemSettings { Header = "Show in folder", Action = () => ExternalTools.ShowInFileManager(File.MainPath) });
    }

    private void OpenWithTool()
    {
        CopyPath(File.MainPath);
        ExternalTools.Launch(File.Tool, File.MainPath);
    }

    // The tools have no file on their command line yet, the path is pasted into their file picker
    private static async void CopyPath(string path)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(MiscUtils.GetMainWindow())?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(path);
            }
        }
        catch (System.Exception ex)
        {
            Log.WriteLine($"Couldn't put the path on the clipboard: {ex.Message}", Log.LogType.Debug);
        }
    }
}
