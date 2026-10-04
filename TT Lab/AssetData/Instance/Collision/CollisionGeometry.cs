using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TT_Lab.Assets;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// A triangle of a collision, its corners by the collision's vertexes
/// </summary>
public readonly record struct CollisionFace(Int32 A, Int32 B, Int32 C, LabURI Surface)
{
    public IEnumerable<Int32> Corners
    {
        get
        {
            yield return A;
            yield return B;
            yield return C;
        }
    }
}

/// <summary>
/// What a collision is made of at one moment, never changed once made: edits make a new one (<see cref="CollisionEdits"/>) and the
/// document's history keeps the one before, the collision's tens of thousands of triangles never become nodes of the property graph
/// </summary>
public sealed class CollisionGeometry
{
    public CollisionGeometry(IReadOnlyList<Vector4> vertexes, IReadOnlyList<CollisionFace> triangles)
    {
        Vertexes = vertexes;
        Triangles = triangles;
    }

    /// <summary>
    /// The vertexes, W as the collision has it
    /// </summary>
    public IReadOnlyList<Vector4> Vertexes { get; }

    public IReadOnlyList<CollisionFace> Triangles { get; }

    public Vector3 Position(Int32 vertex)
    {
        var v = Vertexes[vertex];
        return new Vector3(v.X, v.Y, v.Z);
    }

    public (Vector3 A, Vector3 B, Vector3 C) Corners(Int32 triangle)
    {
        var face = Triangles[triangle];
        return (Position(face.A), Position(face.B), Position(face.C));
    }

    public static CollisionGeometry Of(IEnumerable<Twinsanity.TwinsanityInterchange.Common.Vector4> vertexes, IEnumerable<CollisionTriangle> triangles)
    {
        return new CollisionGeometry(vertexes.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToArray(),
            triangles.Select(t => new CollisionFace(t.Face.Indexes![0], t.Face.Indexes[1], t.Face.Indexes[2], t.Surface)).ToArray());
    }
}
