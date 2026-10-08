using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using TT_Lab.Assets;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

/// <summary>
/// A property's values and everything under it as the JSON text the clipboard holds: every value the graph has under it, hidden, read-only
/// and worked out ones too, links as their URIs (the linked asset's values are its own), numbers exactly (floats that aren't finite by
/// their bits). A whole asset's are its values but its alias
/// </summary>
public sealed class CopiedValues
{
    private const string Marker = "TT Lab values";
    private const int Version = 1;
    internal const string TypeKey = "$type";
    private const string BitsPrefix = "bits:0x";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// What got copied, the way the history names it
    /// </summary>
    public string From { get; }

    /// <summary>
    /// The copied value's type, a whole asset's the asset's
    /// </summary>
    public Type Type { get; }

    public bool IsAsset { get; }

    public JsonElement Values { get; }

    private CopiedValues(string from, Type type, bool isAsset, JsonElement values)
    {
        From = from;
        Type = type;
        IsAsset = isAsset;
        Values = values;
    }

    public static string Of(PropertyNode node)
    {
        return Write(UndoHistory.NameOf(node), node.GetValue()?.GetType() ?? node.PropertyType, false, writer => WriteNode(writer, node, node.PropertyType));
    }

    /// <summary>
    /// A whole asset's values, its node being the document's root or a link's data
    /// </summary>
    public static string OfAsset(PropertyNode assetNode)
    {
        var asset = (IAsset)assetNode.Target;
        return Write(asset.Alias, asset.GetType(), true, writer =>
        {
            writer.WriteStartObject();
            foreach (var child in assetNode.Children.Where(child => child.Name != nameof(SerializableAsset.Alias) && IsValue(child)))
            {
                writer.WritePropertyName(child.Name);
                WriteNode(writer, child, child.PropertyType);
            }

            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// The values the text holds, none when it's no copied values (text copied anywhere else replaced them)
    /// </summary>
    public static CopiedValues? Read(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{'))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(Marker, out var version) || version.ValueKind != JsonValueKind.Number || version.GetInt32() != Version
                || !root.TryGetProperty("Type", out var typeName) || Type.GetType(typeName.GetString() ?? string.Empty) is not { } type
                || !root.TryGetProperty("Values", out var values))
            {
                return null;
            }

            var from = root.TryGetProperty("From", out var fromElement) ? fromElement.GetString() ?? string.Empty : string.Empty;
            var isAsset = root.TryGetProperty("Asset", out var asset) && asset.ValueKind == JsonValueKind.True;
            return new CopiedValues(from, type, isAsset, values.Clone());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException or FileLoadException or TypeLoadException)
        {
            return null;
        }
    }

    internal static string TypeName(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

    /// <summary>
    /// Whether a node under a value is one of its values: a custom editor's node is the value itself, a link's data is another asset's
    /// </summary>
    internal static bool IsValue(PropertyNode child)
    {
        return child.Segment is not ("[custom_editor]" or "[data]");
    }

    internal static bool IsLeafType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return PropertyGraphBuilder.IsLeaf(type);
    }

    internal static bool IsListType(Type type) => type != typeof(string) && PropertyGraphBuilder.IsIndexableCollection(type);

    internal static Type ElementTypeOf(Type listType)
    {
        return listType.GetElementType() ?? listType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
    }

    // A value its getter makes anew every time, its parts set by setting the whole value
    internal static bool IsComputed(PropertyNode node) => node.Metadata?.Editable?.IsComputed == true;

    // Values the graph has no nodes of the parts of (the scenery's collision, a picture's pixels), the inspector doesn't edit them either
    internal static bool IsOpaque(PropertyNode node, object value)
    {
        return !IsLeafType(value.GetType()) && value is not LabURI && value is not IList && !node.Children.Any(IsValue);
    }

    private static string Write(string from, Type type, bool isAsset, Action<Utf8JsonWriter> writeValues)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber(Marker, Version);
            writer.WriteString("From", from);
            writer.WriteString("Type", TypeName(type));
            if (isAsset)
            {
                writer.WriteBoolean("Asset", true);
            }

            writer.WritePropertyName("Values");
            writeValues(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteNode(Utf8JsonWriter writer, PropertyNode node, Type declared)
    {
        var value = node.GetValue();
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;
            case LabURI uri:
                writer.WriteStringValue((string)uri);
                return;
            case IList list when IsListType(node.PropertyType) || IsListType(value.GetType()):
                WriteList(writer, node, list);
                return;
        }

        if (IsLeafType(value.GetType()))
        {
            WriteLeaf(writer, value);
            return;
        }

        writer.WriteStartObject();
        if (value.GetType() != (Nullable.GetUnderlyingType(declared) ?? declared))
        {
            writer.WriteString(TypeKey, TypeName(value.GetType()));
        }

        foreach (var child in node.Children.Where(IsValue))
        {
            if (child.GetValue() is { } childValue && IsOpaque(child, childValue))
            {
                continue;
            }

            writer.WritePropertyName(child.Name);
            WriteNode(writer, child, child.PropertyType);
        }

        writer.WriteEndObject();
    }

    private static void WriteList(Utf8JsonWriter writer, PropertyNode node, IList list)
    {
        var elementType = ElementTypeOf(node.GetValue()?.GetType() ?? node.PropertyType);
        writer.WriteStartArray();
        for (var index = 0; index < list.Count; index++)
        {
            if (list[index] == null || node.FindChild($"[{index}]") is not { } element)
            {
                writer.WriteNullValue();
                continue;
            }

            WriteNode(writer, element, elementType);
        }

        writer.WriteEndArray();
    }

    private static void WriteLeaf(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case Enum enumValue:
                if (IsUnsigned(Enum.GetUnderlyingType(enumValue.GetType())))
                {
                    writer.WriteNumberValue(Convert.ToUInt64(enumValue, CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteNumberValue(Convert.ToInt64(enumValue, CultureInfo.InvariantCulture));
                }

                break;
            case bool boolean:
                writer.WriteBooleanValue(boolean);
                break;
            case string text:
                writer.WriteStringValue(text);
                break;
            case char character:
                writer.WriteStringValue(character.ToString());
                break;
            case Single single:
                if (Single.IsFinite(single))
                {
                    writer.WriteNumberValue(single);
                }
                else
                {
                    writer.WriteStringValue($"{BitsPrefix}{BitConverter.SingleToUInt32Bits(single):X8}");
                }

                break;
            case Double number:
                if (Double.IsFinite(number))
                {
                    writer.WriteNumberValue(number);
                }
                else
                {
                    writer.WriteStringValue($"{BitsPrefix}{BitConverter.DoubleToUInt64Bits(number):X16}");
                }

                break;
            case decimal number:
                writer.WriteNumberValue(number);
                break;
            case UInt64 number:
                writer.WriteNumberValue(number);
                break;
            default:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static bool IsUnsigned(Type type) => type == typeof(Byte) || type == typeof(UInt16) || type == typeof(UInt32) || type == typeof(UInt64);

    /// <summary>
    /// The value of a leaf's type the JSON holds, exactly: numbers of the type, floats parsed from their text
    /// </summary>
    internal static bool TryReadLeaf(JsonElement json, Type type, out object? value)
    {
        value = null;
        var underlying = Nullable.GetUnderlyingType(type);
        if (json.ValueKind == JsonValueKind.Null)
        {
            return underlying != null || !type.IsValueType;
        }

        type = underlying ?? type;
        try
        {
            if (type.IsEnum)
            {
                if (json.ValueKind != JsonValueKind.Number)
                {
                    return false;
                }

                var raw = json.GetRawText();
                value = IsUnsigned(Enum.GetUnderlyingType(type))
                    ? Enum.ToObject(type, UInt64.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture))
                    : Enum.ToObject(type, Int64.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture));
                return true;
            }

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    if (json.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    {
                        return false;
                    }

                    value = json.GetBoolean();
                    return true;
                case TypeCode.String:
                    if (json.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }

                    value = json.GetString();
                    return true;
                case TypeCode.Char:
                    if (json.ValueKind != JsonValueKind.String || json.GetString() is not { Length: 1 } character)
                    {
                        return false;
                    }

                    value = character[0];
                    return true;
                case TypeCode.Single:
                    if (json.ValueKind == JsonValueKind.String && json.GetString() is { } singleBits && singleBits.StartsWith(BitsPrefix, StringComparison.Ordinal))
                    {
                        value = BitConverter.UInt32BitsToSingle(UInt32.Parse(singleBits[BitsPrefix.Length..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        return true;
                    }

                    if (json.ValueKind != JsonValueKind.Number)
                    {
                        return false;
                    }

                    value = Single.Parse(json.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture);
                    return true;
                case TypeCode.Double:
                    if (json.ValueKind == JsonValueKind.String && json.GetString() is { } doubleBits && doubleBits.StartsWith(BitsPrefix, StringComparison.Ordinal))
                    {
                        value = BitConverter.UInt64BitsToDouble(UInt64.Parse(doubleBits[BitsPrefix.Length..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        return true;
                    }

                    if (json.ValueKind != JsonValueKind.Number)
                    {
                        return false;
                    }

                    value = Double.Parse(json.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture);
                    return true;
                case TypeCode.Decimal:
                    if (json.ValueKind != JsonValueKind.Number)
                    {
                        return false;
                    }

                    value = json.GetDecimal();
                    return true;
                case TypeCode.SByte or TypeCode.Byte or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64:
                    if (json.ValueKind != JsonValueKind.Number)
                    {
                        return false;
                    }

                    var text = json.GetRawText();
                    value = IsUnsigned(type)
                        ? Convert.ChangeType(UInt64.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture), type, CultureInfo.InvariantCulture)
                        : Convert.ChangeType(Int64.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture), type, CultureInfo.InvariantCulture);
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or InvalidCastException)
        {
            return false;
        }
    }

    internal static bool TryReadLink(JsonElement json, out LabURI uri)
    {
        uri = LabURI.Empty;
        if (json.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (json.ValueKind != JsonValueKind.String || json.GetString() is not { } text)
        {
            return false;
        }

        uri = string.IsNullOrEmpty(text) ? LabURI.Empty : new LabURI(text);
        return true;
    }

    /// <summary>
    /// The type a value of the JSON has: the one it names, else the one where it goes
    /// </summary>
    internal static Type TypeOf(JsonElement json, Type declared)
    {
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(TypeKey, out var name) && Type.GetType(name.GetString() ?? string.Empty) is { } type)
        {
            return type;
        }

        return Nullable.GetUnderlyingType(declared) ?? declared;
    }
}
