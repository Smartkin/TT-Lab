using Avalonia.Headless.XUnit;
using GlmSharp;
using Newtonsoft.Json;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common;
using static TT_Lab.Tests.Support.TestGeometry;

namespace TT_Lab.Tests.Assets;

// A chunk link's loading hulls are kept as a placement the gizmo moves and the corners around it, the game's bytes stay while nothing moved
[Collection(ProjectCollection.Name)]
public sealed class ChunkLinkHullTests : IDisposable
{
    private static readonly Vector4 Min = new(-4, 0, -6, 1);
    private static readonly Vector4 Max = new(2, 3, 4, 1);

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void BoxesArePlacedByTheirCenterAndHalfExtents()
    {
        var box = TwinCollisionHull.CreateBox(Min, Max);

        var hull = new ChunkLinkHull(box);

        var placement = hull.Placement.ToGlm();
        Assert.Equal(new vec3(-1, 1.5f, -1), placement.Column3.xyz);
        Assert.Equal(new vec3(3, 1.5f, 5), new vec3(placement.m00, placement.m11, placement.m22));
        Assert.All(hull.Vertexes, vertex => Assert.All(new[] { vertex.X, vertex.Y, vertex.Z }, value => Assert.Equal(1.0f, Math.Abs(value))));
        Assert.Equal(box.Vertexes.Select(Serialize), hull.GetCorners().Select(Serialize));
        Assert.Equal(Serialize(box), Serialize(hull.ToTwin().Hull));
    }

    [Fact]
    public void OtherShapesArePlacedAtTheirCentroid()
    {
        var pyramid = new TwinCollisionHull
        {
            Vertexes = [new Vector4(0, 2, 0, 1), new Vector4(-1, 0, -1, 1), new Vector4(1, 0, -1, 1), new Vector4(1, 0, 1, 1), new Vector4(-1, 0, 1, 1)],
            Faces = [[1, 2, 3, 4], [0, 2, 1], [0, 3, 2], [0, 4, 3], [0, 1, 4]]
        };
        pyramid.ComputeFromFaces();

        var hull = new ChunkLinkHull(pyramid);

        Assert.Equal(new vec3(0, 0.4f, 0), hull.Placement.ToGlm().Column3.xyz);
        Assert.Equal(1.6f, hull.Vertexes[0].Y, 5);
        Assert.Equal(Serialize(pyramid), Serialize(hull.ToTwin().Hull));
    }

    // Moving a hull, or a corner of it, leaves the game's planes behind
    [Fact]
    public void MovedHullsGetTheirPlanesWorkedOut()
    {
        var box = TwinCollisionHull.CreateBox(Min, Max);
        var hull = new ChunkLinkHull(box);
        hull.Placement = (mat4.Translate(0, 5, 0) * hull.Placement.ToGlm()).ToTwin();

        var moved = hull.ToTwin().Hull;

        Assert.Equal(5.0f, moved.Vertexes.Min(vertex => vertex.Y));
        Assert.True(moved.DescribesFaces());
        Assert.Equal(box.Faces, moved.Faces);
        // The bottom face's plane: y is 5 where -y + w is 0
        Assert.Equal(5.0f, moved.Planes[0].W, 4);
        Assert.NotEqual(Serialize(box), Serialize(moved));

        var stretched = new ChunkLinkHull(box);
        stretched.Vertexes[6] = new Vector4(1, 1, 3, 1);
        Assert.Equal(14.0f, stretched.ToTwin().Hull.Vertexes[6].Z, 4);
    }

    [Fact]
    public void NewHullsAreBoxes()
    {
        var hull = new ChunkLinkHull();

        var twin = hull.ToTwin().Hull;

        Assert.Equal(8, twin.Vertexes.Count);
        Assert.Equal(6, twin.Planes.Count);
        Assert.True(twin.DescribesFaces());
    }

    // The inspector shows the placement and the corners, the faces and the game's hull stay behind them
    [AvaloniaFact]
    public void HullsAreEditedByTheirPlacementAndCorners()
    {
        var links = _project.Add(new ChunkLinks { Chunk = "default" }, "Links");
        var link = new ChunkLink();
        link.Hulls.Add(new ChunkLinkHull(TwinCollisionHull.CreateBox(Min, Max)));
        links.SetData(new ChunkLinksData(links) { Links = [link] });
        var document = new DocumentViewModel(links);
        document.Initialize();

        var hull = document.PropertyGraph.Find("Root.AssetData.Links[0].Hulls[0]")!;
        Assert.NotNull(hull.Find(nameof(ChunkLinkHull.Placement)));
        Assert.NotNull(hull.Find($"{nameof(ChunkLinkHull.Vertexes)}[0]"));
        Assert.Null(hull.Find(nameof(ChunkLinkHull.Faces)));
        Assert.Null(hull.Find(nameof(ChunkLinkHull.Source)));

        var placement = hull.Find(nameof(ChunkLinkHull.Placement))!;
        placement.SetValue((mat4.Translate(0, 5, 0) * placement.GetValue<Matrix4>()!.ToGlm()).ToTwin());

        Assert.Equal(5.0f, link.Hulls[0].ToTwin().Hull.Vertexes.Min(vertex => vertex.Y), 4);
    }
}
