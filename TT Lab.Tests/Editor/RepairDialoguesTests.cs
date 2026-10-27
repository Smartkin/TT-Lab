using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TT_Lab.Controls;
using TT_Lab.Project.Repair;
using TT_Lab.Tests.Support;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Tests.Editor;

// The repair of a project 1.1.0 unpacked asks with three answers, closing it is Not Now. Its disc folder is picked in a dialogue that only takes
// a folder of the version's files, the PS2 archive's hashes are checked once it's used
public sealed class RepairDialoguesTests : IDisposable
{
    private readonly List<FakeDisc> _discs = [];

    public void Dispose()
    {
        foreach (var disc in _discs)
        {
            disc.Dispose();
        }
    }

    private FakeDisc Disc(GamePlatform platform)
    {
        var disc = new FakeDisc(platform);
        _discs.Add(disc);
        return disc;
    }

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheQuestionHasThreeAnswersAndClosingItIsNotNow()
    {
        foreach (var (button, answer) in new[] { ("RepairButton", RepairAnswer.Repair), ("NotNowButton", RepairAnswer.NotNow), ("DontAskAgainButton", RepairAnswer.DontAskAgain) })
        {
            var dialogue = new RepairDialogue("Repair Test's PS2 game packages?");
            dialogue.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(dialogue.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "Repair Test's PS2 game packages?");

            Click(dialogue.FindControl<Button>(button)!);

            Assert.Equal(answer, dialogue.Answer);
            Assert.False(dialogue.IsVisible);
        }

        var closed = new RepairDialogue("Repair?");
        closed.Show();
        closed.Close();
        Assert.Equal(RepairAnswer.NotNow, closed.Answer);
    }

    [AvaloniaFact]
    public void TheDiscIsAFolderOfTheVersionsFiles()
    {
        var ps2 = Disc(GamePlatform.PS2);
        var xbox = Disc(GamePlatform.Xbox);
        var dialogue = new RepairDiscDialogue(new RepairDiscDialogue.Request("Test", GamePlatform.PS2, null, "The folder before wasn't taken."));
        dialogue.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Test's PS2 game packages", dialogue.Message.Text);
        Assert.Equal("The folder before wasn't taken.", dialogue.Problem.Text);
        Assert.Equal("Skip the PS2 Version", dialogue.SkipButton.Content);
        Assert.False(dialogue.UseButton.IsEnabled);
        dialogue.Folder.Text = xbox.Folder;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("no Crash6/Crash.BH", dialogue.Status.Text);
        Assert.False(dialogue.UseButton.IsEnabled);
        dialogue.Folder.Text = ps2.Folder;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("has the PS2 disc's archive", dialogue.Status.Text);
        Assert.True(dialogue.UseButton.IsEnabled);

        Click(dialogue.UseButton);

        Assert.Equal(ps2.Folder, dialogue.Answer);
        Assert.False(dialogue.IsVisible);

        // The Xbox disc's folder is its default.xbe and its default chunk, skipping answers nothing
        var skipped = new RepairDiscDialogue(new RepairDiscDialogue.Request("Test", GamePlatform.Xbox, xbox.Folder, null));
        skipped.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("Startup/Default.rmx", skipped.Status.Text);
        Assert.False(skipped.UseButton.IsEnabled);
        xbox.AddText("Startup\\Default.rmx", "");
        skipped.Folder.Text = string.Empty;
        skipped.Folder.Text = xbox.Folder;
        Dispatcher.UIThread.RunJobs();
        Assert.True(skipped.UseButton.IsEnabled);

        Click(skipped.SkipButton);

        Assert.Null(skipped.Answer);
        Assert.False(skipped.IsVisible);
    }
}
