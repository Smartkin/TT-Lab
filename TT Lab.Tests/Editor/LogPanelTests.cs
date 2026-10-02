using Avalonia.Headless.XUnit;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;

namespace TT_Lab.Tests.Editor;

// Lines reach the log panel in batches below input and rendering: a dispatcher job and an edit of the panel's text per line ran ahead of
// both, and a few thousand lines a second from a build froze the UI for tens of seconds
[Collection(ProjectCollection.Name)]
public sealed class LogPanelTests : IDisposable
{
    private readonly LogViewModel _panel = new(new TestProject.NullEventAggregator(), new ProjectManager(new TestProject.NullEventAggregator()));

    public LogPanelTests()
    {
        Log.SetViewModel(_panel);
    }

    public void Dispose() => Log.SetViewModel(null);

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    [AvaloniaFact]
    public async Task SpamReachesThePanelInAFewEdits()
    {
        var edits = 0;
        _panel.Text.TextChanged += (_, _) => edits++;

        Parallel.For(0, 4, thread =>
        {
            for (var line = 0; line < 2500; line++)
            {
                Log.WriteLine($"thread {thread} line {line}");
            }
        });
        Log.WriteLine("the last line");
        await WaitUntil(() => _panel.Text.Text.Contains("the last line"));

        Assert.InRange(edits, 1, 10);
        Assert.InRange(_panel.Text.LineCount, 2, 501);
        Assert.EndsWith("the last line" + Environment.NewLine, _panel.Text.Text);
    }

    [AvaloniaFact]
    public async Task ClearingDropsTheLinesNotShownYet()
    {
        Log.WriteLine("before clearing");
        Log.Clear();
        Log.WriteLine("after clearing");
        await WaitUntil(() => _panel.Text.Text.Contains("after clearing"));

        Assert.DoesNotContain("before clearing", _panel.Text.Text);
    }
}
