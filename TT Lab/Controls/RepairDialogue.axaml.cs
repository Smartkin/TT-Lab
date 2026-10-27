using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TT_Lab.Project.Repair;
using TT_Lab.Util;

namespace TT_Lab.Controls;

/// <summary>
/// Asks whether the game's packages of a project an older TT Lab unpacked get repaired from the game's discs, closing it is Not Now
/// </summary>
public partial class RepairDialogue : Window
{
    private RepairAnswer _answer = RepairAnswer.NotNow;

    internal RepairAnswer Answer => _answer;

    public RepairDialogue()
    {
        InitializeComponent();
    }

    public RepairDialogue(string message) : this()
    {
        DataContext = message;
    }

    public static async Task<RepairAnswer> Ask(string message)
    {
        var dialogue = new RepairDialogue(message);
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
        return dialogue._answer;
    }

    private void Choose(RepairAnswer answer)
    {
        _answer = answer;
        Close();
    }

    private void RepairButton_OnClick(object? sender, RoutedEventArgs e) => Choose(RepairAnswer.Repair);

    private void NotNowButton_OnClick(object? sender, RoutedEventArgs e) => Choose(RepairAnswer.NotNow);

    private void DontAskAgainButton_OnClick(object? sender, RoutedEventArgs e) => Choose(RepairAnswer.DontAskAgain);
}
