using Twinsanity.TwinsanityInterchange.Common;
using static TT_Lab.Tests.Support.TestGeometry;

namespace TT_Lab.Tests.TwinTech;

// The convex hulls the game collides models with: what the game's own box builder lays out, what its hull builder works out
public class CollisionHullTests
{
    private static readonly Vector4 Min = new(-1, 0, -2, 1);
    private static readonly Vector4 Max = new(1, 3, 2, 1);

    [Fact]
    public void BoxesAreLaidOutLikeTheGamesBoxBuilder()
    {
        var bytes = Serialize(TwinCollisionHull.CreateBox(Min, Max));

        // 8 vertexes, 12 edges, 6 faces, 3 edge directions, 3 face normals, then where every block starts and the blob's size
        Assert.Equal(new ushort[] { 8, 12, 6, 3, 3, 0x80, 0xE0, 0x110, 0x140, 0x146, 0x164 }, Enumerable.Range(0, 11).Select(i => BitConverter.ToUInt16(bytes, i * 2)));
        Assert.Equal(0x17C, BitConverter.ToInt32(bytes, 22));
        Assert.Equal(26 + 0x17C, bytes.Length);
        // Face offsets, then every face's size and vertexes, then the edges as vertex pairs
        Assert.Equal(new byte[] { 0, 5, 10, 15, 20, 25 }, bytes.Skip(26 + 0x140).Take(6));
        Assert.Equal(new byte[] { 4, 0, 1, 2, 3, 4, 0, 4, 5, 1 }, bytes.Skip(26 + 0x146).Take(10));
        Assert.Equal(new byte[] { 0, 1, 1, 2, 2, 3, 3, 0 }, bytes.Skip(26 + 0x164).Take(8));
    }

    [Fact]
    public void HullsComeBackFromTheirBytes()
    {
        var pyramid = Pyramid();
        var bytes = Serialize(pyramid);

        var read = Deserialize(new TwinCollisionHull(), bytes);

        Assert.Equal(bytes, Serialize(read));
        Assert.Equal(pyramid.Faces, read.Faces);
        Assert.Equal(pyramid.Edges, read.Edges);
        Assert.Equal(pyramid.Vertexes.Select(Serialize), read.Vertexes.Select(Serialize));
    }

    // The planes, axes and edges the game reads follow from the vertexes and faces, like the game's hull builder works them out
    [Fact]
    public void PlanesAndAxesFollowFromTheFaces()
    {
        var box = TwinCollisionHull.CreateBox(Min, Max);
        var hull = new TwinCollisionHull { Vertexes = box.Vertexes.ToList(), Faces = box.Faces.Select(face => face.ToList()).ToList() };

        hull.ComputeFromFaces();

        Assert.True(hull.DescribesFaces());
        Assert.Equal(box.Planes.Select(Serialize), hull.Planes.Select(Serialize));
        Assert.Equal(box.Faces, hull.Faces);
        Assert.Equal(box.Edges, hull.Edges);
        Assert.Equal(3, hull.FaceNormals.Count);
        Assert.Equal(3, hull.EdgeDirections.Count);
        Assert.All(hull.FaceNormals.Concat(hull.EdgeDirections), axis => Assert.Equal(1.0f, MathF.Sqrt(axis.X * axis.X + axis.Y * axis.Y + axis.Z * axis.Z), 5));
    }

    [Fact]
    public void FacesWoundInwardGetTurnedAround()
    {
        var box = TwinCollisionHull.CreateBox(Min, Max);
        var hull = new TwinCollisionHull { Vertexes = box.Vertexes.ToList(), Faces = box.Faces.Select(face => face.ToList()).ToList() };
        hull.Faces[0].Reverse();

        hull.ComputeFromFaces();

        Assert.Equal(box.Faces[0], hull.Faces[0]);
        // Turning the normal around makes negative zeros of its zeros
        Assert.Equal((box.Planes[0].X, box.Planes[0].Y, box.Planes[0].Z, box.Planes[0].W), (hull.Planes[0].X, hull.Planes[0].Y, hull.Planes[0].Z, hull.Planes[0].W));
    }

    [Fact]
    public void MovedVertexesLeaveTheGamesPlanesBehind()
    {
        var hull = TwinCollisionHull.CreateBox(Min, Max);
        Assert.True(hull.DescribesFaces());

        hull.Vertexes[6] = new Vector4(Max.X, Max.Y + 1, Max.Z, 1);

        Assert.False(hull.DescribesFaces());
    }

    // A pyramid: the apex over a square base, 5 faces and 8 edges
    private static TwinCollisionHull Pyramid()
    {
        var hull = new TwinCollisionHull
        {
            Vertexes = [new Vector4(0, 2, 0, 1), new Vector4(-1, 0, -1, 1), new Vector4(1, 0, -1, 1), new Vector4(1, 0, 1, 1), new Vector4(-1, 0, 1, 1)],
            Faces = [[1, 2, 3, 4], [0, 2, 1], [0, 3, 2], [0, 4, 3], [0, 1, 4]]
        };
        hull.ComputeFromFaces();
        Assert.Equal(5, hull.Planes.Count);
        Assert.Equal(8, hull.Edges.Count);
        Assert.True(hull.DescribesFaces());
        return hull;
    }
}
