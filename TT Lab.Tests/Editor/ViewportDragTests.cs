using Avalonia.Headless.XUnit;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects.Gizmo;
using TT_Lab.Tests.Rendering;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.Tests.Editor;

// A gizmo drag is one step of the document's history whatever ends it. A model Blender wrote while an instance of it was being dragged
// made the instance again, which deselected it: the drag stopped, its step stayed open, and every later change went into it unseen
[Collection(ProjectCollection.Name)]
public sealed class ViewportDragTests : IDisposable
{
    private const string FirstX = "Root.AssetData.Points[0].X";
    private static readonly GizmoTransform Origin = new(vec3.Zero, quat.Identity, vec3.Ones);

    private readonly TestProject _project = new();
    private readonly FrameCamera _camera = ViewportMathTests.CreateCamera(new vec3(6, 5, 8), vec3.Zero);

    public void Dispose() => _project.Dispose();

    private (DocumentViewModel Document, PathData Data) OpenPath()
    {
        var path = _project.Add(new Path(), "Path");
        var data = new PathData(path) { Points = Enumerable.Range(0, 3).Select(i => new Vector3(i, 0, 0)).ToList(), ArcLengths = [], InverseSteps = [] };
        path.SetData(data);
        var document = new DocumentViewModel(path);
        document.Initialize();
        return (document, data);
    }

    // Grabs the gizmo's arrow along X, the way a click on it in the viewport does
    private TransformGizmo StartGizmoDrag(ViewportViewModel.SelectionDrag drag)
    {
        var gizmo = new TransformGizmo { Mode = TransformMode.TRANSLATE, Locality = TransformLocality.WORLD };
        drag.Follow(gizmo);
        var grab = vec3.UnitX * _camera.WorldUnitsPerPixel(vec3.Zero) * TransformGizmo.SizeInPixels * 0.7f;
        Assert.True(_camera.WorldToScreen(grab, out var screen));
        Assert.True(gizmo.BeginDrag(_camera, Origin, screen));
        return gizmo;
    }

    [AvaloniaFact]
    public void ADragsStepEndsWhenSomethingElseEndsTheDrag()
    {
        var (document, data) = OpenPath();
        var drag = new ViewportViewModel.SelectionDrag();
        var gizmo = StartGizmoDrag(drag);
        var dragged = document.PropertyGraph.Find("Root.AssetData.Points[0]")!;
        drag.Begin(document.History, "Moved the path", [dragged]);
        document.PropertyGraph.Find(FirstX)!.SetValue(2.0f);
        document.PropertyGraph.Find(FirstX)!.SetValue(3.0f);
        Assert.True(document.History.IsGrouping);

        // What deselecting the dragged object does
        gizmo.EndDrag();

        Assert.False(drag.IsDragging);
        Assert.False(document.History.IsGrouping);
        Assert.Equal("Moved the path", document.History.Current.Description);
        // What's changed after is a step of its own, undone before the drag's
        document.PropertyGraph.Find("Root.AssetData.Points[1].X")!.SetValue(9.0f);
        document.Undo();
        Assert.Equal(1.0f, data.Points[1].X);
        Assert.Equal(3.0f, data.Points[0].X);
        document.Undo();
        Assert.Equal(0.0f, data.Points[0].X);
        Assert.False(document.CanUndo);
    }

    // The objects of what's dragged are made again once the drag is over, once whatever asked how many times
    [AvaloniaFact]
    public void RebuildsOfWhatsDraggedWaitForTheDrag()
    {
        var (document, _) = OpenPath();
        var drag = new ViewportViewModel.SelectionDrag();
        var waited = new List<IReadOnlyList<PropertyNode>>();
        drag.Ended += waiting => waited.Add(waiting);
        var dragged = document.PropertyGraph.Find("Root.AssetData.Points[0]")!;
        var other = document.PropertyGraph.Find("Root.AssetData.Points[1]")!;
        Assert.False(drag.Defer(dragged));

        var gizmo = StartGizmoDrag(drag);
        drag.Begin(document.History, "Moved the path", [dragged, dragged]);
        Assert.True(drag.Defer(dragged));
        Assert.True(drag.Defer(dragged));
        Assert.False(drag.Defer(other));
        Assert.Empty(waited);

        gizmo.EndDrag();

        Assert.Equal([dragged], Assert.Single(waited));
        Assert.False(drag.Defer(dragged));
        // The viewport's mouse up after it changes nothing
        drag.End();
        Assert.Single(waited);
    }

    // A drag left going (its gizmo's never told) ends before the next one starts
    [AvaloniaFact]
    public void ANewDragEndsTheOneLeftGoing()
    {
        var (document, data) = OpenPath();
        var drag = new ViewportViewModel.SelectionDrag();
        var dragged = document.PropertyGraph.Find("Root.AssetData.Points[0]")!;
        drag.Begin(document.History, "Moved the path", [dragged]);
        document.PropertyGraph.Find(FirstX)!.SetValue(4.0f);

        drag.Begin(document.History, "Moved the path again", [dragged]);
        Assert.Equal("Moved the path", document.History.Current.Description);
        document.PropertyGraph.Find(FirstX)!.SetValue(5.0f);
        drag.End();

        Assert.False(document.History.IsGrouping);
        Assert.Equal("Moved the path again", document.History.Current.Description);
        document.Undo();
        Assert.Equal(4.0f, data.Points[0].X);
    }

    // What a scene forgets of data replaced whole: the meshes of the asset's own data right away, the meshes of its parts (its old data's
    // and its new data's, whose URIs are of their content) once nothing draws them
    [AvaloniaFact]
    public void TheMeshesOfReplacedDataAreItsPartsAndTheirParts()
    {
        var ogi = _project.Add(new OGI(), "Skeleton");
        var other = _project.Add(new OGI(), "Other");
        var part = _project.Add(new RigidModel { IsInternal = true, InternalOwner = ogi }, "Skeleton_Part");
        var partOfPart = _project.Add(new Model { IsInternal = true, InternalOwner = part }, "Skeleton_Part_Model");
        _project.Add(new RigidModel { IsInternal = true, InternalOwner = other }, "Other_Part");
        _project.Add(new RigidModel { InternalOwner = ogi }, "Not_Internal");

        Assert.Equal(new HashSet<LabURI> { part.URI, partOfPart.URI }, ViewportViewModel.PartsOf(ogi).ToHashSet());
    }
}
