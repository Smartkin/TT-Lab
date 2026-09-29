using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.Views.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Editor;

// Scrolling a long list used to make the views of every row scrolled in again (about 80 controls for a point), the rows scrolled out
// are given the next items instead
[Collection(ProjectCollection.Name)]
public sealed class ListRecyclingTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static HashSet<Control> RecyclableViews(Window window)
    {
        return new HashSet<Control>(window.GetVisualDescendants().OfType<Control>().Where(control => control is IRecyclableView), ReferenceEqualityComparer.Instance);
    }

    [AvaloniaFact]
    public void ScrollingALongListReusesItsRowsViews()
    {
        var path = _project.Add(new Path(), "Path");
        path.SetData(new PathData(path) { Points = Enumerable.Range(0, 3000).Select(i => new Vector3(i, 0, 0)).ToList(), Parameters = [] });
        var document = new DocumentViewModel(path);
        document.Initialize();
        var window = new Window { Content = new DocumentView { DataContext = document }, Width = 800, Height = 700 };
        window.Show();
        Pump();
        var list = (DocumentCompositeViewModel)document.Root.Nodes.Single(node => node.Property.Name == "Points");
        list.IsExpanded = true;
        Pump();
        var viewer = window.GetVisualDescendants().OfType<ScrollViewer>().MaxBy(v => v.Extent.Height)!;
        var seen = RecyclableViews(window);
        var shownAtOnce = seen.Count;

        // The first steps fill the panel's pool of rows, after that nothing new gets made
        for (var step = 0; step < 40; step++)
        {
            viewer.Offset = new Vector(0, viewer.Offset.Y + 100.0);
            Pump();
            seen.UnionWith(RecyclableViews(window));
        }

        Assert.True(viewer.Offset.Y >= 3900.0);
        Assert.InRange(seen.Count, shownAtOnce, shownAtOnce * 2);
        // The rows show the points scrolled to, with the recycled views' view models swapped
        var shown = window.GetVisualDescendants().OfType<Vector3FieldView>().Select(view => ((Vector3FieldViewModel)view.DataContext!).Property.Index!.Value).ToList();
        Assert.NotEmpty(shown);
        Assert.True(shown.Min() > 30, $"still showing point {shown.Min()}");

        // What a recycled row shows follows its new point
        var first = window.GetVisualDescendants().OfType<Vector3FieldView>().First();
        var point = ((Vector3FieldViewModel)first.DataContext!).Property;
        point.Find("X")!.SetValue(12345.0f);
        Pump();
        Assert.Contains(first.GetVisualDescendants().OfType<TextBox>(), box => box.Text == "12345");
    }
}
