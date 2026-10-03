using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.Models;
using TT_Lab.ViewModels;
using TT_Lab.Views;

namespace TT_Lab.Tests.Editor;

public sealed class AboutViewTests
{
    // The about dialogue lists everyone the about data has, the artists were left out
    [AvaloniaFact]
    public void TheAboutDialogueShowsTheArtists()
    {
        var window = new AboutView { DataContext = new AboutViewModel() };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
        Assert.Contains("Artists:", texts);
        Assert.Contains(new AboutModel().Artists, texts);
        window.Close();
    }
}
