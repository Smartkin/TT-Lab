using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using TT_Lab.Controls;

namespace TT_Lab.Tests.Editor;

public sealed class FitToScrollViewportTests
{
    private const string Text = "No animations present. Create new ones in Blender, they show up here once the file gets loaded again";

    private static (ScrollViewer Viewer, TextBlock Text) Show(bool fit)
    {
        var text = new TextBlock { Text = Text, TextWrapping = TextWrapping.Wrap };
        var viewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { fit ? new FitToScrollViewport { Child = text } : text } }
        };
        var window = new Window { Content = viewer, Width = 200, Height = 300 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        return (viewer, text);
    }

    [AvaloniaFact]
    public void TextInViewersThatScrollSidewaysWraps()
    {
        var (viewer, text) = Show(true);

        Assert.True(text.Bounds.Width <= viewer.Viewport.Width, $"The text is {text.Bounds.Width} wide in a {viewer.Viewport.Width} wide view");
        Assert.True(viewer.Extent.Width <= viewer.Viewport.Width + 0.5);
        Assert.True(text.Bounds.Height > text.FontSize * 2, "The text didn't wrap");
    }

    // What it works around
    [AvaloniaFact]
    public void TextInViewersThatScrollSidewaysDoesntWrapOnItsOwn()
    {
        var (viewer, _) = Show(false);

        Assert.True(viewer.Extent.Width > viewer.Viewport.Width);
    }
}
