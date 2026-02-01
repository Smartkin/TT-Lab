using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using SharpGLTF.Materials;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Graphics;

public struct GltfBone
{
    public SharpGLTF.Scenes.NodeBuilder Node;
    public System.Numerics.Matrix4x4 InverseBindMatrix;
    public SharpGLTF.Scenes.NodeBuilder? Parent;
    public int ParentIndex;
}

public struct GltfGeometryWrapper(
    SharpGLTF.Geometry.IMeshBuilder<MaterialBuilder> mesh,
    List<GltfBone> joints,
    bool facesSquashedOnExport = false)
{
    public readonly SharpGLTF.Geometry.IMeshBuilder<MaterialBuilder> Mesh = mesh;
    public readonly List<GltfBone> Joints = joints;
    public readonly bool FacesSquashedOnExport = facesSquashedOnExport;
}

public class GltfMaterialBuilder(List<MaterialBuilder> materialBuilders)
{
    public List<MaterialBuilder> MaterialBuilders { get; } = materialBuilders;
}

public static class GraphicsHelpers
{
    public const string MaterialTokenDivider = "____";
    public const string MeshTokenDivider = "_";
}

public enum MeshExportType
{
    Rigid,
    Skinned,
    BlendSkinned
}

public class MeshExtraInfo
{
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector3Converter))]
    public Vector3 BlendShape { get; init; } = new(0, 0, 0);
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonEnumStringConverter<MeshExportType>))]
    public MeshExportType Type { get; init; } = MeshExportType.Rigid;

    public Boolean HasEmits { get; init; } = false;
}

public class JsonEnumStringConverter<T> : System.Text.Json.Serialization.JsonConverter<T> where T : struct
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var typeString = reader.GetString();
        return typeString == null ? default : Enum.Parse<T>(typeString);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class JsonVector2Converter : System.Text.Json.Serialization.JsonConverter<Vector2>
{
    public override Vector2? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Read();
        
        var newUnkVec = new Vector2();
        newUnkVec.X = reader.GetSingle(); reader.Read();
        newUnkVec.Y = reader.GetSingle(); reader.Read();
        
        return newUnkVec;
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

public class JsonVector3Converter : System.Text.Json.Serialization.JsonConverter<Vector3>
{
    public override Vector3? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Read();
        
        var newUnkVec = new Vector3();
        newUnkVec.X = reader.GetSingle(); reader.Read();
        newUnkVec.Y = reader.GetSingle(); reader.Read();
        newUnkVec.Z = reader.GetSingle(); reader.Read();
        
        return newUnkVec;
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }
}

public class JsonVector4Converter : System.Text.Json.Serialization.JsonConverter<Vector4>
{
    public override Vector4? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Read();
            
        var newUnkVec = new Vector4();
        newUnkVec.X = reader.GetSingle(); reader.Read();
        newUnkVec.Y = reader.GetSingle(); reader.Read();
        newUnkVec.Z = reader.GetSingle(); reader.Read();
        newUnkVec.W = reader.GetSingle(); reader.Read();
        
        return newUnkVec;
    }

    public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteNumberValue(value.W);
        writer.WriteEndArray();
    }
}

public class JsonBoundingBoxConverter : System.Text.Json.Serialization.JsonConverter<BoundingBox>
{
    private readonly JsonVector4Converter _vector4Converter = new();
    
    public override BoundingBox? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Read();
        var column1 = _vector4Converter.Read(ref reader, typeToConvert, options)!; reader.Read();
        var column2 = _vector4Converter.Read(ref reader, typeToConvert, options)!; reader.Read();
        reader.Read();

        return new BoundingBox
        {
            V1 = column1,
            V2 = column2,
        };
    }

    public override void Write(Utf8JsonWriter writer, BoundingBox value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        _vector4Converter.Write(writer, value.V1, options);
        _vector4Converter.Write(writer, value.V2, options);
        writer.WriteEndArray();
    }
}

public class JsonMatrix4Converter : System.Text.Json.Serialization.JsonConverter<Matrix4>
{
    private readonly JsonVector4Converter _vector4Converter = new();
    
    public override Matrix4? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        reader.Read();
        
        var column1 = _vector4Converter.Read(ref reader, typeToConvert, options); reader.Read();
        var column2 = _vector4Converter.Read(ref reader, typeToConvert, options); reader.Read();
        var column3 = _vector4Converter.Read(ref reader, typeToConvert, options); reader.Read();
        var column4 = _vector4Converter.Read(ref reader, typeToConvert, options); reader.Read();
        
        reader.Read();

        return new Matrix4
        {
            Column1 = column1,
            Column2 = column2,
            Column3 = column3,
            Column4 = column4,
        };
    }

    public override void Write(Utf8JsonWriter writer, Matrix4 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        _vector4Converter.Write(writer, value.Column1, options);
        _vector4Converter.Write(writer, value.Column2, options);
        _vector4Converter.Write(writer, value.Column3, options);
        _vector4Converter.Write(writer, value.Column4, options);
        writer.WriteEndArray();
    }
}

internal class UInt16Ref
{
    public UInt16 Value;
}

internal class ByteRef
{
    public Byte Value;
}


internal class JsonUInt16RefConverter : System.Text.Json.Serialization.JsonConverter<UInt16Ref>
{
    public override UInt16Ref? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var readValue = reader.GetUInt16(); reader.Read();
        return new UInt16Ref { Value = readValue };
    }

    public override void Write(Utf8JsonWriter writer, UInt16Ref value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}


internal class JsonByteRefConverter : System.Text.Json.Serialization.JsonConverter<ByteRef>
{
    public override ByteRef? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var readValue = reader.GetByte(); reader.Read();
        return new ByteRef { Value = readValue };
    }

    public override void Write(Utf8JsonWriter writer, ByteRef value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

public class JsonTwinBoundingBoxBuilderConverter : System.Text.Json.Serialization.JsonConverter<TwinBoundingBoxBuilder>
{
    private readonly JsonListConverter<Vector4> vectorConverter = new();
    private readonly JsonListConverter<UInt16Ref> shortsConverter = new();
    private readonly JsonListConverter<ByteRef> bytesConverter = new();
    
    public override TwinBoundingBoxBuilder? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var bbBuilder = new TwinBoundingBoxBuilder();
        bbBuilder.BoundingBoxPoints = vectorConverter.Read(ref reader, typeToConvert, options); reader.Read();
        bbBuilder.UnkVectors1 = vectorConverter.Read(ref reader, typeToConvert, options); reader.Read();
        bbBuilder.UnkVectors2 = vectorConverter.Read(ref reader, typeToConvert, options); reader.Read();
        bbBuilder.UnkVectors3 = vectorConverter.Read(ref reader, typeToConvert, options); reader.Read();
        bbBuilder.UnkShorts = shortsConverter.Read(ref reader, typeToConvert, options)!.Select(sRef => sRef.Value).ToList(); reader.Read();
        bbBuilder.UnkBytes1 = bytesConverter.Read(ref reader, typeToConvert, options)!.Select(sRef => sRef.Value).ToList(); reader.Read();
        bbBuilder.UnkBytes2 = bytesConverter.Read(ref reader, typeToConvert, options)!.Select(sRef => sRef.Value).ToList(); reader.Read();
        
        return bbBuilder;
    }

    public override void Write(Utf8JsonWriter writer, TwinBoundingBoxBuilder value, JsonSerializerOptions options)
    {
        vectorConverter.Write(writer, value.BoundingBoxPoints, options);
        vectorConverter.Write(writer, value.UnkVectors1, options);
        vectorConverter.Write(writer, value.UnkVectors2, options);
        vectorConverter.Write(writer, value.UnkVectors3, options);
        shortsConverter.Write(writer, value.UnkShorts.Select(vShort => new UInt16Ref { Value = vShort }).ToList(), options);
        bytesConverter.Write(writer, value.UnkBytes1.Select(vByte => new ByteRef { Value = vByte }).ToList(), options);
        bytesConverter.Write(writer, value.UnkBytes2.Select(vByte => new ByteRef { Value = vByte }).ToList(), options);
    }
}

public class JsonListConverter<T> : System.Text.Json.Serialization.JsonConverter<List<T>> where T : class
{
    private abstract class InternalConverter
    {
        public abstract object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options);
        public abstract void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options);
    }
    
    private class InternalConverter<TConverterT, TActualT>(TConverterT converter) : InternalConverter
        where TConverterT : System.Text.Json.Serialization.JsonConverter<TActualT>
    {
        public override Object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return converter.Read(ref reader, typeToConvert, options);
        }

        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            converter.Write(writer, (TActualT)value, options);
        }
    }
    
    private static readonly Dictionary<Type, InternalConverter> JsonConverters = new();
    
    static JsonListConverter()
    {
        JsonConverters.Add(typeof(Vector2), new InternalConverter<JsonVector2Converter, Vector2>(new JsonVector2Converter()));
        JsonConverters.Add(typeof(Vector3), new InternalConverter<JsonVector3Converter, Vector3>(new JsonVector3Converter()));
        JsonConverters.Add(typeof(Vector4), new InternalConverter<JsonVector4Converter, Vector4>(new JsonVector4Converter()));
        JsonConverters.Add(typeof(BoundingBox), new InternalConverter<JsonBoundingBoxConverter, BoundingBox>(new JsonBoundingBoxConverter()));
        JsonConverters.Add(typeof(Matrix4), new InternalConverter<JsonMatrix4Converter, Matrix4>(new JsonMatrix4Converter()));
        JsonConverters.Add(typeof(UInt16Ref), new InternalConverter<JsonUInt16RefConverter, UInt16Ref>(new JsonUInt16RefConverter()));
        JsonConverters.Add(typeof(ByteRef), new InternalConverter<JsonByteRefConverter, ByteRef>(new JsonByteRefConverter()));
        JsonConverters.Add(typeof(TwinBoundingBoxBuilder), new InternalConverter<JsonTwinBoundingBoxBuilderConverter, TwinBoundingBoxBuilder>(new JsonTwinBoundingBoxBuilderConverter()));
    }
    
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var enteringDepth = reader.CurrentDepth;
        reader.Read();
        var list = new List<T>();
        var converter = JsonConverters.TryGetValue(typeof(T), out var jsonConverter) ? jsonConverter : null;
        Debug.Assert(converter != null, $"Unsupported list item type {typeof(T).FullName}");
        while (reader.TokenType != JsonTokenType.PropertyName)
        {
            if (reader.TokenType == JsonTokenType.EndArray && enteringDepth == reader.CurrentDepth)
            {
                break;
            }

            if (reader.TokenType == JsonTokenType.EndArray)
            {
                reader.Read();
                
                // Exited out of scope
                if (enteringDepth == reader.CurrentDepth)
                {
                    break;
                }
            }

            var value = (T?)converter.Read(ref reader, typeToConvert, options);
            list.Add(value!);
        }
        
        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        var converter = JsonConverters.TryGetValue(typeof(T), out var jsonConverter) ? jsonConverter : null;
        Debug.Assert(converter != null, $"Unsupported list item type {typeof(T).FullName}");
        foreach (var item in value)
        {
            converter.Write(writer, item, options);
        }
        writer.WriteEndArray();
    }
}

public record GltfMaterialLabUri(int GltfIndex, LabURI MaterialUri);