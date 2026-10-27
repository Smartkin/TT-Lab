using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using TT_Lab.AssetData;

namespace TT_Lab.Assets;

/// <summary>
/// Works out a chunk's overrides of an asset and makes the asset the way the chunk has it
/// </summary>
/// <remarks>
/// An asset's document is its JSON without what makes it an asset of the project (its URI, names, package and references), with its data
/// under <see cref="DataProperty"/> when the data is kept as JSON the default way. Other data (models, pictures, sounds, code) can't have
/// values of a chunk's own
/// </remarks>
public static class AssetOverrides
{
    public const string DataProperty = "AssetData";

    internal static readonly HashSet<string> IdentityProperties =
        ["Type", "InvariantName", "Raw", "AdditionalPath", "FolderInPackage", "ID", "Alias", "Chunk", "LayoutID", "SkipExport", "URI", "Package", "References", "Variation"];

    public static bool CanOverrideData(Type dataType)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        return dataType.GetMethod("LoadInternal", flags)?.DeclaringType == typeof(AbstractAssetData)
               && dataType.GetMethod("SaveInternal", flags)?.DeclaringType == typeof(AbstractAssetData);
    }

    public static bool IsDataPath(string path) => path == DataProperty || path.StartsWith($"{DataProperty}.") || path.StartsWith($"{DataProperty}[");

    public static JObject GetDocument(IAsset asset, AbstractAssetData? data)
    {
        var document = JObject.FromObject(asset);
        foreach (var property in IdentityProperties)
        {
            document.Remove(property);
        }

        if (data != null && CanOverrideData(data.GetType()))
        {
            var dataDocument = JObject.FromObject(data);
            foreach (var property in GetSharedProperties(data.GetType()))
            {
                dataDocument.Remove(property);
            }

            document[DataProperty] = dataDocument;
        }

        return document;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IReadOnlyList<string>> SharedProperties = new();

    // What chunks don't get values of their own of (NoChunkOverridesAttribute), by its name in the data's JSON
    private static IReadOnlyList<string> GetSharedProperties(Type dataType)
    {
        return SharedProperties.GetOrAdd(dataType, type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.GetCustomAttribute<Attributes.NoChunkOverridesAttribute>() != null)
            .Select(property => property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? property.Name)
            .ToList());
    }

    /// <summary>
    /// The asset's values of what chunks can't have values of their own of that differ from the view's, by their path
    /// </summary>
    public static IEnumerable<KeyValuePair<string, JToken>> DiffShared(AbstractAssetData view, AbstractAssetData asset)
    {
        if (view == asset || !CanOverrideData(asset.GetType()) || GetSharedProperties(asset.GetType()).Count == 0)
        {
            return [];
        }

        var viewDocument = JObject.FromObject(view);
        var assetDocument = JObject.FromObject(asset);
        return GetSharedProperties(asset.GetType())
            .Where(property => assetDocument[property] is { } value && !JToken.DeepEquals(viewDocument[property], value))
            .Select(property => new KeyValuePair<string, JToken>($"{DataProperty}.{property}", assetDocument[property]!))
            .ToList();
    }

    /// <summary>
    /// Sets the values in the view where it has them, keeping the view's objects on the way to them: a document showing them keeps its
    /// nodes. Where the view has nothing on the way, or a value of another type, the value goes there as a whole. The paths it set values
    /// at, a value that couldn't be set has none
    /// </summary>
    public static IReadOnlyList<string> Take(SerializableAsset view, IReadOnlyCollection<KeyValuePair<string, JToken>> values)
    {
        var set = new List<string>();
        var dataValues = values.Where(value => IsDataPath(value.Key)).ToList();
        if (dataValues.Count > 0)
        {
            // Read the way views are made, the values come out of it as they'd be in a new view
            var data = view.GetData();
            var document = new JObject { [DataProperty] = JObject.FromObject(data) };
            Put(document, dataValues);
            var source = (AbstractAssetData)Activator.CreateInstance(data.GetType(), view)!;
            using (var reader = document[DataProperty]!.CreateReader())
            {
                JsonSerializer.Create(new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }).Populate(reader, source);
            }

            set.AddRange(dataValues.Select(value => SetValue(source, data, value.Key, 1)).OfType<string>());
        }

        var metadataValues = values.Where(value => !IsDataPath(value.Key)).ToList();
        if (metadataValues.Count > 0)
        {
            var metadata = JObject.FromObject(view);
            Put(metadata, metadataValues);
            var source = (SerializableAsset)Activator.CreateInstance(view.GetType())!;
            JsonConvert.PopulateObject(metadata.ToString(Formatting.None), source);
            set.AddRange(metadataValues.Select(value => SetValue(source, view, value.Key, 0)).OfType<string>());
        }

        return set.Distinct().ToList();
    }

    /// <summary>
    /// Sets the values in the document like <see cref="Apply"/>, adding what it doesn't have yet in an object it has (a dictionary's new key)
    /// </summary>
    public static void Put(JObject document, IEnumerable<KeyValuePair<string, JToken>> values)
    {
        foreach (var (path, value) in values)
        {
            if (document.SelectToken(path) is { } token && token != document)
            {
                token.Replace(value.DeepClone());
                continue;
            }

            var segments = ParseSegments(path);
            if (segments.Count > 1 && segments[^1].Segment.StartsWith('.') && document.SelectToken(path[..segments[^2].End]) is JObject parent)
            {
                parent[segments[^1].Segment[1..]] = value.DeepClone();
            }
        }
    }

    /// <summary>
    /// A JSON path's segments the way property graph nodes add them to their paths, ".Name" and "[index]"
    /// </summary>
    public static List<string> Segments(string path) => ParseSegments(path).Select(segment => segment.Segment).ToList();

    // Each segment with where it ends in the path
    private static List<(string Segment, int End)> ParseSegments(string path)
    {
        var segments = new List<(string, int)>();
        var position = 0;
        while (position < path.Length)
        {
            switch (path[position])
            {
                case '.':
                    position++;
                    break;
                // A name that isn't an identifier, a dictionary's key
                case '[' when position + 1 < path.Length && path[position + 1] == '\'':
                {
                    var end = path.IndexOf("']", position, StringComparison.Ordinal);
                    segments.Add(($".{path[(position + 2)..end]}", end + 2));
                    position = end + 2;
                    break;
                }
                case '[':
                {
                    var end = path.IndexOf(']', position);
                    segments.Add((path[position..(end + 1)], end + 1));
                    position = end + 1;
                    break;
                }
                default:
                {
                    var end = path.IndexOfAny(['.', '['], position);
                    end = end < 0 ? path.Length : end;
                    segments.Add(($".{path[position..end]}", end));
                    position = end;
                    break;
                }
            }
        }

        return segments;
    }

    private static readonly JsonSerializer ContractSerializer = JsonSerializer.CreateDefault();

    // Goes down the source and the target from the segment on and sets the source's value in the target, the path it set at
    private static string? SetValue(object source, object target, string path, int start)
    {
        var segments = ParseSegments(path);
        for (var i = start; i < segments.Count; i++)
        {
            var segment = segments[i].Segment;
            // A dictionary's key the target doesn't have yet gets the value
            if (!TryGetValue(source, segment, out var sourceValue) || (!TryGetValue(target, segment, out var targetValue) && target is not IDictionary))
            {
                return null;
            }

            var isWhole = i == segments.Count - 1 || sourceValue == null || targetValue == null || sourceValue.GetType() != targetValue.GetType() ||
                          targetValue is string || targetValue.GetType().IsPrimitive || targetValue.GetType().IsEnum || targetValue is LabURI;
            if (isWhole)
            {
                return TrySetValue(target, segment, sourceValue, targetValue) ? path[..segments[i].End] : null;
            }

            source = sourceValue!;
            target = targetValue!;
        }

        return null;
    }

    private static bool TryGetValue(object container, string segment, out object? value)
    {
        value = null;
        if (segment.StartsWith('['))
        {
            if (container is not IList list || !int.TryParse(segment.AsSpan(1, segment.Length - 2), out var index) || index >= list.Count)
            {
                return false;
            }

            value = list[index];
            return true;
        }

        var name = segment[1..];
        if (container is IDictionary dictionary)
        {
            if (!dictionary.Contains(name))
            {
                return false;
            }

            value = dictionary[name];
            return true;
        }

        if (MemberOf(container, name) is not { } member)
        {
            return false;
        }

        value = member.ValueProvider!.GetValue(container);
        return true;
    }

    private static bool TrySetValue(object container, string segment, object? value, object? current)
    {
        if (segment.StartsWith('['))
        {
            if (container is not IList list || !int.TryParse(segment.AsSpan(1, segment.Length - 2), out var index) || index >= list.Count)
            {
                return false;
            }

            list[index] = value;
            return true;
        }

        var name = segment[1..];
        if (container is IDictionary dictionary)
        {
            dictionary[name] = value;
            return true;
        }

        if (MemberOf(container, name) is not { } member)
        {
            return false;
        }

        if (member.Writable)
        {
            member.ValueProvider!.SetValue(container, value);
            return true;
        }

        // A list without a setter keeps its object
        if (current is IList currentList && value is IList valueList && !currentList.IsFixedSize)
        {
            currentList.Clear();
            foreach (var element in valueList)
            {
                currentList.Add(element);
            }

            return true;
        }

        return false;
    }

    private static JsonProperty? MemberOf(object container, string name)
    {
        return ContractSerializer.ContractResolver.ResolveContract(container.GetType()) is JsonObjectContract contract &&
               contract.Properties.GetClosestMatchProperty(name) is { Ignored: false, ValueProvider: not null } member ? member : null;
    }

    /// <summary>
    /// The changed document's values that differ from the base's, by their path. Lists of another length are a value as a whole
    /// </summary>
    public static SortedDictionary<string, JToken> Diff(JObject @base, JObject changed)
    {
        var values = new SortedDictionary<string, JToken>(StringComparer.Ordinal);
        Diff(@base, changed, values);
        return values;
    }

    private static void Diff(JToken? @base, JToken changed, SortedDictionary<string, JToken> values)
    {
        // Links are a value, their URI isn't a property of its own
        if (@base is JObject baseObject && changed is JObject changedObject && !IsLink(changedObject))
        {
            foreach (var (name, value) in changedObject)
            {
                Diff(baseObject[name], value!, values);
            }

            return;
        }

        if (@base is JArray baseArray && changed is JArray changedArray && baseArray.Count == changedArray.Count)
        {
            for (var i = 0; i < changedArray.Count; i++)
            {
                Diff(baseArray[i], changedArray[i], values);
            }

            return;
        }

        if (!JToken.DeepEquals(@base, changed))
        {
            values[changed.Path] = changed.DeepClone();
        }
    }

    private static bool IsLink(JObject token) => token.Count == 1 && token.ContainsKey("_uri");

    /// <summary>
    /// Sets the values in the document, values of what the document doesn't have any more are left out
    /// </summary>
    public static void Apply(JObject document, IEnumerable<KeyValuePair<string, JToken>> values)
    {
        foreach (var (path, value) in values)
        {
            if (document.SelectToken(path) is { } token && token != document)
            {
                token.Replace(value.DeepClone());
            }
        }
    }

    /// <summary>
    /// A copy of the asset with the values, it has the asset's URI and data file and is never saved as itself
    /// </summary>
    /// <remarks>
    /// Data that can't have values of its own is the asset's own, a view edits it for every chunk
    /// </remarks>
    public static SerializableAsset CreateView(IAsset asset, IReadOnlyDictionary<string, JToken> values)
    {
        var metadata = JObject.FromObject(asset);
        Apply(metadata, values.Where(value => !IsDataPath(value.Key)));
        var view = (SerializableAsset)Activator.CreateInstance(asset.GetType())!;
        JsonConvert.PopulateObject(metadata.ToString(Formatting.None), view);
        view.OverriddenAsset = asset;
        view.HashSalt = asset.HashSalt;

        var data = asset.GetData<AbstractAssetData>();
        if (!CanOverrideData(data.GetType()))
        {
            view.SetViewData(data);
            return view;
        }

        var document = new JObject { [DataProperty] = JObject.FromObject(data) };
        Apply(document, values.Where(value => IsDataPath(value.Key)));
        var viewData = (AbstractAssetData)Activator.CreateInstance(data.GetType(), view)!;
        using (var reader = document[DataProperty]!.CreateReader())
        {
            JsonSerializer.Create(new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }).Populate(reader, viewData);
        }

        view.SetViewData(viewData);
        return view;
    }
}
