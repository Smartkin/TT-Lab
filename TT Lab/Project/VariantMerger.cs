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
        var keptTextures = new List<(Texture Variant, IAsset Base)>();
        var merges = new List<(IAsset Variant, IAsset Base, bool SameData)>();
        // Every variant's asset is known before any values are worked out, and a variant's values are compared with every link to a merged
        // variant leading to its asset, so the order they're merged in changes nothing: worked out as each variant got merged, a value kept
        // its link to a variant merged after it, which was then deleted (a level's extra life crate's slot named the level's own health, and
        // the level didn't build). The order (what variants refer to first, then their URIs: the project's assets are kept in the order
        // parallel writes freed their slots) is only the one the chunks get their overrides in
        foreach (var variant in variants.OrderBy(MergeOrder).ThenBy(variant => variant.URI.ToString(), StringComparer.Ordinal))
        {
            // Levels' own versions of the startup chunk's objects are in the level's package, their asset in the global one. The
            // shared asset is the base, not a chunk folder's own copy of the ID (a scenery's texture): the variant is the chunk's
            // version of what the package shares
            if (!bases.TryGetValue((variant.Type, variant.ID, variant.LayoutID), out var candidates))
            {
                continue;
            }

            var ordered = candidates.OrderByDescending(candidate => string.IsNullOrEmpty(candidate.AdditionalPath)).ThenBy(candidate => candidate.URI.ToString(), StringComparer.Ordinal).ToList();
            if ((ordered.FirstOrDefault(candidate => candidate.Package == variant.Package) ??
                 ordered.FirstOrDefault(candidate => assetManager.IsRelated(variant.Package, candidate.Package))) is not { } @base)
            {
                continue;
            }

            var sameData = HaveSameData(@base, variant);
            if (!sameData && !CanHaveValuesOfData(@base))
            {
                if (variant is Texture texture)
                {
                    keptTextures.Add((texture, @base));
                }

                continue;
            }

            _merged[variant.URI] = @base.URI;
            merges.Add((variant, @base, sameData));
        }

        foreach (var (variant, @base, sameData) in merges)
        {
            // Variants no chunk uses went into no chunk, the models of the chunk they were found in are shared and found the asset by its ID
            if (!chunksUsing.TryGetValue(variant.URI, out var users))
            {
                continue;
            }

            var values = GetOverrideValues(@base, variant, sameData);
            if (values.Count == 0)
            {
                continue;
            }

            foreach (var chunk in users)
            {
                chunk.Overrides.Add(new AssetOverride { Asset = @base.URI, Values = new SortedDictionary<string, JToken>(values, StringComparer.Ordinal) });
                changedChunks.Add(chunk);
            }
        }

        LinkMaterialsToTextureVariants(keptTextures, chunks, changedChunks);
        if (_merged.Count == 0 && changedChunks.Count == 0)
        {
            return 0;
        }

        // A stable sort, the same overrides come out in the same order
        foreach (var chunk in changedChunks)
        {
            var sorted = chunk.Overrides.OrderBy(entry => entry.Asset.ToString(), StringComparer.Ordinal).ToList();
            chunk.Overrides.Clear();
            chunk.Overrides.AddRange(sorted);
        }

        RedirectReferences(changedChunks);
        foreach (var variant in _merged.Keys.Select(assetManager.GetAsset).ToList())
        {
            variant.Delete();
        }

        return _merged.Count;
    }

    /// <summary>
    /// A chunk's own picture of a texture ID other chunks have another picture of stays a variant (the game reuses IDs: a hub's 16x16
    /// texture is a 32x32 one elsewhere), and the materials it shares link to the base. The chunk gets the materials pointing at its
    /// picture as overrides, or it built with the other chunks' picture, and so does every other chunk of the same picture: the ones that
    /// found the variant by its content (a disc TT Lab built has the same texture in each), and the ones of a variant of the same picture and
    /// settings, whose texture only differs in what the tools left in its header (each of 43 of the PAL version's chunks has its own BAC3703A
    /// of one picture): the first variant of a picture by URI stays, the others are merged into it, their leftovers dropped like those of a
    /// variant no chunk reaches
    /// </summary>
    private void LinkMaterialsToTextureVariants(List<(Texture Variant, IAsset Base)> keptTextures, Dictionary<(LabURI, string), LevelChunk> chunks, HashSet<LevelChunk> changedChunks)
    {
        var pictures = new List<(Texture Variant, IAsset Base, List<LevelChunk> Users)>();
        foreach (var (variant, @base) in keptTextures)
        {
            var users = variant.UnpackedIn.Select(ToVariation).Prepend(variant.Variation).Distinct()
                .Select(variation => chunks.GetValueOrDefault((variant.Package, variation))).OfType<LevelChunk>().ToList();
            var picture = pictures.FirstOrDefault(kept => kept.Base == @base && HaveSameData(kept.Variant, variant) && OnlyLeftoversDiffer(kept.Variant, variant));
            if (picture.Variant == null)
            {
                pictures.Add((variant, @base, users));
                continue;
            }

            _merged[variant.URI] = picture.Variant.URI;
            picture.Users.AddRange(users.Except(picture.Users));
        }

        var materials = assetManager.GetAllAssetsOf<Material>().Where(material => !material.IsInternal && !_merged.ContainsKey(material.URI)).ToList();
        foreach (var (variant, @base, users) in pictures.Where(picture => picture.Users.Count > 0))
        {
            foreach (var material in materials.Where(material => material.References.Contains(@base.URI)))
            {
                var values = GetLinkOverrideValues(material, @base.URI, variant.URI);
                if (values.Count == 0)
                {
                    continue;
                }

                foreach (var chunk in users)
                {
                    var existing = chunk.Overrides.FirstOrDefault(entry => entry.Asset == material.URI);
                    if (existing == null)
                    {
                        existing = new AssetOverride { Asset = material.URI };
                        chunk.Overrides.Add(existing);
                    }

                    foreach (var (path, value) in values)
                    {
                        existing.Values[path] = value.DeepClone();
                    }

                    changedChunks.Add(chunk);
                }
            }
        }
    }

    // Whether two textures' metadata only differs in what the tools left in their headers, which the game never reads
    private bool OnlyLeftoversDiffer(IAsset first, IAsset second)
    {
        const string leftovers = nameof(Texture.Leftovers);
        return GetOverrideValues(first, second, true).Keys.All(path => path == leftovers || path.StartsWith(leftovers + ".", StringComparison.Ordinal));
    }

    // The values of the asset's document with every link to the URI pointing at the other one
    private static SortedDictionary<string, JToken> GetLinkOverrideValues(IAsset asset, LabURI from, LabURI to)
    {
        var data = asset.GetData<AbstractAssetData>();
        try
        {
            var document = AssetOverrides.GetDocument(asset, data);
            var changed = (JObject)document.DeepClone();
            foreach (var link in changed.Descendants().OfType<JObject>().Where(token => token["_uri"] is JValue { Type: JTokenType.String } value && (string)value! == from.ToString()).ToList())
            {
                link["_uri"] = to.ToString();
            }

            return AssetOverrides.Diff(document, changed);
        }
        finally
        {
            asset.UnloadData();
        }
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

    // Whether data that differs can be a chunk's values, data that isn't kept as JSON the default way can't
    private static bool CanHaveValuesOfData(IAsset asset)
    {
        try
        {
            return AssetOverrides.CanOverrideData(asset.GetData<AbstractAssetData>().GetType());
        }
        finally
        {
            asset.UnloadData();
        }
    }

    /// <summary>
    /// The values the variant has of its own, with the links to merged variants leading to their assets. Of their metadata alone when they
    /// have the same data
    /// </summary>
    private SortedDictionary<string, JToken> GetOverrideValues(IAsset @base, IAsset variant, bool sameData)
    {
        AbstractAssetData? baseData = null;
        AbstractAssetData? variantData = null;
        try
        {
            if (!sameData)
            {
                baseData = @base.GetData<AbstractAssetData>();
                variantData = variant.GetData<AbstractAssetData>();
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

    // Links to merged variants are links to their asset now
    private void RedirectLinks(JToken token)
    {
        foreach (var link in Links(token).ToList())
        {
            if (_merged.TryGetValue(new LabURI((string)link["_uri"]!), out var redirected))
            {
                link["_uri"] = redirected.ToString();
            }
        }
    }

    private static IEnumerable<JObject> Links(JToken token)
    {
        var tokens = token is JContainer container ? container.DescendantsAndSelf() : [token];
        return tokens.OfType<JObject>().Where(link => link["_uri"] is JValue { Type: JTokenType.String });
    }

    // The chunk's own values linking to merged variants link to their assets, a value that's then the asset's is no value of the chunk's,
    // and an asset the chunk has no values of left has no override in it. Whether any changed
    private bool RedirectOverrideValues(LevelChunk chunk)
    {
        var changed = false;
        foreach (var @override in chunk.Overrides.ToList())
        {
            // Chunks can share a value's object (one variant's values went to every chunk using it), a value is changed as a copy
            var redirected = @override.Values.Where(pair => Links(pair.Value).Any(link => _merged.ContainsKey(new LabURI((string)link["_uri"]!))))
                .Select(pair => (Path: pair.Key, Value: pair.Value.DeepClone())).ToList();
            if (redirected.Count == 0)
            {
                continue;
            }

            var document = DocumentOf(@override.Asset);
            foreach (var (path, value) in redirected)
            {
                RedirectLinks(value);
                if (document?.SelectToken(path) is { } assetValue && JToken.DeepEquals(assetValue, value))
                {
                    @override.Values.Remove(path);
                }
                else
                {
                    @override.Values[path] = value;
                }
            }

            if (@override.Values.Count == 0)
            {
                chunk.Overrides.Remove(@override);
            }

            changed = true;
        }

        return changed;
    }

    // The asset's document the way its values are compared, its links to merged variants leading to their assets. None of an asset the
    // project doesn't have or that's merged away itself
    private JObject? DocumentOf(LabURI uri)
    {
        if (_merged.ContainsKey(uri) || !assetManager.DoesAssetExist(uri))
        {
            return null;
        }

        var asset = assetManager.GetAsset(uri);
        try
        {
            var document = AssetOverrides.GetDocument(asset, asset.GetData<AbstractAssetData>());
            RedirectLinks(document);
            return document;
        }
        finally
        {
            asset.UnloadData();
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

            // A chunk's own values are in its metadata, which the text of data files below leaves alone
            if (asset is LevelChunk ownValues && RedirectOverrideValues(ownValues))
            {
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
