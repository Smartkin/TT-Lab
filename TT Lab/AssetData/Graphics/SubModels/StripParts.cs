using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.MeshProcessor;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SubItems;

namespace TT_Lab.AssetData.Graphics.SubModels;

/// <summary>
/// A part of a model read from the game's strips: its distinct vertexes, the triangles the strips draw and the strips themselves
/// </summary>
public record StripPart(List<Vertex> Vertexes, List<IndexedFace> Faces, StripLayout Layout);

public record RigidPartExport(List<Vertex> Vertexes, StripLayout Layout);

public record SkinPartExport(UInt32 Material, List<Vertex> Vertexes, StripLayout Layout, TwinSkinCompression? Compression);

public record BlendPartExport(UInt32 Material, List<Vertex> Vertexes, List<List<Vector4>> ShapeOffsets, StripLayout Layout, TwinSkinCompression? Compression);

/// <summary>
/// Converts between the game's strips and the vertexes and triangles TT Lab edits
/// </summary>
public static class StripParts
{
    public const StripWinding RigidWinding = StripWinding.EvenFlipped;
    public const StripWinding SkinWinding = StripWinding.OddFlipped;

    public static StripPart FromRigid(ITwinSubModel subModel)
    {
        subModel.CalculateData();
        var hasNormals = subModel.Normals.Count == subModel.Vertexes.Count && subModel.Vertexes.Count > 0;
        var hasEmits = subModel.EmitColor.Count == subModel.Vertexes.Count && subModel.Vertexes.Count > 0;
        var vertexes = new List<Vertex>(subModel.Vertexes.Count);
        for (var i = 0; i < subModel.Vertexes.Count; i++)
        {
            var vertex = new Vertex(subModel.Vertexes[i], subModel.Colors[i], subModel.UVW[i]);
            vertex.Color.StoresColorWithAlphaBlend = subModel.Colors[i].StoresColorWithAlphaBlend;
            if (hasNormals)
            {
                vertex.Normal = new Vector4(subModel.Normals[i]);
            }

            if (hasEmits)
            {
                vertex.EmitColor = new Vector4(subModel.EmitColor[i]) { StoresColorWithAlphaBlend = subModel.EmitColor[i].StoresColorWithAlphaBlend };
            }

            vertexes.Add(vertex);
        }

        var part = Merge(vertexes, subModel.GroupSizes, i => subModel.Connection[i], null, RigidWinding);
        part.Layout.Padding = subModel.Padding;
        if (subModel is ITwinStripGroups)
        {
            FaceByNormals(part, vertex => vertex.HasNormals ? vertex.Normal : new Vector4());
        }

        return part;
    }

    public static StripPart FromSkin(ITwinSubSkin subSkin)
    {
        subSkin.CalculateData();
        var vertexes = SkinVertexes(subSkin.Vertexes, subSkin.UVW, subSkin.Colors, subSkin.SkinJoints);
        var groups = subSkin as ITwinStripGroups;
        var part = Merge(vertexes, subSkin.GroupSizes, i => subSkin.SkinJoints[i].Connection, null, SkinWinding);
        part.Layout.Padding = subSkin.Padding;
        if (groups != null)
        {
            SetPalettes(part.Layout, groups.JointPalettes);
            FaceByNormals(part, SkinNormal);
        }

        return part;
    }

    /// <summary>
    /// Merges the models of a blend skin's material into a single part, each model becomes one batch of it
    /// </summary>
    /// <param name="shapeOffsets">Offset of every distinct vertex for every shape</param>
    public static StripPart FromBlend(IReadOnlyList<ITwinBlendSkinModel> models, Int32 shapesAmount, out List<List<Vector4>> shapeOffsets)
    {
        var vertexes = new List<Vertex>();
        var offsets = new List<Vector4[]>();
        var groupSizes = new List<Int32>();
        var connections = new List<Boolean>();
        var blendShapes = new List<Vector3>();
        var paddings = new List<TwinVifPadding>();
        var palettes = new List<List<Int32>>();
        var xbox = false;
        foreach (var model in models)
        {
            model.CalculateData();
            var modelVertexes = SkinVertexes(model.Vertexes, model.UVW, model.Colors, model.SkinJoints);
            connections.AddRange(model.SkinJoints.Select(j => j.Connection));
            paddings.Add(model.Padding);
            // The Xbox version keeps every strip of the material in one model, the PS2 one a model for every batch
            if (model is ITwinStripGroups groups)
            {
                xbox = true;
                groupSizes.AddRange(model.GroupSizes);
                blendShapes.AddRange(model.GroupSizes.Select(_ => model.BlendShape));
                palettes.AddRange(groups.JointPalettes);
            }
            else
            {
                groupSizes.Add(model.Vertexes.Count);
                blendShapes.Add(model.BlendShape);
            }

            vertexes.AddRange(modelVertexes);
            for (var i = 0; i < model.Vertexes.Count; i++)
            {
                offsets.Add(Enumerable.Range(0, shapesAmount).Select(shape => shape < model.Faces.Count ? model.Faces[shape].Vertices[i].Offset : new Vector4()).ToArray());
            }
        }

        var part = Merge(vertexes, groupSizes, i => connections[i], offsets, SkinWinding, out var firstPositions);
        for (var i = 0; i < part.Layout.Batches.Count; i++)
        {
            part.Layout.Batches[i].BlendShape = blendShapes[i];
        }

        if (xbox)
        {
            SetPalettes(part.Layout, palettes);
            FaceByNormals(part, SkinNormal);
        }

        // The game's tools padded every model of a skin the same way, models that needed no padding look like any style
        part.Layout.Padding = paddings.Contains(TwinVifPadding.NopPerByte) ? TwinVifPadding.NopPerByte : TwinVifPadding.QuadWord;
        shapeOffsets = Enumerable.Range(0, shapesAmount).Select(shape => firstPositions.Select(position => new Vector4(offsets[position][shape])).ToList()).ToList();
        return part;
    }

    /// <summary>
    /// Skins keep their normal in the position's W and the UV's Z and W
    /// </summary>
    public static Vector4 SkinNormal(Vertex vertex) => new(vertex.Position.W, vertex.UV.Z, vertex.UV.W, 0);

    private static void SetPalettes(StripLayout layout, List<List<Int32>> palettes)
    {
        for (var i = 0; i < layout.Batches.Count && i < palettes.Count; i++)
        {
            layout.Batches[i].Joints = [..palettes[i]];
        }
    }

    // The Xbox version's strips don't tell which way their triangles face, the vertexes' normals do
    private static void FaceByNormals(StripPart part, Func<Vertex, Vector4> getNormal)
    {
        part.Layout.IgnoresFacing = true;
        // A triangle drawn from both sides would face the same way twice, the second one is kept turned around
        var faced = new HashSet<(Int32, Int32, Int32)>();
        foreach (var face in part.Faces)
        {
            var indexes = face.Indexes!;
            var a = part.Vertexes[indexes[0]];
            var b = part.Vertexes[indexes[1]];
            var c = part.Vertexes[indexes[2]];
            var ab = (X: b.Position.X - a.Position.X, Y: b.Position.Y - a.Position.Y, Z: b.Position.Z - a.Position.Z);
            var ac = (X: c.Position.X - a.Position.X, Y: c.Position.Y - a.Position.Y, Z: c.Position.Z - a.Position.Z);
            var normal = (X: ab.Y * ac.Z - ab.Z * ac.Y, Y: ab.Z * ac.X - ab.X * ac.Z, Z: ab.X * ac.Y - ab.Y * ac.X);
            var vertexNormals = new[] { a, b, c }.Select(getNormal).ToList();
            var facing = vertexNormals.Sum(n => n.X * normal.X + n.Y * normal.Y + n.Z * normal.Z);
            var turned = (indexes[0], indexes[2], indexes[1]);
            if (facing < 0 && !faced.Contains(Rotate(turned)))
            {
                (indexes[1], indexes[2]) = (indexes[2], indexes[1]);
            }

            faced.Add(Rotate((indexes[0], indexes[1], indexes[2])));
        }

        static (Int32, Int32, Int32) Rotate((Int32 A, Int32 B, Int32 C) face)
        {
            if (face.A < face.B && face.A < face.C)
            {
                return face;
            }

            return face.B < face.C ? (face.B, face.C, face.A) : (face.C, face.A, face.B);
        }
    }

    private static List<Vertex> SkinVertexes(List<Vector4> positions, List<Vector4> uvs, List<Vector4> colors, List<VertexJointInfo> joints)
    {
        var vertexes = new List<Vertex>(positions.Count);
        for (var i = 0; i < positions.Count; i++)
        {
            var joint = joints[i];
            vertexes.Add(new Vertex(positions[i], colors[i], uvs[i])
            {
                JointInfo = new VertexJointInfo
                {
                    Weight1 = joint.Weight1,
                    Weight2 = joint.Weight2,
                    Weight3 = joint.Weight3,
                    JointIndex1 = joint.JointIndex1,
                    JointIndex2 = joint.JointIndex2,
                    JointIndex3 = joint.JointIndex3,
                    WeightsAmount = joint.WeightsAmount,
                    Connection = true
                }
            });
        }

        return vertexes;
    }

    private static StripPart Merge(List<Vertex> positions, IReadOnlyList<Int32> groupSizes, Func<Int32, Boolean> draws, List<Vector4[]>? offsets, StripWinding winding)
    {
        return Merge(positions, groupSizes, draws, offsets, winding, out _);
    }

    private static StripPart Merge(List<Vertex> positions, IReadOnlyList<Int32> groupSizes, Func<Int32, Boolean> draws, List<Vector4[]>? offsets, StripWinding winding,
        out List<Int32> firstPositions)
    {
        var layout = StripLayout.FromStrips(groupSizes, draws, new PositionComparer(positions, offsets), out firstPositions);
        var vertexes = firstPositions.Select(position => positions[position]).ToList();
        return new StripPart(vertexes, layout.GetFaces(winding), layout);
    }

    /// <summary>
    /// The stored layout when it still draws the part's triangles, strips built anew otherwise
    /// </summary>
    public static StripLayout GetValidLayout(StripLayout? layout, List<Vertex> vertexes, List<IndexedFace> faces, StripWinding winding)
    {
        if (layout != null && layout.Draws(faces, vertexes.Count, winding))
        {
            return layout;
        }

        return Stripifier.Stripify(faces, winding);
    }

    // Two strip positions are the same vertex when everything the game stores for them matches, blend skins' shape offsets included
    private sealed class PositionComparer(List<Vertex> vertexes, List<Vector4[]>? offsets) : IEqualityComparer<Int32>
    {
        public Boolean Equals(Int32 x, Int32 y)
        {
            if (!Vertex.ExactComparer.Equals(vertexes[x], vertexes[y]))
            {
                return false;
            }

            if (offsets == null)
            {
                return true;
            }

            for (var shape = 0; shape < offsets[x].Length; shape++)
            {
                var a = offsets[x][shape];
                var b = offsets[y][shape];
                if (a.GetBinaryX() != b.GetBinaryX() || a.GetBinaryY() != b.GetBinaryY() || a.GetBinaryZ() != b.GetBinaryZ())
                {
                    return false;
                }
            }

            return true;
        }

        public Int32 GetHashCode(Int32 position)
        {
            return Vertex.ExactComparer.GetHashCode(vertexes[position]);
        }
    }
}
