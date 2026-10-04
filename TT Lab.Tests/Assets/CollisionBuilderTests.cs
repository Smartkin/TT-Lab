using System.Numerics;
using TT_Lab.AssetData.Instance.Collision;

namespace TT_Lab.Tests.Assets;

// The scenery mode makes collision of meshes the way the add-on's collision_builder.py does, these are its tests
public class CollisionBuilderTests
{
    // Two triangles of a square floor, counter-clockwise seen from above like a mesh drawn on it (Y up)
    private static readonly (Vector3, Vector3, Vector3)[] Floor =
    [
        (new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1)),
        (new Vector3(0, 0, 0), new Vector3(1, 0, 1), new Vector3(1, 0, 0)),
    ];

    private static float NormalY(IReadOnlyList<Vector3> positions, (int A, int B, int C) triangle)
    {
        var ab = positions[triangle.B] - positions[triangle.A];
        var ac = positions[triangle.C] - positions[triangle.A];
        return ab.Z * ac.X - ab.X * ac.Z;
    }

    [Fact]
    public void CornersOfMeshesBecomeSharedVertexes()
    {
        var result = CollisionBuilder.AddTriangles([], [], Floor);

        Assert.Equal(4, result.Positions.Count);
        Assert.Equal(2, result.Triangles.Count);
        Assert.Equal((0, 0), (result.Skipped, result.Dropped));
    }

    [Fact]
    public void CornersCloserThanTheWeldDistanceAreOne()
    {
        (Vector3, Vector3, Vector3)[] nearby = [(new Vector3(0.0005f, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0))];

        var welded = CollisionBuilder.AddTriangles([], [], [.. Floor, .. nearby], weld: 0.001f);
        var apart = CollisionBuilder.AddTriangles([], [], [.. Floor, .. nearby], weld: 0.0001f);

        Assert.Equal(5, welded.Positions.Count);
        Assert.Equal(6, apart.Positions.Count);
    }

    // The game winds a floor so its normal points down, into the solid, the other way from the mesh drawn on it
    [Fact]
    public void TrianglesAreWoundLikeTheGames()
    {
        var flipped = CollisionBuilder.AddTriangles([], [], Floor);
        var kept = CollisionBuilder.AddTriangles([], [], Floor, flip: false);

        Assert.All(flipped.Triangles, triangle => Assert.True(NormalY(flipped.Positions, triangle) < 0));
        Assert.All(kept.Triangles, triangle => Assert.True(NormalY(kept.Positions, triangle) > 0));
    }

    [Fact]
    public void FlatTrianglesAreLeftOutWithoutTheirVertexes()
    {
        var line = (new Vector3(5, 0, 0), new Vector3(6, 0, 0), new Vector3(7, 0, 0));
        var point = (new Vector3(9, 0, 0), new Vector3(9, 0, 0.0005f), new Vector3(9.0005f, 0, 0));

        var result = CollisionBuilder.AddTriangles([], [], [.. Floor, line, point]);

        Assert.Equal(2, result.Dropped);
        Assert.Equal(4, result.Positions.Count);
    }

    // Adding to a collision keeps it as it is: its vertexes stay, new corners join them, its triangles aren't made again either way round
    [Fact]
    public void WhatTheCollisionHasIsLeftOut()
    {
        Vector3[] positions = [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)];

        var result = CollisionBuilder.AddTriangles(positions, [(0, 1, 2)], [.. Floor, (new Vector3(0, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 1, 0))]);

        Assert.Equal(1, result.Skipped);
        Assert.Equal([new Vector3(0, 1, 0)], result.Positions);
        Assert.Equal([[0, 2, 3], [0, 2, 4]], result.Triangles.Select(t => new[] { t.A, t.B, t.C }.Order().ToArray()).OrderBy(t => t[2]).ToArray());
    }

    [Fact]
    public void TheSameTriangleTwiceIsMadeOnce()
    {
        var result = CollisionBuilder.AddTriangles([], [], [.. Floor, .. Floor]);

        Assert.Equal((2, 2), (result.Triangles.Count, result.Skipped));
    }

    // The 12 triangles of a box, counter-clockwise seen from outside
    private static List<(Vector3, Vector3, Vector3)> Box(Vector3 low, Vector3 high)
    {
        Vector3 Corner(int x, int y, int z) => new(x == 1 ? high.X : low.X, y == 1 ? high.Y : low.Y, z == 1 ? high.Z : low.Z);
        int[][] quads =
        [
            [0, 0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 0], [1, 0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1], [0, 0, 0, 1, 0, 0, 1, 0, 1, 0, 0, 1],
            [0, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 0], [0, 0, 0, 0, 1, 0, 1, 1, 0, 1, 0, 0], [0, 0, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1],
        ];
        var triangles = new List<(Vector3, Vector3, Vector3)>();
        foreach (var quad in quads)
        {
            var (a, b, c, d) = (Corner(quad[0], quad[1], quad[2]), Corner(quad[3], quad[4], quad[5]), Corner(quad[6], quad[7], quad[8]), Corner(quad[9], quad[10], quad[11]));
            triangles.Add((a, b, c));
            triangles.Add((a, c, d));
        }

        return triangles;
    }

    // A square floor cut into cells, counter-clockwise seen from above
    private static List<(Vector3, Vector3, Vector3)> CutFloor(int cells, float size, Func<float, float, float>? height = null)
    {
        var step = size / cells;
        Vector3 Point(int i, int j) => new(i * step, height?.Invoke(i * step, j * step) ?? 0, j * step);
        var triangles = new List<(Vector3, Vector3, Vector3)>();
        for (var i = 0; i < cells; i++)
        {
            for (var j = 0; j < cells; j++)
            {
                var (a, b, c, d) = (Point(i, j), Point(i, j + 1), Point(i + 1, j + 1), Point(i + 1, j));
                triangles.Add((a, b, c));
                triangles.Add((a, c, d));
            }
        }

        return triangles;
    }

    // The game only collides the player with 32 triangles at a time and slows Crash down to a fifth where there are more: collision is
    // made as coarse as the game's. These are collision_builder.py's CoarseCollisionTests
    [Fact]
    public void AFinelyCutFloorBecomesItsCorners()
    {
        var result = CollisionBuilder.AddMeshes([], [], [CutFloor(10, 10)]);

        Assert.Equal(2, result.Triangles.Count);
        Assert.Equal([new Vector3(0, 0, 0), new Vector3(0, 0, 10), new Vector3(10, 0, 0), new Vector3(10, 0, 10)], result.Positions.OrderBy(p => p.X).ThenBy(p => p.Z));
        Assert.All(result.Triangles, triangle => Assert.True(NormalY(result.Positions, triangle) < 0));
    }

    // A box with its edges bevelled less than the tolerance becomes the box: a platform's mesh of 167 triangles stands on 12
    [Fact]
    public void ABevelledBoxBecomesABox()
    {
        const float bevel = 0.05f;
        var points = new List<CollisionVector>();
        foreach (var x in new[] { -2f, 2f })
        {
            foreach (var y in new[] { -0.5f, 0.5f })
            {
                foreach (var z in new[] { -1f, 1f })
                {
                    points.Add(new CollisionVector(x - MathF.CopySign(bevel, x), y, z));
                    points.Add(new CollisionVector(x, y - MathF.CopySign(bevel, y), z));
                    points.Add(new CollisionVector(x, y, z - MathF.CopySign(bevel, z)));
                }
            }
        }

        var (hullPositions, hullTriangles) = CollisionHulls.Build(points)!.Value;
        var mesh = hullTriangles.Select(t => (hullPositions[t.A].ToSingle(), hullPositions[t.B].ToSingle(), hullPositions[t.C].ToSingle())).ToList();

        var result = CollisionBuilder.AddMeshes([], [], [mesh]);

        Assert.True(mesh.Count > 12);
        Assert.Equal((1, 12, 8), (result.Hulls, result.Triangles.Count, result.Positions.Count));
    }

    // Planks a little apart are one hull, the gaps between them are narrower than Crash
    [Fact]
    public void PlanksCloseTogetherAreOneHull()
    {
        var planks = Box(new Vector3(0, 0, 0), new Vector3(1, 0.2f, 4)).Concat(Box(new Vector3(1.05f, 0, 0), new Vector3(2.05f, 0.2f, 4)))
            .Concat(Box(new Vector3(2.1f, 0, 0), new Vector3(3.1f, 0.2f, 4))).ToList();

        var result = CollisionBuilder.AddMeshes([], [], [planks]);

        Assert.Equal((1, 12), (result.Hulls, result.Triangles.Count));
        Assert.Equal(3.1f, result.Positions.Max(position => position.X));
        // A hull only grows: the planks' top stays where it is, the slab doesn't get thinner
        Assert.Equal((0f, 0.2f), (result.Positions.Min(position => position.Y), result.Positions.Max(position => position.Y)));
    }

    // Under a table there's room, its hull doesn't fit: the top and every leg are hulls of their own
    [Fact]
    public void ATableKeepsTheRoomUnderIt()
    {
        var table = Box(new Vector3(0, 2, 0), new Vector3(4, 2.2f, 2)).ToList();
        foreach (var x in new[] { 0.2f, 3.6f })
        {
            foreach (var z in new[] { 0.2f, 1.6f })
            {
                table.AddRange(Box(new Vector3(x, 0, z), new Vector3(x + 0.2f, 2, z + 0.2f)));
            }
        }

        var result = CollisionBuilder.AddMeshes([], [], [table]);

        Assert.Equal((5, 60), (result.Hulls, result.Triangles.Count));
    }

    // Layers drawn just over the ground (grass, water edges) are no floor of their own, a mesh lying on the collision is there already
    [Fact]
    public void LayersLyingOnOthersAreLeftOut()
    {
        (Vector3, Vector3, Vector3)[] decal =
        [
            (new Vector3(3, 0.05f, 3), new Vector3(3, 0.05f, 7), new Vector3(7, 0.05f, 7)),
            (new Vector3(3, 0.05f, 3), new Vector3(7, 0.05f, 7), new Vector3(7, 0.05f, 3)),
        ];

        var result = CollisionBuilder.AddMeshes([], [], [CutFloor(1, 10), decal]);
        var onto = CollisionBuilder.AddMeshes([new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10), new Vector3(0, 0, 10)], [(0, 1, 2), (0, 2, 3)],
            [CutFloor(8, 10)]);

        Assert.Equal((1, 2), (result.Covered, result.Triangles.Count));
        Assert.Equal((1, 0, 0), (onto.Covered, onto.Triangles.Count, onto.Positions.Count));
    }

    // Bumps everywhere Crash goes: with every bump he'd touch more triangles than the game takes, the floor is made coarser until he doesn't
    [Fact]
    public void CrowdedMeshesAreMadeCoarser()
    {
        var crate = CutFloor(30, 3, (x, z) => 0.15f * MathF.Sin(x * 2 * MathF.PI / 0.8f) * MathF.Sin(z * 2 * MathF.PI / 0.8f));

        var fine = CollisionBuilder.AddMeshes([], [], [crate], hullDistance: 0, coarserWhereCrowded: false);
        var coarse = CollisionBuilder.AddMeshes([], [], [crate], hullDistance: 0);
        var before = CollisionBuilder.Crowd(fine.Positions, fine.Triangles, Enumerable.Range(0, fine.Triangles.Count));
        var after = CollisionBuilder.Crowd(coarse.Positions, coarse.Triangles, Enumerable.Range(0, coarse.Triangles.Count));

        Assert.Equal((0, 1), (fine.Coarsened, coarse.Coarsened));
        Assert.True(before.Most > CollisionBuilder.MostTriangles);
        Assert.True(after.Most < before.Most);
        Assert.True(coarse.Triangles.Count < fine.Triangles.Count);
        Assert.NotNull(before.Worst);
    }

    [Fact]
    public void NoToleranceAndNoHullsKeepEveryTriangle()
    {
        var mesh = CutFloor(4, 2);

        var exact = CollisionBuilder.AddMeshes([], [], [mesh], tolerance: 0, hullDistance: 0);
        var triangles = CollisionBuilder.AddTriangles([], [], mesh);

        Assert.Equal(triangles.Positions, exact.Positions);
        Assert.Equal(triangles.Triangles, exact.Triangles);
    }

    // The collision's own vertexes stay, a mesh made coarser keeps joining them
    [Fact]
    public void TheCollisionsVertexesStay()
    {
        Vector3[] positions = [new(0, 0, 0), new(0, 0, 10), new(10, 0, 10)];

        var result = CollisionBuilder.AddMeshes(positions, [], [CutFloor(5, 10)]);

        Assert.Equal(2, result.Triangles.Count);
        Assert.Equal([new Vector3(10, 0, 0)], result.Positions);
        Assert.Equal([0, 1, 2, 3], result.Triangles.SelectMany(t => new[] { t.A, t.B, t.C }).Distinct().Order());
    }

    [Fact]
    public void CrowdingCountsTheTrianglesAroundAFloor()
    {
        var positions = new List<Vector3> { new(0, 0, 0), new(0, 0, 1), new(1, 0, 0) };
        positions.AddRange(Enumerable.Range(0, 40).Select(i => new Vector3(0.1f * i, 0.5f, 0)));
        // A floor wound like the game's (its normal down) and a fan of 38 thin triangles standing over it
        var triangles = new List<(int, int, int)> { (0, 2, 1) };
        triangles.AddRange(Enumerable.Range(0, 38).Select(i => (0, 3 + i, 4 + i)));

        var crowding = CollisionBuilder.Crowd(positions, triangles, [0]);

        Assert.Equal((1, 39), (crowding.Places, crowding.Most));
    }
}
