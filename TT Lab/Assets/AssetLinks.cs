using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.Project;

namespace TT_Lab.Assets;

/// <summary>
/// Links are the URIs of what they link to, wherever TT Lab keeps them: assets' JSON (LabURI is {"_uri": ...}), the materials and
/// collision surfaces of model files, prefabs and build profiles. Moving assets gives every file linking to them their new URIs and
/// copying assets gives the copies' files the URIs of the copies, as text: what's around a link stays as it was written
/// </summary>
internal static class AssetLinks
{
    public const string UriPrefix = "res://";
    private static readonly Byte[] TlmMagic = "TLM\0"u8.ToArray();
    private const Int32 TlmHeaderSize = 12;

    // Newtonsoft's escaping for the files it writes, System.Text.Json's for model files
    public static string QuoteNewtonsoft(string value) => JsonConvert.ToString(value);

    public static string QuoteSystemText(string value) => System.Text.Json.JsonSerializer.Serialize(value);

    /// <summary>
    /// Every string of the JSON that's a URI, a string is read whole so a URI inside a longer one isn't a link
    /// </summary>
    public static IEnumerable<string> UrisIn(string json)
    {
        foreach (var (_, _, value) in UriStrings(json))
        {
            yield return value;
        }
    }

    /// <summary>
    /// The JSON with the strings that are URIs the map has replaced, null when it has none
    /// </summary>
    public static string? Remap(string json, IReadOnlyDictionary<string, string> map, Func<string, string> quote)
    {
        StringBuilder? result = null;
        var copied = 0;
        foreach (var (start, end, value) in UriStrings(json))
        {
            if (!map.TryGetValue(value, out var replacement))
            {
                continue;
            }

            result ??= new StringBuilder(json.Length);
            result.Append(json, copied, start - copied).Append(quote(replacement));
            copied = end;
        }

        if (result == null)
        {
            return null;
        }

        result.Append(json, copied, json.Length - copied);
        return result.ToString();
    }

    // The strings starting with the URI prefix: where their opening quote is, past their closing one, and their text
    private static IEnumerable<(int Start, int End, string Value)> UriStrings(string json)
    {
        var position = 0;
        while (position < json.Length)
        {
            if (json[position] != '"')
            {
                position++;
                continue;
            }

            var start = position++;
            var escaped = false;
            while (position < json.Length && json[position] != '"')
            {
                if (json[position] == '\\')
                {
                    escaped = true;
                    position++;
                }

                position++;
            }

            if (position >= json.Length)
            {
                yield break;
            }

            var end = ++position;
            var length = end - start - 2;
            if (length < UriPrefix.Length || string.CompareOrdinal(json, start + 1, UriPrefix, 0, UriPrefix.Length) != 0)
            {
                continue;
            }

            var value = escaped ? Unescape(json.Substring(start, end - start)) : json.Substring(start + 1, length);
            if (value != null)
            {
                yield return (start, end, value);
            }
        }
    }

    private static string? Unescape(string quoted)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string>(quoted);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// A text file's text and whether it starts with a byte order mark, which writing it back keeps
    /// </summary>
    public static (string Text, bool Bom) ReadText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return (Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0)), bom);
    }

    public static void WriteText(string path, string text, bool bom)
    {
        File.WriteAllText(path, text, new UTF8Encoding(bom));
        AssetFileStamps.Record(path);
    }

    /// <summary>
    /// Gives a JSON file the URIs the map has, written to the destination (its own place when there's none). Whether anything changed
    /// </summary>
    public static bool RemapJsonFile(string path, IReadOnlyDictionary<string, string> map, string? destination = null)
    {
        var (text, bom) = ReadText(path);
        var remapped = Remap(text, map, QuoteNewtonsoft);
        if (remapped == null)
        {
            if (destination != null)
            {
                File.Copy(path, destination);
            }

            return false;
        }

        WriteText(destination ?? path, remapped, bom);
        return true;
    }

    /// <summary>
    /// The URIs a TT Lab model file's JSON has, read without its binary data
    /// </summary>
    public static List<string> UrisInModelFile(string path)
    {
        return ReadModelJson(path) is { } json ? UrisIn(json).ToList() : [];
    }

    private static string? ReadModelJson(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        var header = new Byte[TlmHeaderSize];
        if (stream.Read(header, 0, TlmHeaderSize) != TlmHeaderSize || !header.AsSpan(0, 4).SequenceEqual(TlmMagic))
        {
            return null;
        }

        var length = BitConverter.ToInt32(header, 8);
        var json = new Byte[length];
        return stream.Read(json, 0, length) == length ? Encoding.UTF8.GetString(json) : null;
    }

    /// <summary>
    /// Gives a TT Lab model file's JSON the URIs the map has, its binary data stays as it is. Written to the destination (its own place
    /// when there's none), whether anything changed
    /// </summary>
    public static bool RemapModelFile(string path, IReadOnlyDictionary<string, string> map, string? destination = null)
    {
        var bytes = File.ReadAllBytes(path);
        string? remapped = null;
        var length = bytes.Length >= TlmHeaderSize && bytes.AsSpan(0, 4).SequenceEqual(TlmMagic) ? BitConverter.ToInt32(bytes, 8) : -1;
        if (length >= 0 && TlmHeaderSize + length <= bytes.Length)
        {
            remapped = Remap(Encoding.UTF8.GetString(bytes, TlmHeaderSize, length), map, QuoteSystemText);
        }

        if (remapped == null)
        {
            if (destination != null)
            {
                File.Copy(path, destination);
            }

            return false;
        }

        // The JSON is padded to 4 bytes with spaces, the binary data after it is written as it was
        var json = Encoding.UTF8.GetBytes(remapped.TrimEnd(' '));
        var padding = -json.Length & 3;
        using (var stream = new FileStream(destination ?? path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(bytes, 0, 8);
            writer.Write(json.Length + padding);
            writer.Write(json);
            for (var i = 0; i < padding; i++)
            {
                writer.Write((Byte)' ');
            }

            writer.Write(bytes, TlmHeaderSize + length, bytes.Length - TlmHeaderSize - length);
        }

        AssetFileStamps.Record(destination ?? path);
        return true;
    }

    public static bool IsModelFile(string path) => path.EndsWith(".tlm", StringComparison.OrdinalIgnoreCase);

    public static bool IsJsonFile(string path) => path.EndsWith(".data", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The links an asset keeps in itself rather than its data: what it references, a chunk's resources, versions, sky and own values, a
    /// folder's children, a package's dependencies
    /// </summary>
    public static IEnumerable<LabURI> MetadataLinks(IAsset asset)
    {
        foreach (var reference in asset.References)
        {
            yield return reference;
        }

        switch (asset)
        {
            case LevelChunk chunk:
                foreach (var uri in chunk.ChunkResources.Concat(chunk.ItemVersions).Append(chunk.Skydome))
                {
                    yield return uri;
                }

                foreach (var @override in chunk.Overrides)
                {
                    yield return @override.Asset;
                    foreach (var uri in @override.Values.Values.SelectMany(UrisIn))
                    {
                        yield return new LabURI(uri);
                    }
                }

                break;
            case Folder folder:
                foreach (var uri in folder.Children.Append(folder.Parent))
                {
                    yield return uri;
                }

                break;
            case Package package:
                foreach (var uri in package.Dependencies)
                {
                    yield return uri;
                }

                break;
        }
    }

    private static IEnumerable<string> UrisIn(JToken token)
    {
        return StringsIn(token).Select(value => (string)value!).Where(text => text.StartsWith(UriPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gives the links the asset keeps in itself the URIs the map has, whether any changed
    /// </summary>
    public static bool RemapMetadata(IAsset asset, IReadOnlyDictionary<LabURI, LabURI> map)
    {
        var changed = RemapList(asset.References, map);
        switch (asset)
        {
            case LevelChunk chunk:
                changed |= RemapList(chunk.ChunkResources, map);
                changed |= RemapList(chunk.ItemVersions, map);
                if (map.TryGetValue(chunk.Skydome, out var skydome))
                {
                    chunk.Skydome = skydome;
                    changed = true;
                }

                foreach (var @override in chunk.Overrides)
                {
                    if (map.TryGetValue(@override.Asset, out var overridden))
                    {
                        @override.Asset = overridden;
                        changed = true;
                    }

                    foreach (var value in @override.Values.Values)
                    {
                        changed |= RemapToken(value, map);
                    }
                }

                break;
            case Folder folder:
                changed |= RemapList(folder.Children, map);
                if (map.TryGetValue(folder.Parent, out var parent))
                {
                    folder.Parent = parent;
                    changed = true;
                }

                break;
            case Package package:
                changed |= RemapList(package.Dependencies, map);
                break;
        }

        return changed;
    }

    private static IEnumerable<JValue> StringsIn(JToken token)
    {
        var tokens = token is JContainer container ? container.DescendantsAndSelf() : [token];
        return tokens.OfType<JValue>().Where(value => value.Type == JTokenType.String);
    }

    private static bool RemapList(List<LabURI> uris, IReadOnlyDictionary<LabURI, LabURI> map)
    {
        var changed = false;
        for (var i = 0; i < uris.Count; i++)
        {
            if (map.TryGetValue(uris[i], out var replacement))
            {
                uris[i] = replacement;
                changed = true;
            }
        }

        return changed;
    }

    private static bool RemapToken(JToken token, IReadOnlyDictionary<LabURI, LabURI> map)
    {
        var changed = false;
        foreach (var value in StringsIn(token).ToList())
        {
            var text = (string)value!;
            if (text.StartsWith(UriPrefix, StringComparison.Ordinal) && map.TryGetValue(new LabURI(text), out var replacement))
            {
                value.Value = replacement.ToString();
                changed = true;
            }
        }

        return changed;
    }
}
