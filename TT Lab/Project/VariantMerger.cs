using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Graphics;

namespace TT_Lab.Project;

/// <summary>
/// Turns the variants of assets a disc's chunks made into the chunks' overrides of the assets they're variants of
/// </summary>
/// <remarks>
/// An item with the ID of another chunk's item but other content became a variant for each chunk after the first. Most only differ in a
/// few values, a texture in its slot in the tools' memory or an object in its name, and a lot don't differ at all where it matters, like
/// sounds whose records sit elsewhere in their chunk. Those become the chunk's override of the first chunk's asset and what referred to the
/// variant refers to that asset instead. Variants whose data differs where it can't have values of its own (a picture of another size,
/// other code) stay what they are
/// </remarks>
internal sealed class VariantMerger(AssetManager assetManager, string assetsPath)
{
    private const string UriStart = "res://";

    private readonly Dictionary<LabURI, LabURI> _merged = new();

    public int Merge()
    {
        var chunks = assetManager.GetAllAssetsOf<LevelChunk>().Where(chunk => !string.IsNullOrEmpty(chunk.AdditionalPath))
            .GroupBy(chunk => (chunk.Package, Variation: ToVariation(chunk.AdditionalPath!)))
            .ToDictionary(group => group.Key, group => group.First());
        var bases = assetManager.GetAssets().Where(asset => !asset.IsInternal && string.IsNullOrEmpty(asset.Variation))
            .GroupBy(asset => (asset.Type, asset.ID, asset.LayoutID))
            .ToDictionary(group => group.Key, group => group.ToList());
        var variants = assetManager.GetAssets()
            .Where(asset => !asset.IsInternal && !string.IsNullOrEmpty(asset.Variation) && chunks.ContainsKey((asset.Package, asset.Variation)))
            .ToList();
        // A variant can be the version of several chunks, a later chunk with the same content found it by its hash
        var chunksUsing = GetChunksUsing(chunks.Values.Distinct(), variants.Select(variant => variant.URI).ToHashSet());
        var changedChunks = new HashSet<LevelChunk>();
        // What variants refer to has to be merged before them, an object's sound slots hold the chunk's variants of its sounds
        foreach (var variant in variants.OrderBy(MergeOrder))
        {
            // Levels' own versions of the startup chunk's objects are in the level's package, their asset in the global one
            if (!bases.TryGetValue((variant.Type, variant.ID, variant.LayoutID), out var candidates) ||
                (candidates.FirstOrDefault(candidate => candidate.Package == variant.Package) ??
                 candidates.FirstOrDefault(candidate => assetManager.IsRelated(variant.Package, candidate.Package))) is not { } @base)
            {
                continue;
            }

            var values = GetOverrideValues(@base, variant);
            if (values == null)
            {
                continue;
            }

            // Variants no chunk uses went into no chunk, the models of the chunk they were found in are shared and found the asset by its ID
            if (values.Count > 0 && chunksUsing.TryGetValue(variant.URI, out var users))
            {
                foreach (var chunk in users)
                {
                    chunk.Overrides.Add(new AssetOverride { Asset = @base.URI, Values = new SortedDictionary<string, JToken>(values, StringComparer.Ordinal) });
                    changedChunks.Add(chunk);
                }
            }

            _merged[variant.URI] = @base.URI;
        }

        if (_merged.Count == 0)
        {
            return 0;
        }

        foreach (var chunk in changedChunks)
        {
            chunk.Overrides.Sort((first, second) => string.CompareOrdinal(first.Asset.ToString(), second.Asset.ToString()));
        }

        RedirectReferences(changedChunks);
        foreach (var variant in _merged.Keys.Select(assetManager.GetAsset).ToList())
        {
            variant.Delete();
        }

        return _merged.Count;
    }

    // What each chunk has or refers to down its assets' references
    private Dictionary<LabURI, List<LevelChunk>> GetChunksUsing(IEnumerable<LevelChunk> chunks, HashSet<LabURI> variants)
    {
        var users = new Dictionary<LabURI, List<LevelChunk>>();
        foreach (var chunk in chunks)
        {
            var visited = new HashSet<LabURI>();
            var pending = new Stack<LabURI>(chunk.ChunkResources.Concat(chunk.ItemVersions).Append(chunk.Skydome));
            while (pending.TryPop(out var uri))
            {
                if (uri == LabURI.Empty || !visited.Add(uri) || !assetManager.DoesAssetExist(uri))
                {
                    continue;
                }

                if (variants.Contains(uri))
                {
                    if (!users.TryGetValue(uri, out var chunksOfVariant))
                    {
                        chunksOfVariant = [];
                        users[uri] = chunksOfVariant;
                    }

                    chunksOfVariant.Add(chunk);
                }

                // Links to other chunks don't make their content this chunk's
                var asset = assetManager.GetAsset(uri);
                if (asset is LevelChunk or Folder or Package)
                {
                    continue;
                }

                foreach (var reference in asset.References)
                {
                    pending.Push(reference);
                }
            }
        }

        return users;
    }

    private static string ToVariation(string chunkPath) => chunkPath.Replace('\\', '_').Replace('/', '_');

    private static int MergeOrder(IAsset asset) => asset switch
    {
        SoundEffect => 0,
        Texture => 1,
        Material => 2,
        GameObject => 4,
        _ => 3
    };

    /// <summary>
    /// The values the variant has of its own, null when it differs where it can't have values
    /// </summary>
    private SortedDictionary<string, JToken>? GetOverrideValues(IAsset @base, IAsset variant)
    {
        var sameData = HaveSameData(@base, variant);
        AbstractAssetData? baseData = null;
        AbstractAssetData? variantData = null;
        try
        {
            if (!sameData)
            {
                baseData = @base.GetData<AbstractAssetData>();
                variantData = variant.GetData<AbstractAssetData>();
                if (!AssetOverrides.CanOverrideData(baseData.GetType()))
                {
                    return null;
                }
            }

            var baseDocument = AssetOverrides.GetDocument(@base, baseData);
            var variantDocument = AssetOverrides.GetDocument(variant, variantData);
            RedirectLinks(baseDocument);
            RedirectLinks(variantDocument);
            return AssetOverrides.Diff(baseDocument, variantDocument);
        }
        finally
        {
            if (baseData != null)
            {
                @base.UnloadData();
            }

            if (variantData != null)
            {
                variant.UnloadData();
            }
        }
    }

    private static bool HaveSameData(IAsset first, IAsset second)
    {
        var firstInfo = new FileInfo(first.FullDataPath);
        var secondInfo = new FileInfo(second.FullDataPath);
        if (!firstInfo.Exists || !secondInfo.Exists)
        {
            return firstInfo.Exists == secondInfo.Exists;
        }

        return firstInfo.Length == secondInfo.Length && File.ReadAllBytes(firstInfo.FullName).AsSpan().SequenceEqual(File.ReadAllBytes(secondInfo.FullName));
    }

    // Links to variants merged already are links to their asset now
    private void RedirectLinks(JObject document)
    {
        foreach (var link in document.Descendants().OfType<JObject>().Where(token => token["_uri"] is JValue { Type: JTokenType.String }).ToList())
        {
            if (_merged.TryGetValue(new LabURI((string)link["_uri"]!), out var redirected))
            {
                link["_uri"] = redirected.ToString();
            }
        }
    }

    private LabURI Redirect(LabURI uri) => _merged.TryGetValue(uri, out var redirected) ? redirected : uri;

    private void RedirectReferences(HashSet<LevelChunk> changedChunks)
    {
        foreach (var asset in assetManager.GetAssets().Where(asset => !asset.IsInternal && !_merged.ContainsKey(asset.URI)))
        {
            var changed = false;
            if (asset.References.Any(_merged.ContainsKey))
            {
                asset.References = asset.References.Select(Redirect).Distinct().ToList();
                changed = true;
            }

            if (asset is LevelChunk chunk && (chunk.ChunkResources.Any(_merged.ContainsKey) || chunk.ItemVersions.Any(_merged.ContainsKey) || _merged.ContainsKey(chunk.Skydome)))
            {
                chunk.ChunkResources = chunk.ChunkResources.Select(Redirect).Distinct().ToList();
                chunk.ItemVersions = chunk.ItemVersions.Select(Redirect).Distinct().ToList();
                chunk.Skydome = Redirect(chunk.Skydome);
                changed = true;
            }

            if (changed || (asset is LevelChunk changedChunk && changedChunks.Contains(changedChunk)))
            {
                asset.Serialize();
            }
        }

        var quoted = _merged.ToDictionary(pair => $"\"{pair.Key}\"", pair => $"\"{pair.Value}\"");
        // Only the packages of the variants and the ones depending on them or they depend on can refer to them
        var variantPackages = _merged.Keys.Select(uri => assetManager.GetAsset(uri).Package).Distinct().ToList();
        var packageFolders = assetManager.GetAllAssetsOf<Package>()
            .Where(package => variantPackages.Any(variantPackage => assetManager.IsRelated(package.URI, variantPackage)))
            .Select(package => Path.Combine(assetsPath, package.URI.GetPackageName()))
            .Where(Directory.Exists);
        foreach (var file in packageFolders.SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)))
        {
            switch (Path.GetExtension(file))
            {
                case ".data":
                    RedirectInText(file, quoted);
                    break;
                case ".tlm":
                    RedirectInModel(file);
                    break;
            }
        }
    }

    private void RedirectInText(string file, Dictionary<string, string> quoted)
    {
        var text = File.ReadAllText(file);
        if (!FindsMergedUri(text))
        {
            return;
        }

        var builder = new StringBuilder(text);
        foreach (var (from, to) in quoted)
        {
            builder.Replace(from, to);
        }

        File.WriteAllText(file, builder.ToString());
    }

    // Models keep the URIs of their materials as strings in their tree
    private void RedirectInModel(string file)
    {
        var bytes = File.ReadAllBytes(file);
        if (!FindsMergedUri(Encoding.UTF8.GetString(bytes)))
        {
            return;
        }

        var model = TlmFile.Load(file);
        if (model.Root == null)
        {
            return;
        }

        foreach (var value in Descendants(model.Root).OfType<JsonValue>().ToList())
        {
            if (value.TryGetValue<string>(out var text) && _merged.TryGetValue(new LabURI(text), out var redirected))
            {
                value.ReplaceWith(JsonValue.Create(redirected.ToString()));
            }
        }

        model.Save(file);
    }

    private static IEnumerable<JsonNode> Descendants(JsonNode node)
    {
        yield return node;
        var children = node switch
        {
            JsonObject jsonObject => jsonObject.Select(pair => pair.Value),
            JsonArray array => array,
            _ => []
        };
        foreach (var child in children.OfType<JsonNode>())
        {
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private bool FindsMergedUri(string text)
    {
        for (var start = text.IndexOf(UriStart, StringComparison.Ordinal); start != -1; start = text.IndexOf(UriStart, start + 1, StringComparison.Ordinal))
        {
            var end = text.IndexOf('"', start);
            if (end != -1 && _merged.ContainsKey(new LabURI(text[start..end])))
            {
                return true;
            }
        }

        return false;
    }
}
