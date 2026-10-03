using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TT_Lab.Util;

namespace TT_Lab.Controls;

/// <summary>
/// Asks whether editors with unsaved changes get made again from files another program changed, closing it keeps them as they are
/// </summary>
public partial class ExternalChangeDialogue : Window
{
    private bool _reload;

    public ExternalChangeDialogue()
    {
        InitializeComponent();
    }

    public ExternalChangeDialogue(string message) : this()
    {
        DataContext = message;
    }

    public static async Task<bool> Ask(string message)
    {
        var dialogue = new ExternalChangeDialogue(message);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._reload;
    }

    private void ReloadButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _reload = true;
        Close();
    }

    private void KeepButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
