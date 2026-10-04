using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TT_Lab.Util;

namespace TT_Lab.Controls;

/// <summary>
/// Asks whether a project an older TT Lab made gets migrated to this one's version to open it, closing it leaves the project as it is
/// </summary>
public partial class MigrationDialogue : Window
{
    private bool _migrate;

    public MigrationDialogue()
    {
        InitializeComponent();
    }

    public MigrationDialogue(string message) : this()
    {
        DataContext = message;
    }

    public static async Task<bool> Ask(string message)
    {
        var dialogue = new MigrationDialogue(message);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._migrate;
    }

    private void MigrateButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _migrate = true;
        Close();
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
