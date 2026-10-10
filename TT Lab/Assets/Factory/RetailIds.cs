using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;

namespace TT_Lab.Assets.Factory;

/// <summary>
/// The game's IDs of the graphics items of the file being built. The tools made those IDs of what the files don't keep (their scenes),
/// so TT Lab makes them of the items' data, and builds an item with the disc's ID while it's made of what the disc's was: textures,
/// materials and skies by their own <see cref="IAsset.RetailFingerprints"/>, the parts in their owners' model files by their owners'
/// <see cref="IAsset.RetailPartIds"/>. The first item of an ID in a file takes it, and items made of the same share it: the game reuses
/// IDs for other pictures in other chunks, and a texture brought over from one of them would take the place of the chunk's own one. The
/// others get the ID of their data
/// </summary>
public sealed class RetailIds
{
    private static readonly AsyncLocal<RetailIds?> CurrentIds = new();

    private readonly object _lock = new();
    // The fingerprint of the item that took the ID
    private readonly Dictionary<(Type Kind, UInt32 Id), UInt32> _taken = new();
    private readonly Dictionary<LabURI, UInt32?> _given = new();

    /// <summary>
    /// The IDs of the file being built on this flow
    /// </summary>
    public static RetailIds? Current => CurrentIds.Value;

    public IDisposable Use()
    {
        var previous = CurrentIds.Value;
        CurrentIds.Value = this;
        return Disposable.Create(() => CurrentIds.Value = previous);
    }

    /// <summary>
    /// The disc's ID the asset is built with, none when it's not made of what the disc's item was or another item took it
    /// </summary>
    internal static UInt32? IdOf(SerializableAsset asset)
    {
        var owner = asset.IsInternal ? OwnerOf(asset) : asset;
        if (owner == null || (asset.IsInternal ? owner.RetailPartIds == null : asset.RetailFingerprints == null))
        {
            return null;
        }

        var ids = Current;
        if (ids != null)
        {
            lock (ids._lock)
            {
                if (ids._given.TryGetValue(asset.URI, out var known))
                {
                    return known;
                }
            }
        }

        // Worked out without the lock, it loads the asset's data
        var fingerprint = asset.Fingerprint();
        List<UInt32> candidates = asset.IsInternal
            ? owner.RetailPartIds!.GetValueOrDefault(PartKey(asset, fingerprint), [])
            : asset.RetailFingerprints!.Contains(fingerprint) ? [asset.ID] : [];
        if (ids == null)
        {
            return candidates.Count > 0 ? candidates[0] : null;
        }

        lock (ids._lock)
        {
            if (ids._given.TryGetValue(asset.URI, out var known))
            {
                return known;
            }

            // The disc has items made of the same under IDs of their own (a scenery's models): one nothing took yet, else one an item made of
            // the same took
            var kind = asset.GetType();
            UInt32? given = candidates.Where(candidate => !ids._taken.ContainsKey((kind, candidate))).Select(candidate => (UInt32?)candidate).FirstOrDefault()
                            ?? candidates.Where(candidate => ids._taken[(kind, candidate)] == fingerprint).Select(candidate => (UInt32?)candidate).FirstOrDefault();
            if (given != null)
            {
                ids._taken[(kind, given.Value)] = fingerprint;
            }

            ids._given[asset.URI] = given;
            return given;
        }
    }

    private static IAsset? OwnerOf(IAsset part)
    {
        var owner = part.InternalOwner;
        while (owner is { IsInternal: true })
        {
            owner = owner.InternalOwner;
        }

        return owner;
    }

    private static string PartKey(IAsset part, UInt32 fingerprint) => $"{part.GetType().Name}:{fingerprint:X8}";

    /// <summary>
    /// What the disc's textures, materials and skies are made of, and every chunk's own version of one (its overrides), recorded once
    /// their assets are written. The ones written already get it added to their file
    /// </summary>
    public static void Record(IEnumerable<IAsset> assets, IEnumerable<LevelChunk> chunks)
    {
        var overrides = chunks.SelectMany(chunk => chunk.Overrides).GroupBy(@override => @override.Asset)
            .ToDictionary(group => group.Key, group => group.Select(@override => @override.Values).ToList());
        var recorded = assets.OfType<SerializableAsset>().Where(asset => asset.RecordsRetailId).ToList();
        Parallel.ForEach(recorded, asset =>
        {
            using (var scope = new AssetDataScope())
            {
                // What a build reads, the file, whatever creation still has of the data
                if (File.Exists(asset.FullDataPath))
                {
                    scope.ReadAgain(asset);
                }

                var fingerprints = new List<UInt32> { asset.Fingerprint() };
                foreach (var values in overrides.GetValueOrDefault(asset.URI, []))
                {
                    var fingerprint = AssetOverrides.CreateView(asset, values).Fingerprint();
                    if (!fingerprints.Contains(fingerprint))
                    {
                        fingerprints.Add(fingerprint);
                    }
                }

                asset.RetailFingerprints = fingerprints;
            }

            WriteInto(asset);
        });
    }

    /// <summary>
    /// The disc's IDs of the parts an asset's model file holds, recorded while creation writes the asset: its parts are the disc's items
    /// then, reading the file makes them again without their IDs
    /// </summary>
    /// <param name="owner">The asset being written</param>
    /// <param name="known">The parts recorded so far, what a part made again without its ID was (a rigid model two OGIs share is read from
    /// the file creation kept it in for the second, which makes its model anew)</param>
    public static void RecordParts(IAsset owner, Dictionary<String, List<UInt32>> known)
    {
        if (owner.IsInternal)
        {
            return;
        }

        try
        {
            IEnumerable<LabURI>? parts = owner.GetData<AbstractAssetData>() switch
            {
                OGIData ogi => ogi.RigidModelIds.Append(ogi.Skin).Append(ogi.BlendSkin),
                SceneryData scenery => scenery.Placements.Select(placement => placement.Model).Concat(DynamicSceneryMeshes(scenery)),
                SkydomeData sky => sky.Meshes,
                // Default.rm2's meshes, their models
                RigidModelData mesh => [mesh.Model],
                _ => null
            };
            if (parts == null)
            {
                return;
            }

            var ids = new Dictionary<String, List<UInt32>>();
            var seen = new HashSet<LabURI>();
            foreach (var part in parts)
            {
                RecordPart(part, ids, seen, known);
            }

            owner.RetailPartIds = ids.Count > 0 ? ids : null;
        }
        catch (Exception ex)
        {
            Log.WriteLine($"{owner.Name}'s parts get the IDs of their data, what they're made of wasn't worked out: {ex.Message}", Log.LogType.Debug);
            owner.RetailPartIds = null;
        }
    }

    private static IEnumerable<LabURI> DynamicSceneryMeshes(SceneryData scenery)
    {
        var assetManager = AssetManager.Get();
        return scenery.DynamicScenery != LabURI.Empty && assetManager.DoesAssetExist(scenery.DynamicScenery)
            ? assetManager.GetAssetData<DynamicSceneryData>(scenery.DynamicScenery).DynamicModels.Select(model => model.Mesh)
            : [];
    }

    private static void RecordPart(LabURI uri, Dictionary<String, List<UInt32>> ids, HashSet<LabURI> seen, Dictionary<String, List<UInt32>> known)
    {
        var assetManager = AssetManager.Get();
        if (uri == LabURI.Empty || !seen.Add(uri) || !assetManager.DoesAssetExist(uri) || assetManager.GetAsset(uri) is not SerializableAsset part)
        {
            return;
        }

        var key = PartKey(part, part.Fingerprint());
        List<UInt32> found;
        lock (known)
        {
            // A part made again of a model file creation kept has no ID, it's what an earlier part of the same was
            found = part.ID != 0 ? [part.ID] : known.GetValueOrDefault(key, []).ToList();
            if (part.ID != 0)
            {
                if (!known.TryGetValue(key, out var all))
                {
                    known[key] = all = [];
                }

                if (!all.Contains(part.ID))
                {
                    all.Add(part.ID);
                }
            }
        }

        if (found.Count > 0)
        {
            if (!ids.TryGetValue(key, out var list))
            {
                ids[key] = list = [];
            }

            list.AddRange(found.Where(id => !list.Contains(id)));
        }

        switch (part.GetData())
        {
            case RigidModelData rigid:
                RecordPart(rigid.Model, ids, seen, known);
                break;
            case LodModelData lod:
                foreach (var mesh in lod.Meshes)
                {
                    RecordPart(mesh, ids, seen, known);
                }

                break;
        }
    }

    // Only the key is written into the file: the variants' merge rewrote what links to the merged ones in the files, not in the assets
    private static void WriteInto(SerializableAsset asset)
    {
        var path = Path.Combine(asset.FullPath, $"{asset.Name}.json");
        if (!File.Exists(path))
        {
            return;
        }

        var metadata = JObject.Parse(File.ReadAllText(path));
        metadata[nameof(IAsset.RetailFingerprints)] = new JArray(asset.RetailFingerprints!.Cast<object>().ToArray());
        File.WriteAllText(path, metadata.ToString(Formatting.Indented));
    }
}
