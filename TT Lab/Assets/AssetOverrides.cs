using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

    private static readonly HashSet<string> IdentityProperties =
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
