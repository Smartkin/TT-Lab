using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// Reads and writes the values of TT Lab's data in JSON
/// </summary>
/// <remarks>
/// The data ends up in Blender's custom properties, which only hold dictionaries, strings, 32 bit integers, floats, booleans and
/// arrays of numbers, so it sticks to those: lists of objects are dictionaries keyed by their index and unsigned integers are
/// written as the signed ones with the same bits
/// </remarks>
public static class TlmJson
{
    public static string? GetString(this JsonObject json, string key)
    {
        return json[key] is JsonValue value && value.TryGetValue<string>(out var result) ? result : null;
    }

    public static Int32 GetInt(this JsonObject json, string key, Int32 fallback = 0)
    {
        return json[key] is JsonValue value ? ToInt(value, fallback) : fallback;
    }

    public static UInt32 GetUInt(this JsonObject json, string key, UInt32 fallback = 0)
    {
        return json[key] is JsonValue value ? unchecked((UInt32)ToLong(value, fallback)) : fallback;
    }

    public static Single GetFloat(this JsonObject json, string key, Single fallback = 0)
    {
        if (json[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<Single>(out var single))
        {
            return single;
        }

        if (TryGetNumber(value, out var number))
        {
            return (Single)number;
        }

        return value.TryGetValue<string>(out var text) && Single.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
    }

    public static Boolean GetBool(this JsonObject json, string key, Boolean fallback = false)
    {
        if (json[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<Boolean>(out var flag))
        {
            return flag;
        }

        // Blender can write booleans as integers
        return TryGetNumber(value, out var number) ? number != 0 : fallback;
    }

    public static TEnum GetEnum<TEnum>(this JsonObject json, string key, TEnum fallback) where TEnum : struct, Enum
    {
        if (json[key] is not JsonValue value)
        {
            return fallback;
        }

        if (value.TryGetValue<string>(out var text) && Enum.TryParse<TEnum>(text, true, out var parsed))
        {
            return parsed;
        }

        return TryGetNumber(value, out var number) ? (TEnum)Enum.ToObject(typeof(TEnum), (Int64)number) : fallback;
    }

    public static Single[] GetFloats(this JsonObject json, string key)
    {
        return json[key] is JsonArray array ? array.Select(item => item is JsonValue value ? value.TryGetValue<Single>(out var single) ? single : TryGetNumber(value, out var number) ? (Single)number : 0 : 0).ToArray() : [];
    }

    public static Int32[] GetInts(this JsonObject json, string key)
    {
        return json[key] is JsonArray array ? array.Select(item => item is JsonValue value ? ToInt(value, 0) : 0).ToArray() : [];
    }

    public static Boolean[] GetBools(this JsonObject json, string key)
    {
        return json[key] is JsonArray array ? array.Select(item => item is JsonValue value && (value.TryGetValue<Boolean>(out var flag) ? flag : TryGetNumber(value, out var number) && number != 0)).ToArray() : [];
    }

    public static Vector4 GetVector4(this JsonObject json, string key, Vector4? fallback = null)
    {
        var values = json.GetFloats(key);
        if (values.Length < 3)
        {
            return fallback ?? new Vector4();
        }

        return new Vector4(values[0], values[1], values[2], values.Length > 3 ? values[3] : 0);
    }

    public static Vector3 GetVector3(this JsonObject json, string key, Vector3? fallback = null)
    {
        var values = json.GetFloats(key);
        return values.Length < 3 ? fallback ?? new Vector3() : new Vector3(values[0], values[1], values[2]);
    }

    public static JsonArray ToJson(Vector4 vector)
    {
        return [vector.X, vector.Y, vector.Z, vector.W];
    }

    public static JsonArray ToJson(Vector3 vector)
    {
        return [vector.X, vector.Y, vector.Z];
    }

    public static JsonArray ToJson(IEnumerable<Single> values)
    {
        return new JsonArray(values.Select(v => (JsonNode)v).ToArray());
    }

    public static JsonArray ToJson(IEnumerable<Int32> values)
    {
        return new JsonArray(values.Select(v => (JsonNode)v).ToArray());
    }

    public static JsonArray ToJson(IEnumerable<Boolean> values)
    {
        return new JsonArray(values.Select(v => (JsonNode)v).ToArray());
    }

    /// <summary>
    /// An unsigned value written as the signed integer with the same bits, which Blender's custom properties can hold
    /// </summary>
    public static JsonNode ToJson(UInt32 value)
    {
        return unchecked((Int32)value);
    }

    /// <summary>
    /// Items of a list that was stored as a dictionary keyed by their index, in index order
    /// </summary>
    public static List<JsonObject> GetIndexed(this JsonObject json, string key)
    {
        if (json[key] is JsonArray array)
        {
            return array.OfType<JsonObject>().ToList();
        }

        if (json[key] is not JsonObject dictionary)
        {
            return [];
        }

        return dictionary.Where(pair => pair.Value is JsonObject)
            .OrderBy(pair => Int32.TryParse(pair.Key, out var index) ? index : Int32.MaxValue)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (JsonObject)pair.Value!)
            .ToList();
    }

    public static JsonObject ToIndexed(IEnumerable<JsonObject> items)
    {
        var result = new JsonObject();
        var index = 0;
        foreach (var item in items)
        {
            result[(index++).ToString()] = item;
        }

        return result;
    }

    // Values parsed from a file convert to any number type, values created in code only to the type they were created from
    private static Boolean TryGetNumber(JsonValue value, out Double number)
    {
        if (value.TryGetValue(out number))
        {
            return true;
        }

        if (value.TryGetValue<Single>(out var single))
        {
            number = single;
        }
        else if (value.TryGetValue<Int64>(out var longValue))
        {
            number = longValue;
        }
        else if (value.TryGetValue<Int32>(out var intValue))
        {
            number = intValue;
        }
        else if (value.TryGetValue<UInt32>(out var uintValue))
        {
            number = uintValue;
        }
        else if (value.TryGetValue<Int16>(out var shortValue))
        {
            number = shortValue;
        }
        else if (value.TryGetValue<UInt16>(out var ushortValue))
        {
            number = ushortValue;
        }
        else if (value.TryGetValue<Byte>(out var byteValue))
        {
            number = byteValue;
        }
        else if (value.TryGetValue<Decimal>(out var decimalValue))
        {
            number = (Double)decimalValue;
        }
        else
        {
            return false;
        }

        return true;
    }

    private static Int32 ToInt(JsonValue value, Int32 fallback)
    {
        return unchecked((Int32)ToLong(value, fallback));
    }

    private static Int64 ToLong(JsonValue value, Int64 fallback)
    {
        if (value.TryGetValue<Int64>(out var integer))
        {
            return integer;
        }

        if (TryGetNumber(value, out var number))
        {
            return (Int64)Math.Round(number);
        }

        if (value.TryGetValue<string>(out var text))
        {
            text = text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && Int64.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out var hex))
            {
                return hex;
            }

            if (Int64.TryParse(text, out var parsed))
            {
                return parsed;
            }
        }

        return fallback;
    }
}
