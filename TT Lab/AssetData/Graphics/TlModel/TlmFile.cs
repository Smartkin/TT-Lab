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

    public void Save(string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        WriteTo(stream);
    }

    public void WriteTo(Stream stream)
    {
        var json = Encoding.UTF8.GetBytes(Json.ToJsonString());
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
