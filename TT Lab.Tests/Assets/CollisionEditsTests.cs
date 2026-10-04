using System.Numerics;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.Assets;

namespace TT_Lab.Tests.Assets;

// The scenery mode's edits of a collision's triangles, each a new geometry the history keeps the old one of
public class CollisionEditsTests
{
    private static readonly LabURI Grass = new("res://Grass");
    private static readonly LabURI Rock = new("res://Rock");

    // A square of two triangles sharing an edge, and a vertex no triangle has like the tools left in some collisions
    private static CollisionGeometry Square() => new(
        [new Vector4(0, 0, 0, 1), new Vector4(1, 0, 0, 1), new Vector4(1, 0, 1, 1), new Vector4(0, 0, 1, 1), new Vector4(5, 5, 5, 1)],
        [new CollisionFace(0, 1, 2, Grass), new CollisionFace(0, 2, 3, Rock)]);

    [Fact]
    public void DeletedTrianglesTakeOnlyTheirOwnVertexes()
    {
        var square = Square();

        var deleted = CollisionEdits.Delete(square, [1]);

        Assert.Equal([new CollisionFace(0, 1, 2, Grass)], deleted.Triangles);
        // The corner only the deleted one had is gone, the tools' leftover stays
        Assert.Equal([square.Vertexes[0], square.Vertexes[1], square.Vertexes[2], square.Vertexes[4]], deleted.Vertexes);
        Assert.Equal(5, square.Vertexes.Count);
    }

    [Fact]
    public void DuplicatesGetCornersOfTheirOwn()
    {
        var (duplicated, copies) = CollisionEdits.Duplicate(Square(), [0, 1]);

        Assert.Equal([2, 3], copies);
        Assert.Equal(9, duplicated.Vertexes.Count);
        Assert.Equal(new CollisionFace(5, 6, 7, Grass), duplicated.Triangles[2]);
        Assert.Equal(new CollisionFace(5, 7, 8, Rock), duplicated.Triangles[3]);
        Assert.Equal(duplicated.Vertexes[0], duplicated.Vertexes[5]);
    }

    [Fact]
    public void SurfacesChangeOnlyForTheSelection()
    {
        var edited = CollisionEdits.SetSurface(Square(), [1], Grass);

        Assert.All(edited.Triangles, triangle => Assert.Equal(Grass, triangle.Surface));
        Assert.Equal(Rock, Square().Triangles[1].Surface);
    }

    // Moving a triangle moves its corners, the triangle sharing two of them stretches along and the collision stays closed
    [Fact]
    public void MovedTrianglesTakeTheirSharedCornersAlong()
    {
        var moved = CollisionEdits.Transform(Square(), [0], Matrix4x4.CreateTranslation(0, 2, 0));

        Assert.Equal([2.0f, 2.0f, 2.0f, 0.0f, 5.0f], moved.Vertexes.Select(vertex => vertex.Y));
        Assert.Equal(Square().Triangles, moved.Triangles);
    }

    [Fact]
    public void AddedTrianglesJoinTheCollisionsCorners()
    {
        (Vector3, Vector3, Vector3)[] wall = [(new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 1, 1))];

        var (added, indexes, result) = CollisionEdits.Add(Square(), wall, Rock);

        Assert.Equal([2], indexes);
        Assert.Single(result.Positions);
        Assert.Equal(6, added.Vertexes.Count);
        // Turned to the game's winding, its corners on the square's own
        Assert.Equal(new CollisionFace(1, 5, 2, Rock), added.Triangles[2]);
    }

    [Fact]
    public void PickingFindsTheClosestTriangleFromEitherSide()
    {
        var square = Square();

        Assert.Equal(0, CollisionEdits.Pick(square, new Vector3(0.9f, 5, 0.1f), -Vector3.UnitY, out var distance));
        Assert.Equal(5.0f, distance, 1e-5f);
        Assert.Equal(1, CollisionEdits.Pick(square, new Vector3(0.1f, -5, 0.9f), Vector3.UnitY, out _));
        Assert.Null(CollisionEdits.Pick(square, new Vector3(3, 5, 3), -Vector3.UnitY, out _));
    }

    [Fact]
    public void PointsSnapToCornersCloseBy()
    {
        var square = Square();

        Assert.Equal(2, CollisionEdits.NearestCorner(square, new Vector3(1.05f, 0, 0.98f), 0.1f));
        Assert.Null(CollisionEdits.NearestCorner(square, new Vector3(0.5f, 0, 0.5f), 0.1f));
        // Leftover vertexes aren't corners of anything
        Assert.Null(CollisionEdits.NearestCorner(square, new Vector3(5, 5, 5), 0.1f));
    }

    [Fact]
    public void TheSelectionsBoxHoldsItsCorners()
    {
        var bounds = CollisionEdits.Bounds(Square(), [1]);

        Assert.Equal((new Vector3(0, 0, 0), new Vector3(1, 0, 1)), bounds);
        Assert.Null(CollisionEdits.Bounds(Square(), []));
    }
}
