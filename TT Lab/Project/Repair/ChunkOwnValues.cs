using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TT_Lab.Assets;

namespace TT_Lab.Project.Repair;

/// <summary>
/// Edits of a chunk's own values the way TT Lab keeps them in the chunk's file: an entry per asset in the order of their URIs, the values by
/// their paths in the asset's document in ordinal order, no entry without values
/// </summary>
internal static class ChunkOwnValues
{
    public static JObject EntryFor(JObject chunk, string asset)
    {
        if (chunk["Overrides"] is not JArray overrides)
        {
            chunk["Overrides"] = overrides = new JArray();
        }

        var existing = overrides.OfType<JObject>().FirstOrDefault(entry => RepairFiles.UriOf(entry["Asset"]) == asset);
        if (existing != null)
        {
            return existing;
        }

        var created = new JObject { ["Asset"] = new JObject { ["_uri"] = asset }, ["Values"] = new JObject() };
        var before = overrides.OfType<JObject>().FirstOrDefault(entry => string.CompareOrdinal(RepairFiles.UriOf(entry["Asset"]), asset) > 0);
        if (before != null)
        {
            before.AddBeforeSelf(created);
        }
        else
        {
            overrides.Add(created);
        }

        return created;
    }

    public static void Set(JObject entry, string path, JToken value)
    {
        var values = (JObject)entry["Values"]!;
        values[path] = value;
        Sort(values);
    }

    // The entry goes once it has no values left
    public static void Remove(JObject entry, string path)
    {
        var values = (JObject)entry["Values"]!;
        values.Remove(path);
        if (values.Count == 0)
        {
            entry.Remove();
        }
    }

    // Puts the values of what's at the property paths in place of the ones the entry has there, the entry going without values
    public static void Replace(JObject entry, Func<string, bool> isOf, IReadOnlyDictionary<string, JToken> values)
    {
        var current = (JObject)entry["Values"]!;
        foreach (var property in current.Properties().Where(property => isOf(property.Name)).ToList())
        {
            property.Remove();
        }

        foreach (var (path, value) in values)
        {
            current[path] = value;
        }

        Sort(current);
        if (current.Count == 0)
        {
            entry.Remove();
        }
    }

    private static void Sort(JObject values)
    {
        var sorted = values.Properties().OrderBy(property => property.Name, StringComparer.Ordinal).ToList();
        values.RemoveAll();
        foreach (var property in sorted)
        {
            values.Add(property);
        }
    }

    // The own value at the path, at a path above it (a list of another length is a value as a whole) or under it
    public static JProperty? Covering(JObject values, string path)
    {
        return values.Properties().FirstOrDefault(property => property.Name == path || IsUnder(path, property.Name) || IsUnder(property.Name, path));
    }

    public static bool IsUnder(string path, string above) => path.Length > above.Length && path.StartsWith(above, StringComparison.Ordinal) && path[above.Length] is '.' or '[';

    // The asset's document with the chunk's own values in it, the way a chunk's view of the asset has them (AssetOverrides.Apply)
    public static JObject Apply(JObject document, JObject? values)
    {
        var result = (JObject)document.DeepClone();
        if (values == null)
        {
            return result;
        }

        AssetOverrides.Apply(result, values.Properties().Select(property => new KeyValuePair<string, JToken>(property.Name, property.Value)));
        return result;
    }
}
