using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Util;

namespace TT_Lab.Project;

/// <summary>
/// Imports and writes the assets of a project being created
/// </summary>
/// <remarks>
/// Holding the data of every asset of the game until the last one was written took gigabytes, so each asset gets imported, written and
/// let go of on its own. Internal assets (a model's parts, a scenery's meshes) are never written as themselves, the assets owning them
/// write them into their own files: they get imported when a write first looks them up, and once no write uses them any more they're
/// kept in their own data file and let go of. The few a write uses again (a rigid model two OGIs look up) load from that file, which
/// goes when the internal assets do. Which ones those are can't be told beforehand, OGIs looking up an ID find other assets than the
/// ones the resolvers made for them
/// </remarks>
internal sealed class CreationWriter(AssetManager assetManager, MemoryGate gate)
{
    private sealed class InternalAsset
    {
        public Boolean Imported;
        public Int32 Users;
        public String? KeptPath;
        public Type? DataType;
        public IAsset? LastWritten;
    }

    private readonly ConcurrentDictionary<IAsset, Byte> _written = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentDictionary<IAsset, InternalAsset> _internals = new(ReferenceEqualityComparer.Instance);
    private readonly ConcurrentBag<String> _keptFiles = [];

    public Boolean IsWritten(IAsset asset) => _written.ContainsKey(asset);

    /// <summary>
    /// Imports and writes every asset that isn't written yet, except for packages which change until the project is done
    /// </summary>
    public void ImportAndWrite()
    {
        foreach (var asset in assetManager.GetAssets().Where(asset => asset.IsInternal))
        {
            _internals.TryAdd(asset, new InternalAsset());
        }

        Log.WriteLine("Importing and writing assets...");
        var start = DateTime.Now;
        var assets = assetManager.GetAssets().Where(asset => !asset.IsInternal && asset is not Package && !IsWritten(asset)).ToList();
        Parallel.ForEach(assets, asset =>
        {
            gate.Enter();
            try
            {
                if (Import(asset))
                {
                    Write(asset);
                }
            }
            finally
            {
                gate.Exit();
            }
        });

        // Embedded assets can reference ones embedded right before them (a PTC's material its texture) so they're imported in order
        var assetsToImport = assetManager.GetAssetsToImport();
        while (!assetsToImport.IsEmpty)
        {
            foreach (var asset in assetsToImport)
            {
                Import(asset);
                // If asset is embedded it shouldn't be exported during game's build stage because its owner will do it for us
                asset.SkipExport = true;
                assetManager.AddAsset(asset);
                Write(asset);
            }

            assetsToImport = assetManager.GetAssetsToImport();
        }

        Log.WriteLine($"Imported and wrote {assets.Count} assets in {DateTime.Now - start}");
    }

    /// <summary>
    /// Internal assets are never part of a project, the files kept for them go with them
    /// </summary>
    public void RemoveInternalAssets()
    {
        foreach (var asset in assetManager.GetAssets().Where(asset => asset.IsInternal).ToList())
        {
            assetManager.RemoveAsset(asset);
        }

        foreach (var file in _keptFiles)
        {
            File.Delete(file);
        }

        _keptFiles.Clear();
        _internals.Clear();
    }

    private static Boolean Import(IAsset asset)
    {
#if !DEBUG
        try
        {
#endif
        asset.Import();
        return true;
#if !DEBUG
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Error importing {asset.Name}: {ex.Message}");
            return false;
        }
#endif
    }

    private void Write(IAsset asset)
    {
        var used = new List<IAsset>();
        var usedSet = new HashSet<IAsset>(ReferenceEqualityComparer.Instance);
        try
        {
            using (assetManager.HookLookups(found => Use(asset, found, used, usedSet)))
            {
#if !DEBUG
                try
                {
#endif
                asset.Serialize(SerializationFlags.SaveData | SerializationFlags.FixReferences);
#if !DEBUG
                }
                catch (Exception ex)
                {
                    Log.WriteLine($"Error serializing {asset.Name}: {ex.Message}");
                }
#endif
            }

            _written.TryAdd(asset, 0);
        }
        finally
        {
            // Parts before the parts they're made of, keeping a rigid model in its file writes its model's data
            foreach (var part in used)
            {
                Release(part);
            }
        }
    }

    // Internal assets a write looks up get imported the first time and loaded from their file when they were let go of already
    private void Use(IAsset written, IAsset found, List<IAsset> used, HashSet<IAsset> usedSet)
    {
        if (!found.IsInternal || !usedSet.Add(found))
        {
            return;
        }

        // Loading an internal asset from its file can make internal assets of its own, those come loaded already
        var state = _internals.GetOrAdd(found, _ => new InternalAsset { Imported = true });
        lock (state)
        {
            if (!state.Imported)
            {
                found.Import();
                state.Imported = true;
            }
            else if (!found.IsLoaded)
            {
                if (state.KeptPath == null)
                {
                    throw new InvalidOperationException($"{found.Name}'s data went without being kept after writing {state.LastWritten?.Name}, writing {written.Name} needs it");
                }

                ((SerializableAsset)found).LoadKeptData(state.DataType!, state.KeptPath);
            }

            state.Users++;
            state.LastWritten = written;
        }

        used.Add(found);
    }

    private void Release(IAsset part)
    {
        var state = _internals[part];
        lock (state)
        {
            if (--state.Users > 0 || !part.IsLoaded)
            {
                return;
            }

            // Its data hasn't changed since it was kept the first time
            var asset = (SerializableAsset)part;
            if (state.KeptPath != null)
            {
                asset.DisposeInternalData();
                return;
            }

            DirectoryCase.Create(part.FullPath);
            state.KeptPath = part.FullDataPath;
            state.DataType = asset.AssetData!.GetType();
            _keptFiles.Add(state.KeptPath);
            asset.KeepInternalData(state.KeptPath);
        }
    }
}
