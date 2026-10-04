using System.Numerics;
using TT_Lab.AssetData.Instance.Scenery;

namespace TT_Lab.Tests.Assets;

// Placeholder shapes stand on the ground facing outwards like the game's meshes, closed, their texture repeating once a unit
public class PlaceholderShapesTests
{
    public static TheoryData<PlaceholderShape> Shapes => new(Enum.GetValues<PlaceholderShape>());

    private static Vector3 Normal((Vector3 A, Vector3 B, Vector3 C) triangle) => Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A);

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ShapesStandOnTheGroundFacingOutwards(PlaceholderShape shape)
    {
        var mesh = PlaceholderShapes.Make(shape);
        var triangles = mesh.Triangles().ToList();

        Assert.Equal(0.0f, mesh.Positions.Min(position => position.Y), 1e-5f);
        Assert.All(triangles, triangle => Assert.True(Normal(triangle).Length() > 1e-6f, $"{shape} has a flat triangle"));
        if (shape == PlaceholderShape.Plane)
        {
            Assert.All(triangles, triangle => Assert.True(Normal(triangle).Y > 0));
            return;
        }

        // Every shape is convex, its triangles face away from its middle
        var middle = mesh.Positions.Aggregate(Vector3.Add) / mesh.Positions.Count;
        Assert.All(triangles, triangle => Assert.True(Vector3.Dot(Normal(triangle), (triangle.A + triangle.B + triangle.C) / 3 - middle) > 0, $"{shape} has a triangle facing in"));
        // The vertexes' normals go the same way as their triangles
        foreach (var (a, b, c) in mesh.Faces)
        {
            var normal = Normal((mesh.Positions[a], mesh.Positions[b], mesh.Positions[c]));
            Assert.All(new[] { a, b, c }, corner => Assert.True(Vector3.Dot(mesh.Normals[corner], normal) > 0, $"{shape}'s vertex normal points in"));
        }
    }

    // Each edge is two triangles' going opposite ways: nothing open, nothing turned around
    [Theory]
    [MemberData(nameof(Shapes))]
    public void SolidShapesAreClosed(PlaceholderShape shape)
    {
        if (shape == PlaceholderShape.Plane)
        {
            return;
        }

        var mesh = PlaceholderShapes.Make(shape);
        (int, int, int) Key(Vector3 position) => ((int)MathF.Round(position.X * 1000), (int)MathF.Round(position.Y * 1000), (int)MathF.Round(position.Z * 1000));
        var edges = new Dictionary<((int, int, int), (int, int, int)), int>();
        foreach (var (a, b, c) in mesh.Triangles())
        {
            foreach (var (from, to) in new[] { (a, b), (b, c), (c, a) })
            {
                var edge = (Key(from), Key(to));
                edges[edge] = edges.GetValueOrDefault(edge) + 1;
            }
        }

        Assert.All(edges, edge => Assert.Equal((1, 1), (edge.Value, edges.GetValueOrDefault((edge.Key.Item2, edge.Key.Item1)))));
    }

    // Like a new chunk's ground: two greys alternating, the texture two squares across
    [Fact]
    public void ThePlaceholderTextureIsAChecker()
    {
        var pixels = SceneryPlaceholders.CheckerPixels();
        var size = (int)MathF.Sqrt(pixels.Length);

        Assert.Equal(2, pixels.Distinct().Count());
        Assert.NotEqual(pixels[0], pixels[size - 1]);
        Assert.NotEqual(pixels[0], pixels[(size - 1) * size]);
        Assert.Equal(pixels[0], pixels[size * size - 1]);
    }

    [Fact]
    public void TexturesRepeatOnceAUnit()
    {
        var cube = PlaceholderShapes.Make(PlaceholderShape.Cube, 2.0f);

        Assert.Equal(0.0f, cube.Uvs.Min(uv => MathF.Min(uv.X, uv.Y)), 1e-5f);
        Assert.Equal(2.0f, cube.Uvs.Max(uv => MathF.Max(uv.X, uv.Y)), 1e-5f);
        Assert.Equal(new Vector3(1, 2, 1), cube.Positions.Aggregate(Vector3.Max));
    }
}
