using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace TT_Lab.AssetData.Global;

/// <summary>
/// A save icon as a TT Lab model file: a <c>save_icon</c> node with the icon's mesh (its corners merged into vertexes where they're the
/// same in everything, the shapes after the first as offsets), its texture as the file's one embedded material, its animation (every
/// shape's keys of a time and a weight) and the icon as the game has it. The icon's Y goes down, the file's up like every model's: it's
/// turned half a turn about X. Reading takes the game's icon back while the file still has everything of it
/// </summary>
public static class SaveIconTlm
{
    public const string AssetType = "SaveIcon";
    public const string Kind = "save_icon";
    public const string AnimationKey = "animation";
    public const string ExactKey = "exact";
    private const Single Scale = 4096.0f;

    public static TlmFile Write(string name, PS2SaveIcon icon)
    {
        var file = new TlmFile(AssetType, name);
        var pixels = icon.Texture.Select(texel =>
        {
            var (r, g, b, a) = PS2SaveIcon.ToRgba(texel);
            return (UInt32)(a << 24 | r << 16 | g << 8 | b);
        }).ToArray();
        file.Materials.Add(new JsonObject
        {
            ["name"] = $"{name} texture",
            ["image"] = new JsonObject { ["png"] = file.Write(TextureData.EncodePng(pixels, PS2SaveIcon.TextureSize, PS2SaveIcon.TextureSize).AsSpan()), ["name"] = $"{name}.png" }
        });

        // Corners the same in everything are one vertex
        var vertexes = new List<SaveIconVertex>();
        var indexes = new Dictionary<string, Int32>();
        var faces = new UInt32[icon.Vertexes.Count];
        for (var i = 0; i < icon.Vertexes.Count; i++)
        {
            var corner = icon.Vertexes[i];
            var key = string.Join(',', corner.Positions.Concat(corner.Normal).Concat([corner.U, corner.V])) + ',' + corner.Color;
            if (!indexes.TryGetValue(key, out var index))
            {
                index = vertexes.Count;
                indexes[key] = index;
                vertexes.Add(corner);
            }

            faces[i] = (UInt32)index;
        }

        var part = new JsonObject
        {
            ["material"] = 0,
            ["vertices"] = vertexes.Count,
            ["faces"] = file.Write(faces.AsSpan()),
            ["position"] = file.Write(vertexes.SelectMany(v => Floats(ToTlm(Position(v, 0)))).ToArray().AsSpan()),
            ["normal"] = file.Write(vertexes.SelectMany(v => Floats(Unit(ToTlm(Normal(v))))).ToArray().AsSpan()),
            ["twin_normal"] = file.Write(vertexes.SelectMany(v => Floats(ToTlm(Normal(v)))).ToArray().AsSpan()),
            ["uv"] = file.Write(vertexes.SelectMany(v => new[] { v.U / Scale, v.V / Scale }).ToArray().AsSpan()),
            ["color"] = file.Write(vertexes.SelectMany(v => BitConverter.GetBytes(v.Color)).ToArray().AsSpan())
        };
        if (icon.ShapeCount > 1)
        {
            part["shapes"] = new JsonArray(Enumerable.Range(1, icon.ShapeCount - 1)
                .Select(shape => (JsonNode)file.Write(vertexes.SelectMany(v => Floats(ToTlm(Position(v, shape)) - ToTlm(Position(v, 0)))).ToArray().AsSpan())).ToArray());
        }

        var root = TlmNodes.Create(Kind, name, new JsonObject
        {
            ["FileId"] = TlmJson.ToJson(icon.FileId),
            ["TextureType"] = TlmJson.ToJson(icon.TextureType),
            ["HeaderValue"] = TlmJson.ToJson(icon.HeaderValue),
            ["AnimationTag"] = TlmJson.ToJson(icon.AnimationTag),
            ["FrameLength"] = TlmJson.ToJson(icon.FrameLength),
            ["AnimationSpeed"] = icon.AnimationSpeed,
            ["PlayOffset"] = TlmJson.ToJson(icon.PlayOffset)
        });
        root[TlmNodes.MeshKey] = new JsonObject { ["parts"] = new JsonArray(part) };
        root[AnimationKey] = new JsonObject
        {
            ["frames"] = new JsonArray(icon.Frames.Select(frame => (JsonNode)new JsonObject
            {
                ["shape"] = (Int32)frame.Shape,
                ["keys"] = file.Write(frame.Keys.SelectMany(key => new[] { key.Time, key.Value }).ToArray().AsSpan())
            }).ToArray())
        };
        root[ExactKey] = file.Write(ToBytes(icon).AsSpan());
        file.Root = root;
        return file;
    }

    public static PS2SaveIcon Read(TlmFile file)
    {
        var root = file.Root ?? throw new InvalidDataException("The save icon's file has no root");
        var data = root.GetData();
        var icon = new PS2SaveIcon
        {
            FileId = data.GetUInt("FileId", 0x10000),
            TextureType = data.GetUInt("TextureType", 0x7),
            HeaderValue = data.GetUInt("HeaderValue", 0x3F800000),
            AnimationTag = data.GetUInt("AnimationTag", 1),
            FrameLength = data.GetUInt("FrameLength", 1),
            AnimationSpeed = data.GetFloat("AnimationSpeed", 1.0f),
            PlayOffset = data.GetUInt("PlayOffset")
        };
        var parts = (root[TlmNodes.MeshKey]?["parts"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
        icon.ShapeCount = 1 + parts.Select(part => (part["shapes"] as JsonArray)?.Count ?? 0).DefaultIfEmpty(0).Max();
        foreach (var part in parts)
        {
            ReadCorners(file, part, icon);
        }

        var frames = root[AnimationKey]?["frames"] as JsonArray;
        icon.Frames = frames?.OfType<JsonObject>().Select(frame =>
        {
            var keys = file.Read<Single>(frame["keys"]);
            var result = new SaveIconFrame { Shape = (UInt32)Math.Max(0, frame.GetInt("shape")) };
            for (var i = 0; i + 1 < keys.Length; i += 2)
            {
                result.Keys.Add(new SaveIconKey(keys[i], keys[i + 1]));
            }

            return result;
        }).ToList() ?? [new SaveIconFrame { Keys = [new SaveIconKey(0.0f, 1.0f)] }];

        var exactBytes = file.Read<Byte>(root[ExactKey]);
        var exact = exactBytes.Length > 0 ? FromBytes(exactBytes) : null;
        icon.Texture = ReadTexture(file, parts.FirstOrDefault()) ?? exact?.Texture ?? Enumerable.Repeat((UInt16)0xFFFF, PS2SaveIcon.TexelCount).ToArray();
        if (exact == null)
        {
            return icon;
        }

        // The game's icon while nothing of it changed: it has the words of the corners nothing reads and its texture's encoding
        if (IsSameIcon(icon, exact))
        {
            return exact;
        }

        icon.CompressedTexture = exact.CompressedTexture;
        icon.Trailing = exact.Trailing;
        return icon;
    }

    public static PS2SaveIcon FromBytes(Byte[] bytes)
    {
        var icon = new PS2SaveIcon();
        using var reader = new BinaryReader(new MemoryStream(bytes));
        icon.Read(reader, bytes.Length);
        return icon;
    }

    public static Byte[] ToBytes(PS2SaveIcon icon)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.Default, true))
        {
            icon.Write(writer);
        }

        return stream.ToArray();
    }

    private static void ReadCorners(TlmFile file, JsonObject part, PS2SaveIcon icon)
    {
        var count = part.GetInt("vertices");
        var positions = file.Read<Single>(part["position"]);
        var normals = file.Read<Single>(part["normal"]);
        var rawNormals = file.Read<Single>(part["twin_normal"]);
        var uvs = file.Read<Single>(part["uv"]);
        var rawUvs = file.Read<Single>(part["twin_uv"]);
        var colors = file.Read<Byte>(part["color"]);
        var shapes = (part["shapes"] as JsonArray)?.Select(shape => file.Read<Single>(shape)).ToList() ?? [];
        var rawShapes = (part["twin_shapes"] as JsonArray)?.Select(shape => file.Read<Single>(shape)).ToList() ?? [];
        var vertexes = new SaveIconVertex[count];
        for (var i = 0; i < count; i++)
        {
            var position = Vector(positions, i);
            var vertex = new SaveIconVertex { Positions = new Int16[icon.ShapeCount * 4] };
            WriteShorts(FromTlm(position), vertex.Positions, 0);
            for (var shape = 1; shape < icon.ShapeCount; shape++)
            {
                var offset = shape - 1 < shapes.Count ? Vector(shapes[shape - 1], i) : NVector3.Zero;
                // Blender keeps shapes as positions, which moves the offsets by rounding errors
                if (shape - 1 < rawShapes.Count && rawShapes[shape - 1].Length >= (i + 1) * 3)
                {
                    var raw = Vector(rawShapes[shape - 1], i);
                    offset = NVector3.Distance(offset, raw) <= 1e-5f * Math.Max(1.0f, raw.Length()) ? raw : offset;
                }

                WriteShorts(FromTlm(position + offset), vertex.Positions, shape * 4);
            }

            var normal = normals.Length >= (i + 1) * 3 ? Vector(normals, i) : NVector3.UnitY;
            if (rawNormals.Length >= (i + 1) * 3)
            {
                var raw = Vector(rawNormals, i);
                var length = raw.Length();
                normal = length < 1e-6f || NVector3.Dot(raw / length, Unit(normal)) > 0.9999f ? raw : normal;
            }

            WriteShorts(FromTlm(normal), vertex.Normal, 0);
            var uv = uvs.Length >= (i + 1) * 2 ? new NVector2(uvs[i * 2], uvs[i * 2 + 1]) : NVector2.Zero;
            if (rawUvs.Length >= (i + 1) * 2)
            {
                var raw = new NVector2(rawUvs[i * 2], rawUvs[i * 2 + 1]);
                uv = NVector2.Distance(raw, uv) <= 1e-5f * Math.Max(1.0f, raw.Length()) ? raw : uv;
            }

            vertex.U = ToShort(uv.X * Scale);
            vertex.V = ToShort(uv.Y * Scale);
            vertex.Color = colors.Length >= (i + 1) * 4 ? BitConverter.ToUInt32(colors, i * 4) : 0xFF808080;
            vertexes[i] = vertex;
        }

        // Every corner of every triangle is a vertex of the icon
        foreach (var index in file.Read<UInt32>(part["faces"]))
        {
            if (index >= count)
            {
                throw new InvalidDataException($"A face of the save icon is on vertex {index} of {count}");
            }

            var vertex = vertexes[index];
            icon.Vertexes.Add(new SaveIconVertex { Positions = (Int16[])vertex.Positions.Clone(), Normal = (Int16[])vertex.Normal.Clone(), U = vertex.U, V = vertex.V, Color = vertex.Color });
        }
    }

    // The part's material's picture as the icon's 128x128 texels, resized when it's another size
    private static UInt16[]? ReadTexture(TlmFile file, JsonObject? part)
    {
        var materials = file.Materials.OfType<JsonObject>().ToList();
        var index = part?.GetInt("material", -1) ?? -1;
        var material = index >= 0 && index < materials.Count ? materials[index] : materials.FirstOrDefault(entry => entry["image"] != null);
        var png = file.Read<Byte>(material?["image"]?["png"]);
        if (png.Length == 0)
        {
            return null;
        }

        var (pixels, width, height) = TextureData.DecodePng(new MemoryStream(png));
        if (width != PS2SaveIcon.TextureSize || height != PS2SaveIcon.TextureSize)
        {
            pixels = TextureData.Resample(pixels, width, height, PS2SaveIcon.TextureSize, PS2SaveIcon.TextureSize);
        }

        return pixels.Select(argb => PS2SaveIcon.FromRgba((Byte)(argb >> 16), (Byte)(argb >> 8), (Byte)argb, (Byte)(argb >> 24))).ToArray();
    }

    // Everything the file has of the icon, which leaves out the words of the corners nothing reads and the texture's encoding
    private static bool IsSameIcon(PS2SaveIcon icon, PS2SaveIcon exact)
    {
        if (icon.FileId != exact.FileId || icon.TextureType != exact.TextureType || icon.HeaderValue != exact.HeaderValue || icon.AnimationTag != exact.AnimationTag
            || icon.FrameLength != exact.FrameLength || BitConverter.SingleToInt32Bits(icon.AnimationSpeed) != BitConverter.SingleToInt32Bits(exact.AnimationSpeed)
            || icon.PlayOffset != exact.PlayOffset || icon.ShapeCount != exact.ShapeCount || icon.Vertexes.Count != exact.Vertexes.Count
            || icon.Frames.Count != exact.Frames.Count || !icon.Texture.AsSpan().SequenceEqual(exact.Texture))
        {
            return false;
        }

        for (var i = 0; i < icon.Frames.Count; i++)
        {
            if (icon.Frames[i].Shape != exact.Frames[i].Shape || !icon.Frames[i].Keys.SequenceEqual(exact.Frames[i].Keys))
            {
                return false;
            }
        }

        for (var i = 0; i < icon.Vertexes.Count; i++)
        {
            var (a, b) = (icon.Vertexes[i], exact.Vertexes[i]);
            if (a.U != b.U || a.V != b.V || a.Color != b.Color || !SameXyz(a.Normal, b.Normal, 0))
            {
                return false;
            }

            for (var shape = 0; shape < icon.ShapeCount; shape++)
            {
                if (!SameXyz(a.Positions, b.Positions, shape * 4))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool SameXyz(Int16[] a, Int16[] b, Int32 start)
    {
        return a.Length >= start + 3 && b.Length >= start + 3 && a[start] == b[start] && a[start + 1] == b[start + 1] && a[start + 2] == b[start + 2];
    }

    /// <summary>
    /// Where the corner is in the shape, in the file's units with Y up
    /// </summary>
    public static NVector3 PositionOf(SaveIconVertex corner, Int32 shape) => ToTlm(Position(corner, shape));

    public static NVector3 NormalOf(SaveIconVertex corner) => Unit(ToTlm(Normal(corner)));

    public static NVector2 UvOf(SaveIconVertex corner) => new(corner.U / Scale, corner.V / Scale);

    private static NVector3 Position(SaveIconVertex vertex, Int32 shape) => new(vertex.Positions[shape * 4], vertex.Positions[shape * 4 + 1], vertex.Positions[shape * 4 + 2]);

    private static NVector3 Normal(SaveIconVertex vertex) => new(vertex.Normal[0], vertex.Normal[1], vertex.Normal[2]);

    // The icon's 4096ths with Y going down into the file's units, half a turn about X
    private static NVector3 ToTlm(NVector3 icon) => new(icon.X / Scale, -icon.Y / Scale, -icon.Z / Scale);

    private static NVector3 FromTlm(NVector3 tlm) => new(tlm.X * Scale, -tlm.Y * Scale, -tlm.Z * Scale);

    private static NVector3 Unit(NVector3 vector)
    {
        var length = vector.Length();
        return length > 1e-6f ? vector / length : NVector3.UnitY;
    }

    private static Single[] Floats(NVector3 vector) => [vector.X, vector.Y, vector.Z];

    private static NVector3 Vector(Single[] values, Int32 index) => values.Length >= (index + 1) * 3 ? new NVector3(values[index * 3], values[index * 3 + 1], values[index * 3 + 2]) : NVector3.Zero;

    private static void WriteShorts(NVector3 value, Int16[] target, Int32 start)
    {
        target[start] = ToShort(value.X);
        target[start + 1] = ToShort(value.Y);
        target[start + 2] = ToShort(value.Z);
    }

    private static Int16 ToShort(Single value) => Single.IsNaN(value) ? (Int16)0 : (Int16)Math.Clamp(MathF.Round(value), Int16.MinValue, Int16.MaxValue);
}
