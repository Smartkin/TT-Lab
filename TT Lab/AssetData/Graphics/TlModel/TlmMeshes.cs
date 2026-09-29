using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.MeshProcessor;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Matrix4x4 = System.Numerics.Matrix4x4;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// Writes and reads the parts of models in TT Lab model files
/// </summary>
/// <remarks>
/// A part keeps the game's values Blender can't show next to the ones it shows, and they're used while what Blender shows still
/// matches them. The strips the game draws are only used again while they still draw the part's triangles, so edited parts get
/// stripified anew
/// </remarks>
public static class TlmMeshes
{
    public static JsonObject WriteMesh(TlmFile file, IEnumerable<(ModelPart Part, Int32 Material)> parts, Boolean skinned)
    {
        return new JsonObject { ["parts"] = new JsonArray(parts.Select(p => (JsonNode)(skinned ? WriteSkinPart(file, p.Part, p.Material) : WriteRigidPart(file, p.Part, p.Material))).ToArray()) };
    }

    /// <param name="file">File the mesh is in</param>
    /// <param name="mesh">The mesh</param>
    /// <param name="skinned">Whether it's a skin</param>
    /// <param name="transform">Where the rigid mesh's node was moved to, baked into the vertexes</param>
    public static List<(ModelPart Part, Int32 Material)> ReadMesh(TlmFile file, JsonObject? mesh, Boolean skinned, Matrix4x4? transform = null)
    {
        var parts = mesh?["parts"] as JsonArray;
        return parts == null ? [] : parts.OfType<JsonObject>().Select(part => (skinned ? ReadSkinPart(file, part) : ReadRigidPart(file, part, transform), part.GetInt("material", -1))).ToList();
    }

    public static JsonObject WriteRigidPart(TlmFile file, ModelPart part, Int32 material)
    {
        part = SeparateRepeatedFaces(part);
        var vertexes = part.Vertexes;
        var json = StartPart(file, part, material);
        json["position"] = file.Write(vertexes.SelectMany(v => new[] { v.Position.X, v.Position.Y, v.Position.Z }).ToArray().AsSpan());
        if (vertexes.Any(v => v.HasNormals))
        {
            json["normal"] = file.Write(vertexes.SelectMany(v => Floats(v.GetUnitNormal())).ToArray().AsSpan());
            json["twin_normal"] = file.Write(vertexes.SelectMany(v => new[] { v.Normal.X, v.Normal.Y, v.Normal.Z }).ToArray().AsSpan());
        }

        json["uv"] = file.Write(vertexes.SelectMany(v => new[] { v.UV.X, v.UV.Y }).ToArray().AsSpan());
        if (vertexes.Any(v => v.UV.Z != 1.0f))
        {
            json["uv_q"] = file.Write(vertexes.Select(v => v.UV.Z).ToArray().AsSpan());
        }

        json["color"] = file.Write(vertexes.SelectMany(v => ToBytes(v.Color)).ToArray().AsSpan());
        if (vertexes.Any(v => v.HasEmitColor))
        {
            json["emit_color"] = file.Write(vertexes.SelectMany(v => ToBytes(v.EmitColor)).ToArray().AsSpan());
        }

        if (vertexes.Any(v => v.Color.StoresColorWithAlphaBlend || v.HasEmitColor && v.EmitColor.StoresColorWithAlphaBlend))
        {
            json["alpha_flags"] = file.Write(vertexes.SelectMany(v => new[]
            {
                (Byte)(v.Color.StoresColorWithAlphaBlend ? 1 : 0), (Byte)(v.HasEmitColor && v.EmitColor.StoresColorWithAlphaBlend ? 1 : 0)
            }).ToArray().AsSpan());
        }

        return json;
    }

    public static JsonObject WriteSkinPart(TlmFile file, ModelPart part, Int32 material)
    {
        part = SeparateRepeatedFaces(part);
        var vertexes = part.Vertexes;
        var json = StartPart(file, part, material);
        json["position"] = file.Write(vertexes.SelectMany(v => new[] { v.Position.X, v.Position.Y, v.Position.Z }).ToArray().AsSpan());
        json["normal"] = file.Write(vertexes.SelectMany(v => Floats(SkinUnitNormal(v))).ToArray().AsSpan());
        // Skins keep their normal in the position's W and the UV's Z and W
        json["twin_normal"] = file.Write(vertexes.SelectMany(v => new[] { v.Position.W, v.UV.Z, v.UV.W }).ToArray().AsSpan());
        json["uv"] = file.Write(vertexes.SelectMany(v => new[] { v.UV.X, v.UV.Y }).ToArray().AsSpan());
        json["color"] = file.Write(vertexes.SelectMany(v => ToBytes(v.Color)).ToArray().AsSpan());
        json["joints"] = file.Write(vertexes.SelectMany(v => new[] { (Byte)v.JointInfo.JointIndex1, (Byte)v.JointInfo.JointIndex2, (Byte)v.JointInfo.JointIndex3 }).ToArray().AsSpan());
        json["weights"] = file.Write(vertexes.SelectMany(v => new[]
        {
            v.JointInfo.Weight1, v.JointInfo.GetJointConnectionsAmount() > 1 ? v.JointInfo.Weight2 : 0, v.JointInfo.GetJointConnectionsAmount() > 2 ? v.JointInfo.Weight3 : 0
        }).ToArray().AsSpan());
        if (part.ShapeOffsets.Count > 0)
        {
            json["shapes"] = new JsonArray(part.ShapeOffsets.Select(shape => (JsonNode)file.Write(shape.SelectMany(o => new[] { o.X, o.Y, o.Z }).ToArray().AsSpan())).ToArray());
        }

        if (part.Compression != null)
        {
            json["compression"] = WriteCompression(part.Compression);
        }

        return json;
    }

    private static JsonObject StartPart(TlmFile file, ModelPart part, Int32 material)
    {
        var json = new JsonObject
        {
            ["material"] = material,
            ["vertices"] = part.Vertexes.Count,
            ["faces"] = file.Write(part.Faces.SelectMany(f => f.Indexes!.Select(i => (UInt32)i)).ToArray().AsSpan())
        };
        if (part.Layout != null)
        {
            json["strips"] = WriteLayout(file, part.Layout);
        }

        return json;
    }

    public static ModelPart ReadRigidPart(TlmFile file, JsonObject json, Matrix4x4? transform = null)
    {
        var part = new ModelPart();
        var count = json.GetInt("vertices");
        var positions = Vectors3(file.Read<Single>(json["position"]), count);
        var normals = json["normal"] != null ? Vectors3(file.Read<Single>(json["normal"]), count) : null;
        if (transform != null && !TlmNodes.IsIdentity(transform.Value))
        {
            var matrix = transform.Value;
            positions = positions.Select(p => NVector3.Transform(p, matrix)).ToArray();
            if (normals != null && Matrix4x4.Invert(matrix, out var inverse))
            {
                var normalMatrix = Matrix4x4.Transpose(inverse);
                normals = normals.Select(n => NVector3.Normalize(NVector3.TransformNormal(n, normalMatrix))).ToArray();
            }
        }

        var rawNormals = json["twin_normal"] != null ? Vectors3(file.Read<Single>(json["twin_normal"]), count) : null;
        var uvs = ReadUvs(file, json, count);
        var uvQ = file.Read<Single>(json["uv_q"]);
        var colors = file.Read<Byte>(json["color"]);
        var emits = file.Read<Byte>(json["emit_color"]);
        var flags = file.Read<Byte>(json["alpha_flags"]);
        for (var i = 0; i < count; i++)
        {
            var color = colors.Length >= (i + 1) * 4 ? FromBytes(colors, i) : DefaultColor();
            // Vertexes added in Blender have a Q of 0, which nothing is drawn with
            var uv = new Vector4(uvs[i].X, uvs[i].Y, uvQ.Length > i && uvQ[i] != 0 ? uvQ[i] : 1.0f, 0);
            var vertex = new Vertex(new Vector4(positions[i].X, positions[i].Y, positions[i].Z, 0), color, uv);
            vertex.Color.StoresColorWithAlphaBlend = flags.Length >= (i + 1) * 2 && flags[i * 2] != 0;
            vertex.AlphaBlendingBit = vertex.Color.StoresColorWithAlphaBlend;
            if (normals != null)
            {
                vertex.Normal = PickNormal(normals[i], rawNormals?[i]);
            }

            if (emits.Length >= (i + 1) * 4)
            {
                vertex.EmitColor = FromBytes(emits, i);
                vertex.EmitColor.StoresColorWithAlphaBlend = flags.Length >= (i + 1) * 2 && flags[i * 2 + 1] != 0;
            }

            part.Vertexes.Add(vertex);
        }

        ReadFaces(file, json, part);
        MergeRepeatedVertexes(part);
        part.Layout = ReadLayout(file, json, part, StripParts.RigidWinding);
        return part;
    }

    public static ModelPart ReadSkinPart(TlmFile file, JsonObject json)
    {
        var part = new ModelPart();
        var count = json.GetInt("vertices");
        var positions = Vectors3(file.Read<Single>(json["position"]), count);
        var normals = json["normal"] != null ? Vectors3(file.Read<Single>(json["normal"]), count) : null;
        var rawNormals = json["twin_normal"] != null ? Vectors3(file.Read<Single>(json["twin_normal"]), count) : null;
        var uvs = ReadUvs(file, json, count);
        var colors = file.Read<Byte>(json["color"]);
        var rawJoints = file.Read<Byte>(json["joints"]);
        var rawWeights = file.Read<Single>(json["weights"]);
        var groupJoints = file.Read<Int32>(json["group_joints"]);
        var groupWeights = file.Read<Single>(json["group_weights"]);
        for (var i = 0; i < count; i++)
        {
            var normal = normals != null ? PickNormal(normals[i], rawNormals?[i]) : new Vector4(0, 1, 0, 0);
            var raw = RawInfluences(rawJoints, rawWeights, i);
            // Blender's weights are what the add-on read from the vertex groups, the game's ones are used while they still match
            var influences = groupJoints.Length >= (i + 1) * 4 ? UseRawWeights(GroupInfluences(groupJoints, groupWeights, i), raw) : raw;
            var vertex = new Vertex(new Vector4(positions[i].X, positions[i].Y, positions[i].Z, normal.X),
                colors.Length >= (i + 1) * 4 ? FromBytes(colors, i) : DefaultSkinColor(), new Vector4(uvs[i].X, uvs[i].Y, normal.Y, normal.Z))
            {
                JointInfo = ToJointInfo(influences, !ReferenceEquals(influences, raw))
            };
            part.Vertexes.Add(vertex);
        }

        if (json["shapes"] is JsonArray shapes)
        {
            var rawShapes = json["twin_shapes"] as JsonArray;
            for (var shape = 0; shape < shapes.Count; shape++)
            {
                var offsets = Vectors3(file.Read<Single>(shapes[shape]), count);
                // Blender keeps shapes as positions, which moves the offsets by rounding errors
                var raw = rawShapes != null && shape < rawShapes.Count ? Vectors3(file.Read<Single>(rawShapes[shape]), count) : null;
                part.ShapeOffsets.Add(offsets.Select((o, i) => raw != null && NVector3.Distance(o, raw[i]) <= 1e-5f * Math.Max(1.0f, raw[i].Length()) ? raw[i] : o)
                    .Select(o => new Vector4(o.X, o.Y, o.Z, 1.0f)).ToList());
            }
        }

        ReadFaces(file, json, part);
        MergeRepeatedVertexes(part);
        part.Layout = ReadLayout(file, json, part, StripParts.SkinWinding);
        part.Compression = ReadCompression(json["compression"]);
        return part;
    }

    // Blender flips UVs upside down and back, the game's UV while the shown one only moved by rounding errors
    private static NVector2[] ReadUvs(TlmFile file, JsonObject json, Int32 count)
    {
        var uvs = file.Read<Single>(json["uv"]);
        var rawUvs = file.Read<Single>(json["twin_uv"]);
        var result = new NVector2[count];
        for (var i = 0; i < count && i * 2 + 1 < uvs.Length; i++)
        {
            result[i] = new NVector2(uvs[i * 2], uvs[i * 2 + 1]);
            if (i * 2 + 1 >= rawUvs.Length)
            {
                continue;
            }

            var raw = new NVector2(rawUvs[i * 2], rawUvs[i * 2 + 1]);
            if (NVector2.Distance(raw, result[i]) <= 1e-5f * Math.Max(1.0f, raw.Length()))
            {
                result[i] = raw;
            }
        }

        return result;
    }

    private static (Int32 Joint, Single Weight)[] RawInfluences(Byte[] joints, Single[] weights, Int32 index)
    {
        if (joints.Length < (index + 1) * 3 || weights.Length < (index + 1) * 3)
        {
            return [];
        }

        return Enumerable.Range(0, 3).Select(k => ((Int32)joints[index * 3 + k], weights[index * 3 + k])).TakeWhile(influence => influence.Item2 > 0).ToArray();
    }

    private static (Int32 Joint, Single Weight)[] GroupInfluences(Int32[] joints, Single[] weights, Int32 index)
    {
        return Enumerable.Range(0, 4).Select(k => (joints[index * 4 + k], weights.ElementAtOrDefault(index * 4 + k))).Where(influence => influence.Item1 >= 0 && influence.Item2 > 0).ToArray();
    }

    // The game's weights as they were, while Blender's are still the same share of every joint. Blender keeps one weight per joint and
    // by bone, the game can give a vertex a joint twice
    private static (Int32 Joint, Single Weight)[] UseRawWeights((Int32 Joint, Single Weight)[] influences, (Int32 Joint, Single Weight)[] raw)
    {
        if (raw.Length == 0 || influences.Length == 0)
        {
            return influences;
        }

        var rawShares = Shares(raw);
        var shares = Shares(influences);
        if (rawShares.Count != shares.Count)
        {
            return influences;
        }

        foreach (var (joint, share) in rawShares)
        {
            if (!shares.TryGetValue(joint, out var other) || Math.Abs(share - other) > 1e-4f)
            {
                return influences;
            }
        }

        return raw;

        static Dictionary<Int32, Single> Shares((Int32 Joint, Single Weight)[] weights)
        {
            var total = weights.Sum(influence => influence.Weight);
            return weights.GroupBy(influence => influence.Joint).ToDictionary(group => group.Key, group => group.Sum(influence => influence.Weight) / total);
        }
    }

    // The game's joints in the order they were stored. Blender can have more than 3, the 3 strongest are kept. Blender's weights can
    // add up to anything, it scales them to 1 when it deforms the vertex, so they get normalized like the game needs them
    private static VertexJointInfo ToJointInfo((Int32 Joint, Single Weight)[] influences, Boolean normalize)
    {
        if (influences.Length == 0)
        {
            return new VertexJointInfo { Weight1 = 1.0f, WeightsAmount = 1 };
        }

        var kept = influences.Length > 3 ? influences.OrderByDescending(i => i.Weight).Take(3).ToArray() : influences;
        var total = kept.Sum(i => i.Weight);
        if (normalize && total > 0)
        {
            kept = kept.Select(i => (i.Joint, i.Weight / total)).ToArray();
        }

        return new VertexJointInfo
        {
            JointIndex1 = kept[0].Joint,
            Weight1 = kept[0].Weight,
            JointIndex2 = kept.Length > 1 ? kept[1].Joint : 0,
            Weight2 = kept.Length > 1 ? kept[1].Weight : 0,
            JointIndex3 = kept.Length > 2 ? kept[2].Joint : 0,
            Weight3 = kept.Length > 2 ? kept[2].Weight : 0,
            WeightsAmount = kept.Length,
            Connection = true
        };
    }

    // The game's normal while the shown one still points the same way. Rigid models keep flags in the lowest bits of the normal's X,
    // an edited normal keeps them. The game's normals of zero length stay, Blender gives the vertexes of some triangles a normal of its
    // own without anyone editing them
    private static Vector4 PickNormal(NVector3 normal, NVector3? raw)
    {
        if (raw == null)
        {
            return new Vector4(normal.X, normal.Y, normal.Z, 0);
        }

        var rawValue = raw.Value;
        var rawLength = rawValue.Length();
        var unchanged = rawLength < 1e-6f || NVector3.Dot(rawValue / rawLength, normal) > 0.9999f;
        if (unchanged)
        {
            return new Vector4(rawValue.X, rawValue.Y, rawValue.Z, 0);
        }

        var result = new Vector4(normal.X, normal.Y, normal.Z, 0);
        result.SetBinaryX(result.GetBinaryX() & 0xFFFFFF00 | BitConverter.SingleToUInt32Bits(rawValue.X) & 0xFF);
        return result;
    }

    private static void ReadFaces(TlmFile file, JsonObject json, ModelPart part)
    {
        var indexes = file.Read<UInt32>(json["faces"]);
        for (var i = 0; i + 2 < indexes.Length; i += 3)
        {
            part.Faces.Add(new IndexedFace((Int32)indexes[i], (Int32)indexes[i + 1], (Int32)indexes[i + 2]));
        }
    }

    // The game draws some triangles from both sides with the same vertexes, Blender can't have two faces on the same vertexes and
    // drops one. Every face but the first on its vertexes gets copies of them, reading merges the copies back
    private static ModelPart SeparateRepeatedFaces(ModelPart part)
    {
        var used = new HashSet<(Int32, Int32, Int32)>();
        ModelPart? separated = null;
        for (var i = 0; i < part.Faces.Count; i++)
        {
            var indexes = part.Faces[i].Indexes!;
            var sorted = indexes.Order().ToArray();
            if (used.Add((sorted[0], sorted[1], sorted[2])))
            {
                continue;
            }

            separated ??= new ModelPart
            {
                Vertexes = [..part.Vertexes],
                Faces = [..part.Faces],
                Layout = part.Layout,
                Compression = part.Compression,
                ShapeOffsets = part.ShapeOffsets.Select(shape => shape.ToList()).ToList()
            };
            var copies = indexes.Select(index =>
            {
                separated.Vertexes.Add(part.Vertexes[index]);
                for (var shape = 0; shape < part.ShapeOffsets.Count; shape++)
                {
                    separated.ShapeOffsets[shape].Add(part.ShapeOffsets[shape][index]);
                }

                return separated.Vertexes.Count - 1;
            }).ToArray();
            separated.Faces[i] = new IndexedFace(copies[0], copies[1], copies[2]);
        }

        return separated ?? part;
    }

    private static void MergeRepeatedVertexes(ModelPart part)
    {
        var firstIndexes = new Dictionary<Int32, Int32>(new VertexComparer(part));
        var remap = new Int32[part.Vertexes.Count];
        var kept = new List<Int32>();
        for (var i = 0; i < part.Vertexes.Count; i++)
        {
            if (!firstIndexes.TryGetValue(i, out var index))
            {
                index = kept.Count;
                firstIndexes.Add(i, index);
                kept.Add(i);
            }

            remap[i] = index;
        }

        if (kept.Count == part.Vertexes.Count)
        {
            return;
        }

        part.Vertexes = kept.Select(i => part.Vertexes[i]).ToList();
        part.ShapeOffsets = part.ShapeOffsets.Select(shape => kept.Select(i => shape[i]).ToList()).ToList();
        part.Faces = part.Faces.Select(face => new IndexedFace(remap[face.Indexes![0]], remap[face.Indexes[1]], remap[face.Indexes[2]])).ToList();
    }

    // Vertexes of a part that are the same in everything the game stores, shape offsets included
    private sealed class VertexComparer(ModelPart part) : IEqualityComparer<Int32>
    {
        public Boolean Equals(Int32 x, Int32 y)
        {
            return Vertex.ExactComparer.Equals(part.Vertexes[x], part.Vertexes[y]) && part.ShapeOffsets.All(shape =>
                BitConverter.SingleToInt32Bits(shape[x].X) == BitConverter.SingleToInt32Bits(shape[y].X) &&
                BitConverter.SingleToInt32Bits(shape[x].Y) == BitConverter.SingleToInt32Bits(shape[y].Y) &&
                BitConverter.SingleToInt32Bits(shape[x].Z) == BitConverter.SingleToInt32Bits(shape[y].Z));
        }

        public Int32 GetHashCode(Int32 index)
        {
            return Vertex.ExactComparer.GetHashCode(part.Vertexes[index]);
        }
    }

    public static JsonObject WriteCompression(TwinSkinCompression compression)
    {
        return new JsonObject
        {
            ["PositionScale"] = compression.PositionScale,
            ["PositionOffset"] = TlmJson.ToJson(compression.PositionOffset),
            ["UvScale"] = compression.UvScale,
            ["UvOffset"] = TlmJson.ToJson(compression.UvOffset)
        };
    }

    public static TwinSkinCompression? ReadCompression(JsonNode? json)
    {
        if (json is not JsonObject compression)
        {
            return null;
        }

        var positionOffset = compression.GetInts("PositionOffset");
        var uvOffset = compression.GetInts("UvOffset");
        return new TwinSkinCompression
        {
            PositionScale = compression.GetFloat("PositionScale", TwinSkinCompression.MinPositionScale),
            PositionOffset = positionOffset.Length == 4 ? positionOffset : new Int32[4],
            UvScale = compression.GetFloat("UvScale", TwinVIFCompiler.SkinUvScale),
            UvOffset = uvOffset.Length == 4 ? uvOffset : new Int32[4]
        };
    }

    private static JsonObject WriteLayout(TlmFile file, StripLayout layout)
    {
        var (vertexes, batchSizes) = layout.ToArrays();
        var result = new JsonObject
        {
            ["vertexes"] = file.Write(vertexes.AsSpan()),
            ["batch_sizes"] = file.Write(batchSizes.AsSpan()),
            ["padding"] = layout.Padding.ToString()
        };
        if (layout.Batches.Any(b => b.BlendShape != null))
        {
            result["blend_shapes"] = file.Write(layout.Batches.SelectMany(b => b.BlendShape == null ? [0f, 0f, 0f] : new[] { b.BlendShape.X, b.BlendShape.Y, b.BlendShape.Z }).ToArray().AsSpan());
        }

        if (layout.IgnoresFacing)
        {
            result["ignores_facing"] = true;
        }

        if (layout.Batches.Any(b => b.Joints != null))
        {
            result["joint_palette_sizes"] = file.Write(layout.Batches.Select(b => b.Joints?.Count ?? 0).ToArray().AsSpan());
            result["joint_palettes"] = file.Write(layout.Batches.SelectMany(b => b.Joints ?? []).ToArray().AsSpan());
        }

        return result;
    }

    private static StripLayout? ReadLayout(TlmFile file, JsonObject part, ModelPart modelPart, StripWinding winding)
    {
        if (part["strips"] is not JsonObject strips)
        {
            return null;
        }

        var layout = StripLayout.FromArrays(file.Read<Int32>(strips["vertexes"]), file.Read<Int32>(strips["batch_sizes"]), strips.GetEnum("padding", TwinVifPadding.QuadWord));
        if (layout == null)
        {
            return null;
        }

        layout.IgnoresFacing = strips.GetBool("ignores_facing");
        if (!layout.Draws(modelPart.Faces, modelPart.Vertexes.Count, winding))
        {
            return null;
        }

        var paletteSizes = file.Read<Int32>(strips["joint_palette_sizes"]);
        var palettes = file.Read<Int32>(strips["joint_palettes"]);
        if (paletteSizes.Length == layout.Batches.Count && paletteSizes.Sum() == palettes.Length)
        {
            var start = 0;
            for (var i = 0; i < layout.Batches.Count; i++)
            {
                layout.Batches[i].Joints = palettes.Skip(start).Take(paletteSizes[i]).ToList();
                start += paletteSizes[i];
            }
        }

        var blendShapes = file.Read<Single>(strips["blend_shapes"]);
        if (blendShapes.Length == layout.Batches.Count * 3)
        {
            for (var i = 0; i < layout.Batches.Count; i++)
            {
                layout.Batches[i].BlendShape = new Twinsanity.TwinsanityInterchange.Common.Vector3(blendShapes[i * 3], blendShapes[i * 3 + 1], blendShapes[i * 3 + 2]);
            }
        }

        return layout;
    }

    private static Vector4 SkinUnitNormal(Vertex vertex)
    {
        var normal = new NVector3(vertex.Position.W, vertex.UV.Z, vertex.UV.W);
        var length = normal.Length();
        return length < 1e-6f ? new Vector4(0, 1, 0, 0) : new Vector4(normal.X / length, normal.Y / length, normal.Z / length, 0);
    }

    // PS2 draws a color of 0x80 at full brightness, that's the color a vertex without one gets
    private static Vector4 DefaultColor()
    {
        return new Vector4(0x7F / 255.0f, 0x7F / 255.0f, 0x7F / 255.0f, 1.0f);
    }

    private static Vector4 DefaultSkinColor()
    {
        return new Vector4(0x7F / 255.0f, 0x7F / 255.0f, 0x7F / 255.0f, 0x77 / 255.0f);
    }

    private static Byte[] ToBytes(Vector4 color)
    {
        return [ToByte(color.X), ToByte(color.Y), ToByte(color.Z), ToByte(color.W)];
    }

    private static Byte ToByte(Single value)
    {
        return (Byte)Math.Clamp(Math.Round(value * 255.0f), 0, 255);
    }

    private static Vector4 FromBytes(Byte[] bytes, Int32 index)
    {
        return new Vector4(bytes[index * 4] / 255.0f, bytes[index * 4 + 1] / 255.0f, bytes[index * 4 + 2] / 255.0f, bytes[index * 4 + 3] / 255.0f);
    }

    private static Single[] Floats(Vector4 vector)
    {
        return [vector.X, vector.Y, vector.Z];
    }

    private static NVector3[] Vectors3(Single[] values, Int32 count)
    {
        var result = new NVector3[count];
        for (var i = 0; i < count && i * 3 + 2 < values.Length; i++)
        {
            result[i] = new NVector3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
        }

        return result;
    }
}
