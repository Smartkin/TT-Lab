using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData;

namespace TT_Lab.Assets;

/// <summary>
/// The views a chunk's document edits the assets it shares with other chunks through, saving makes what they differ in the chunk's
/// overrides of those assets
/// </summary>
/// <remarks>
/// The chunk's own assets (its resources, instances and what's in its folder) are edited as they are. Data that can't have values of a
/// chunk's own (models, pictures, sounds, code) is the asset's, editing it from a chunk edits it for every chunk
/// </remarks>
public sealed class ChunkOverrideSession(LevelChunk chunk)
{
    private static readonly IReadOnlyDictionary<string, JToken> NoValues = new Dictionary<string, JToken>();

    private readonly Dictionary<LabURI, SerializableAsset> _views = new();

    // The assets as they were when their views were made. An asset can change in its own editor while the chunk's document is open, the
    // view keeps the values it had then, which aren't the chunk's own
    private readonly Dictionary<SerializableAsset, JObject> _origins = new(ReferenceEqualityComparer.Instance);

    public LevelChunk Chunk => chunk;

    /// <summary>
    /// Raised when a view's values change compared to its asset, by saving or reverting them
    /// </summary>
    public event Action<SerializableAsset>? ViewChanged;

    public bool IsShared(IAsset asset)
    {
        // Chunks, reached through their links, and instances belong to their chunk, only what goes into the chunk as an item can differ
        if (asset == chunk || asset.IsInternal || asset is not SerializableAsset || asset is LevelChunk or Folder or Package or Instance.SerializableInstance
            || chunk.ChunkResources.Contains(asset.URI))
        {
            return false;
        }

        var chunkPath = Normalize(chunk.AdditionalPath);
        if (string.IsNullOrEmpty(chunkPath))
        {
            return false;
        }

        var assetPath = Normalize(asset.AdditionalPath);
        return Normalize(asset.Chunk) != chunkPath && assetPath != chunkPath && !assetPath.StartsWith($"{chunkPath}/");
    }

    private static string Normalize(string? path) => path?.Replace('\\', '/') ?? string.Empty;

    public SerializableAsset GetView(IAsset asset)
    {
        if (!_views.TryGetValue(asset.URI, out var view))
        {
            view = AssetOverrides.CreateView(asset, chunk.GetOverride(asset.URI)?.Values ?? NoValues);
            _views[asset.URI] = view;
            _origins[view] = AssetOverrides.GetDocument(asset, asset.GetData<AbstractAssetData>());
        }

        return view;
    }

    private readonly Dictionary<SerializableAsset, SortedDictionary<string, JToken>> _ownValues = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// What the view has of its own, by its path in the asset (see <see cref="AssetOverrides"/>): the values the chunk has or changed
    /// that differ from the asset's
    /// </summary>
    public SortedDictionary<string, JToken> GetOwnValues(SerializableAsset view)
    {
        if (_ownValues.TryGetValue(view, out var values))
        {
            return values;
        }

        var asset = view.OverriddenAsset!;
        var assetDocument = AssetOverrides.GetDocument(asset, asset.GetData<AbstractAssetData>());
        var viewDocument = AssetOverrides.GetDocument(view, view.GetData());
        var chunksValues = AssetOverrides.Diff(_origins.GetValueOrDefault(view) ?? assetDocument, viewDocument).Keys.ToList();
        values = new SortedDictionary<string, JToken>(StringComparer.Ordinal);
        foreach (var (path, value) in AssetOverrides.Diff(assetDocument, viewDocument).Where(value => HasValueAt(chunksValues, value.Key)))
        {
            values[path] = value;
        }

        _ownValues[view] = values;
        return values;
    }

    /// <summary>
    /// The view got edited, what it has of its own gets worked out again when it's asked for
    /// </summary>
    public void Invalidate(SerializableAsset view)
    {
        _ownValues.Remove(view);
        ViewChanged?.Invoke(view);
    }

    public void Save(SerializableAsset view)
    {
        var asset = view.OverriddenAsset!;
        var values = GetOwnValues(view);
        chunk.Overrides.RemoveAll(@override => @override.Asset == asset.URI);
        if (values.Count > 0)
        {
            chunk.Overrides.Add(new AssetOverride { Asset = asset.URI, Values = values });
            chunk.Overrides.Sort((first, second) => string.CompareOrdinal(first.Asset.ToString(), second.Asset.ToString()));
        }

        if (!AssetOverrides.CanOverrideData(view.GetData().GetType()))
        {
            asset.Save();
        }

        Invalidate(view);
    }

    /// <summary>
    /// The asset's value at the path as the view has it, to put back what the chunk changed there
    /// </summary>
    public object? GetAssetValue(SerializableAsset view, string path, Type type)
    {
        var asset = view.OverriddenAsset!;
        var document = AssetOverrides.GetDocument(asset, asset.GetData<AbstractAssetData>());
        return document.SelectToken(path)?.ToObject(type, JsonSerializer.CreateDefault());
    }

    /// <summary>
    /// Makes the view's value at the path the asset's, for every chunk that doesn't have one of its own there
    /// </summary>
    public void ApplyToAsset(SerializableAsset view, string path)
    {
        var asset = view.OverriddenAsset!;
        var viewDocument = AssetOverrides.GetDocument(view, view.GetData());
        if (viewDocument.SelectToken(path) is not { } value)
        {
            return;
        }

        var values = new Dictionary<string, JToken> { [path] = value };
        var data = asset.GetData<AbstractAssetData>();
        if (AssetOverrides.IsDataPath(path))
        {
            var document = new JObject { [AssetOverrides.DataProperty] = JObject.FromObject(data) };
            AssetOverrides.Apply(document, values);
            using var reader = document[AssetOverrides.DataProperty]!.CreateReader();
            JsonSerializer.Create(new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace }).Populate(reader, data);
        }
        else
        {
            var metadata = JObject.FromObject(asset);
            AssetOverrides.Apply(metadata, values);
            JsonConvert.PopulateObject(metadata.ToString(Formatting.None), asset, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        }

        asset.Save();
        Invalidate(view);
    }

    public IEnumerable<SerializableAsset> Views => _views.Values;

    public bool HasOwnValues(SerializableAsset view) => GetOwnValues(view).Count > 0;

    public static bool IsWithin(string valuePath, string path)
    {
        return valuePath == path || valuePath.StartsWith($"{path}.") || valuePath.StartsWith($"{path}[") ||
               path.StartsWith($"{valuePath}.") || path.StartsWith($"{valuePath}[");
    }

    public static bool HasValueAt(IEnumerable<string> valuePaths, string path) => valuePaths.Any(valuePath => IsWithin(valuePath, path));
}
