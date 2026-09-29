using System;
using System.Collections.Generic;
using System.Linq;
using NVector3 = System.Numerics.Vector3;
using System.Text.Json.Nodes;
using Twinsanity.TwinsanityInterchange.Common;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// Collision hulls in TT Lab model files: a `hull` node with the vertexes and faces Blender shows and the planes, axes and edges the
/// game reads next to them
/// </summary>
/// <remarks>
/// The add-on writes the vertexes and faces it imported back as `twin_vertices` and `twin_faces`. The game's planes, axes and edges
/// stay while those are still what the mesh has, a hull edited or moved in Blender gets them worked out again
/// </remarks>
public static class TlmHulls
{
    public const string Kind = "hull";
    private const string VerticesKey = "vertices";
    private const string FacesKey = "faces";
    private const string PlanesKey = "planes";
    private const string EdgeDirectionsKey = "edge_directions";
    private const string FaceNormalsKey = "face_normals";
    private const string EdgesKey = "edges";
    private const string TwinVerticesKey = "twin_vertices";
    private const string TwinFacesKey = "twin_faces";

    public static JsonObject Write(TlmFile file, TwinCollisionHull hull, string name)
    {
        var node = TlmNodes.Create(Kind, name);
        node[VerticesKey] = file.Write(Floats(hull.Vertexes));
        node[FacesKey] = file.Write(FaceBytes(hull.Faces));
        node[PlanesKey] = file.Write(Floats(hull.Planes));
        node[EdgeDirectionsKey] = file.Write(Floats(hull.EdgeDirections));
        node[FaceNormalsKey] = file.Write(Floats(hull.FaceNormals));
        node[EdgesKey] = file.Write(hull.Edges.SelectMany(edge => edge).ToArray().AsSpan());
        return node;
    }

    /// <summary>
    /// The hull of a node, moved by the node's transform
    /// </summary>
    public static TwinCollisionHull Read(TlmFile file, JsonObject node)
    {
        var hull = new TwinCollisionHull
        {
            Vertexes = Vectors(file.Read<Single>(node[VerticesKey])),
            Faces = ReadFaces(file.Read<Byte>(node[FacesKey])),
            Planes = Vectors(file.Read<Single>(node[PlanesKey])),
            EdgeDirections = Vectors(file.Read<Single>(node[EdgeDirectionsKey])),
            FaceNormals = Vectors(file.Read<Single>(node[FaceNormalsKey])),
            Edges = Pairs(file.Read<Byte>(node[EdgesKey]))
        };
        var transform = node.GetTransform();
        var edited = !TlmNodes.IsIdentity(transform) || hull.Planes.Count == 0 && hull.Faces.Count > 0;
        if (node[TwinVerticesKey] != null)
        {
            var twinVertexes = file.Read<Single>(node[TwinVerticesKey]);
            var twinFaces = file.Read<Byte>(node[TwinFacesKey]);
            edited |= !twinVertexes.AsSpan().SequenceEqual(Floats(hull.Vertexes)) || !twinFaces.AsSpan().SequenceEqual(FaceBytes(hull.Faces));
        }

        if (!TlmNodes.IsIdentity(transform))
        {
            hull.Vertexes = hull.Vertexes.Select(vertex =>
            {
                var moved = NVector3.Transform(new NVector3(vertex.X, vertex.Y, vertex.Z), transform);
                return new Vector4(moved.X, moved.Y, moved.Z, vertex.W);
            }).ToList();
        }

        if (edited)
        {
            hull.ComputeFromFaces();
        }

        return hull;
    }

    private static Single[] Floats(List<Vector4> vectors)
    {
        return vectors.SelectMany(vector => new[] { vector.X, vector.Y, vector.Z, vector.W }).ToArray();
    }

    private static List<Vector4> Vectors(Single[] values)
    {
        var result = new List<Vector4>(values.Length / 4);
        for (var i = 0; i + 3 < values.Length; i += 4)
        {
            result.Add(new Vector4(values[i], values[i + 1], values[i + 2], values[i + 3]));
        }

        return result;
    }

    private static Byte[] FaceBytes(List<List<Byte>> faces)
    {
        return faces.SelectMany(face => face.Prepend((Byte)face.Count)).ToArray();
    }

    private static List<List<Byte>> ReadFaces(Byte[] bytes)
    {
        var faces = new List<List<Byte>>();
        for (var offset = 0; offset < bytes.Length; offset += 1 + bytes[offset])
        {
            faces.Add(bytes.Skip(offset + 1).Take(bytes[offset]).ToList());
        }

        return faces;
    }

    private static List<List<Byte>> Pairs(Byte[] bytes)
    {
        var pairs = new List<List<Byte>>();
        for (var i = 0; i + 1 < bytes.Length; i += 2)
        {
            pairs.Add(new List<Byte> { bytes[i], bytes[i + 1] });
        }

        return pairs;
    }
}
