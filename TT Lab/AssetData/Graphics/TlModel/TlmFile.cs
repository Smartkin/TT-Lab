using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// A TT Lab model file: a JSON tree with its arrays in binary data next to it, see BlenderTools/TLM.md
/// </summary>
public sealed class TlmFile
{
    public const UInt32 FormatVersion = 1;
    private static readonly Byte[] Magic = "TLM\0"u8.ToArray();
    private static readonly Dictionary<Type, string> TypeNames = new()
    {
        [typeof(Single)] = "f32",
        [typeof(Int32)] = "i32",
        [typeof(UInt32)] = "u32",
        [typeof(Int16)] = "i16",
        [typeof(UInt16)] = "u16",
        [typeof(Byte)] = "u8"
    };
    private static readonly Dictionary<string, Int32> TypeSizes = new()
    {
        ["f32"] = 4, ["i32"] = 4, ["u32"] = 4, ["i16"] = 2, ["u16"] = 2, ["u8"] = 1
    };

    private readonly MemoryStream _binary = new();

    public JsonObject Json { get; }

    public TlmFile(string assetType, string name)
    {
        Json = new JsonObject
        {
            ["asset"] = new JsonObject { ["type"] = assetType, ["name"] = name },
            ["materials"] = new JsonArray()
        };
    }

    private TlmFile(JsonObject json, Byte[] binary)
    {
        Json = json;
        _binary.Write(binary);
    }

    public string AssetType => (Json["asset"] as JsonObject)?.GetString("type") ?? string.Empty;

    public JsonObject? Root
    {
        get => Json["root"] as JsonObject;
        set => Json["root"] = value;
    }

    public JsonArray Materials => Json["materials"] as JsonArray ?? (JsonArray)(Json["materials"] = new JsonArray());

    /// <summary>
    /// Puts the values into the binary data and gives the view pointing at them
    /// </summary>
    public JsonObject Write<T>(ReadOnlySpan<T> values) where T : unmanaged
    {
        var padding = (Int32)(-_binary.Length & 3);
        _binary.Write(new Byte[padding]);
        var offset = _binary.Length;
        _binary.Write(MemoryMarshal.AsBytes(values));
        return new JsonObject { ["offset"] = offset, ["count"] = values.Length, ["type"] = TypeNames[typeof(T)] };
    }

    public JsonObject Write<T>(Span<T> values) where T : unmanaged
    {
        return Write((ReadOnlySpan<T>)values);
    }

    public JsonObject Write<T>(IReadOnlyCollection<T> values) where T : unmanaged
    {
        var array = new T[values.Count];
        var i = 0;
        foreach (var value in values)
        {
            array[i++] = value;
        }

        return Write<T>(array.AsSpan());
    }

    /// <summary>
    /// The values a view points at, empty without one
    /// </summary>
    public T[] Read<T>(JsonNode? view) where T : unmanaged
    {
        if (view is not JsonObject json)
        {
            return [];
        }

        var type = json.GetString("type");
        if (type != TypeNames[typeof(T)])
        {
            throw new InvalidDataException($"A view of {type} values was read as {TypeNames[typeof(T)]}");
        }

        var offset = json.GetInt("offset");
        var count = json.GetInt("count");
        var size = Marshal.SizeOf<T>();
        if (offset < 0 || count < 0 || offset + (Int64)count * size > _binary.Length)
        {
            throw new InvalidDataException("A view points past the end of the binary data");
        }

        var result = new T[count];
        _binary.GetBuffer().AsSpan(offset, count * size).CopyTo(MemoryMarshal.AsBytes(result.AsSpan()));
        return result;
    }

    /// <summary>
    /// A copy of a node of another file with everything under it: the data its views point at goes into this file's binary data and the
    /// materials its parts use into this file's materials, the map keeping the ones copied already
    /// </summary>
    public JsonObject CopyNode(TlmFile from, JsonObject node, Dictionary<Int32, Int32> materials)
    {
        return (JsonObject)Copy(from, node, materials)!;
    }

    private JsonNode? Copy(TlmFile from, JsonNode? node, Dictionary<Int32, Int32> materials)
    {
        switch (node)
        {
            case JsonObject view when IsView(view):
                return CopyView(from, view);
            case JsonObject json:
                var copy = new JsonObject();
                foreach (var (key, value) in json)
                {
                    copy[key] = key == "material" && value is JsonValue index && index.TryGetValue<Int32>(out var material) && material >= 0
                        ? CopyMaterial(from, material, materials)
                        : Copy(from, value, materials);
                }

                return copy;
            case JsonArray array:
                var items = new JsonArray();
                foreach (var item in array)
                {
                    items.Add(Copy(from, item, materials));
                }

                return items;
            default:
                return node?.DeepClone();
        }
    }

    private Int32 CopyMaterial(TlmFile from, Int32 material, Dictionary<Int32, Int32> materials)
    {
        if (materials.TryGetValue(material, out var copied))
        {
            return copied;
        }

        // A material the file doesn't have stays without one
        if (material >= from.Materials.Count)
        {
            return -1;
        }

        Materials.Add(Copy(from, from.Materials[material], materials));
        return materials[material] = Materials.Count - 1;
    }

    private static Boolean IsView(JsonObject json)
    {
        return json.Count == 3 && json["offset"] is JsonValue && json["count"] is JsonValue && json["type"] is JsonValue type
               && type.TryGetValue<string>(out var name) && TypeSizes.ContainsKey(name);
    }

    private JsonObject CopyView(TlmFile from, JsonObject view)
    {
        var type = view.GetString("type")!;
        var offset = view.GetInt("offset");
        var count = view.GetInt("count");
        var length = count * TypeSizes[type];
        if (offset < 0 || count < 0 || offset + (Int64)length > from._binary.Length)
        {
            throw new InvalidDataException("A view points past the end of the binary data");
        }

        var padding = (Int32)(-_binary.Length & 3);
        _binary.Write(new Byte[padding]);
        var copied = _binary.Length;
        _binary.Write(from._binary.GetBuffer(), offset, length);
        return new JsonObject { ["offset"] = copied, ["count"] = count, ["type"] = type };
    }

    // The JSON is made before the file is opened: a value JSON can't hold (NaN typed into a scenery's value) left the asset's file empty
    public void Save(string path)
    {
        var json = JsonBytes();
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        WriteTo(stream, json);
    }

    public void WriteTo(Stream stream)
    {
        WriteTo(stream, JsonBytes());
    }

    private Byte[] JsonBytes()
    {
        try
        {
            return Encoding.UTF8.GetBytes(Json.ToJsonString());
        }
        catch (ArgumentException) when (FirstNonFinite(Json) is { } found)
        {
            throw new InvalidDataException($"{found.Where} is {found.Value}, which a model file can't keep");
        }
    }

    private static (string Where, double Value)? FirstNonFinite(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var (_, child) in json)
                {
                    if (FirstNonFinite(child) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            case JsonArray array:
                foreach (var child in array)
                {
                    if (FirstNonFinite(child) is { } found)
                    {
                        return found;
                    }
                }

                return null;
            case JsonValue value when value.TryGetValue<Single>(out var single) && !Single.IsFinite(single):
                return (value.GetPath(), single);
            case JsonValue value when value.TryGetValue<Double>(out var number) && !Double.IsFinite(number):
                return (value.GetPath(), number);
            default:
                return null;
        }
    }

    private void WriteTo(Stream stream, Byte[] json)
    {
        var padding = -json.Length & 3;
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(json.Length + padding);
        writer.Write(json);
        writer.Write(Encoding.ASCII.GetBytes(new string(' ', padding)));
        writer.Write((Int32)_binary.Length);
        writer.Write(_binary.GetBuffer(), 0, (Int32)_binary.Length);
    }

    public static TlmFile Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Read(stream);
    }

    public static TlmFile Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual(Magic))
        {
            throw new InvalidDataException("Not a TT Lab model file");
        }

        var version = reader.ReadUInt32();
        if (version != FormatVersion)
        {
            throw new InvalidDataException($"TT Lab model file of version {version}, this TT Lab reads version {FormatVersion}");
        }

        var json = JsonNode.Parse(reader.ReadBytes(reader.ReadInt32()), documentOptions: new JsonDocumentOptions { MaxDepth = 256 }) as JsonObject
                   ?? throw new InvalidDataException("A TT Lab model file without its JSON");
        var binary = reader.ReadBytes(reader.ReadInt32());
        return new TlmFile(json, binary);
    }
}
