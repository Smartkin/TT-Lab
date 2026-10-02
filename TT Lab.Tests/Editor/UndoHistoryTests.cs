using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Instance;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Editor;

// Every editor keeps a tree of what its document changed, changing something after undoing starts a branch next to the undone one
[Collection(ProjectCollection.Name)]
public sealed class UndoHistoryTests : IDisposable
{
    private const string FirstX = "Root.AssetData.Points[0].X";

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private (DocumentViewModel Document, PathData Data) OpenPath(string name = "Path")
    {
        var path = _project.Add(new Path(), name);
        var data = new PathData(path) { Points = Enumerable.Range(0, 3).Select(i => new Vector3(i, 0, 0)).ToList(), ArcLengths = [], InverseSteps = [] };
        path.SetData(data);
        var document = new DocumentViewModel(path);
        document.Initialize();
        return (document, data);
    }

    // A step of its own, changes of a value right after each other are one step otherwise
    private static void Change(DocumentViewModel document, string path, object value)
    {
        using (document.History.BeginGroup())
        {
            document.PropertyGraph.Find(path)!.SetValue(value);
        }
    }

    [AvaloniaFact]
    public void ValuesGoBackAndForth()
    {
        var (document, data) = OpenPath();
        Change(document, FirstX, 5.0f);
        Change(document, FirstX, 7.0f);

        document.Undo();
        Assert.Equal(5.0f, data.Points[0].X);
        document.Undo();
        Assert.Equal(0.0f, data.Points[0].X);
        Assert.False(document.CanUndo);

        document.Redo();
        document.Redo();
        Assert.Equal(7.0f, data.Points[0].X);
        Assert.False(document.CanRedo);
    }

    // Typing or dragging changes a value many times in a row
    [AvaloniaFact]
    public void ChangesOfAValueInARowAreOneStep()
    {
        var (document, data) = OpenPath();
        var x = document.PropertyGraph.Find(FirstX)!;
        foreach (var value in new[] { 1.0f, 2.0f, 3.0f })
        {
            x.SetValue(value);
        }

        document.Undo();

        Assert.Equal(0.0f, data.Points[0].X);
        Assert.False(document.CanUndo);
    }

    [AvaloniaFact]
    public void ElementsAddedAndRemovedComeBack()
    {
        var (document, data) = OpenPath();
        var points = document.PropertyGraph.Find("Root.AssetData.Points")!;
        var second = data.Points[1];

        points.RemoveElement(points.Children[1]);
        Assert.Equal([0.0f, 2.0f], data.Points.Select(point => point.X));
        document.Undo();
        Assert.Equal([0.0f, 1.0f, 2.0f], data.Points.Select(point => point.X));
        Assert.Same(second, data.Points[1]);
        Assert.Equal(1.0f, document.PropertyGraph.Find("Root.AssetData.Points[1].X")!.GetValue());

        points.InsertElement(1, new Vector3(9, 0, 0));
        document.Undo();
        Assert.Equal(3, data.Points.Count);
        document.Redo();
        Assert.Equal([0.0f, 9.0f, 1.0f, 2.0f], data.Points.Select(point => point.X));
    }

    [AvaloniaFact]
    public void ChangingAfterUndoingKeepsTheUndoneBranch()
    {
        var (document, data) = OpenPath();
        Change(document, FirstX, 5.0f);
        var undone = document.History.Current;
        document.Undo();
        Change(document, FirstX, 8.0f);

        Assert.Equal(2, document.History.Root.Children.Count);
        Assert.NotEqual(undone.Branch, document.History.Current.Branch);

        document.History.GoTo(undone);
        Assert.Equal(5.0f, data.Points[0].X);
        document.History.GoTo(document.History.Root);
        Assert.Equal(0.0f, data.Points[0].X);
        // Redoing goes along the branch last taken
        document.Redo();
        Assert.Equal(5.0f, data.Points[0].X);
    }

    [AvaloniaFact]
    public void UndoingToWhereItWasSavedIsntUnsaved()
    {
        var (document, _) = OpenPath();
        Change(document, FirstX, 5.0f);
        document.Save();
        Change(document, FirstX, 6.0f);
        Assert.True(document.IsDirty);

        document.Undo();
        Assert.False(document.IsDirty);
        document.Undo();
        Assert.True(document.IsDirty);
        document.Redo();
        Assert.False(document.IsDirty);
    }

    [AvaloniaFact]
    public void GroupsAreOneStep()
    {
        var (document, data) = OpenPath();
        using (document.History.BeginGroup("Moved the path"))
        {
            document.PropertyGraph.Find(FirstX)!.SetValue(4.0f);
            document.PropertyGraph.Find("Root.AssetData.Points[1].X")!.SetValue(5.0f);
        }

        Assert.Equal("Moved the path", document.History.Current.Description);
        document.Undo();

        Assert.Equal([0.0f, 1.0f], data.Points.Take(2).Select(point => point.X));
        Assert.False(document.CanUndo);
    }

    // Like a drag given up on, which puts back what it started from
    [AvaloniaFact]
    public void GroupsThatChangedNothingArentSteps()
    {
        var (document, _) = OpenPath();
        using (document.History.BeginGroup())
        {
            document.PropertyGraph.Find(FirstX)!.SetValue(4.0f);
            document.PropertyGraph.Find(FirstX)!.SetValue(0.0f);
        }

        Assert.False(document.CanUndo);
    }

    [AvaloniaFact]
    public void EveryEditorKeepsItsOwnHistory()
    {
        var (first, firstData) = OpenPath("First");
        var (second, secondData) = OpenPath("Second");
        Change(first, FirstX, 5.0f);
        Change(second, FirstX, 6.0f);

        first.Undo();

        Assert.Equal(0.0f, firstData.Points[0].X);
        Assert.Equal(6.0f, secondData.Points[0].X);
        Assert.True(second.CanUndo);
    }

    // Describing the steps for the history's panel
    [AvaloniaFact]
    public void StepsSayWhatChanged()
    {
        var (document, _) = OpenPath();
        Change(document, FirstX, 5.0f);

        Assert.Equal("Path › Points[0] › X = 5", document.History.Current.Description);
    }
}
