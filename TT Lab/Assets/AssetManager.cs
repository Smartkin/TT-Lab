using Caliburn.Micro;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Project;

namespace TT_Lab.Assets;

/// <summary>
/// Manages the assets and wraps the getters in a type safe package for obtaining them for the currently opened project
/// </summary>
public class AssetManager
{
    private readonly AssetStorage _assets = [];
    private readonly AssetStorage _importAssets = [];

    // TODO: Check if locks are required to access assets for thread-safety
    private readonly object _assetAccessLock = new();

    // Builds record every asset they look up to know what their outputs depend on, it flows into tasks started while recording
    private readonly AsyncLocal<ConcurrentDictionary<IAsset, byte>?> _accessedAssets = new();

    // Creating a project imports internal assets when a write first looks them up (CreationWriter), it flows the same way
    private readonly AsyncLocal<Action<IAsset>?> _lookupHook = new();

    public IDisposable RecordAccessedAssets(ConcurrentDictionary<IAsset, byte> accessedAssets)
    {
        var previous = _accessedAssets.Value;
        _accessedAssets.Value = accessedAssets;
        return Disposable.Create(() => _accessedAssets.Value = previous);
    }

    internal IDisposable HookLookups(Action<IAsset> hook)
    {
        var previous = _lookupHook.Value;
        _lookupHook.Value = hook;
        return Disposable.Create(() => _lookupHook.Value = previous);
    }

    // A project being created writes its assets in parallel, what one write reads of another asset's data can be half written or
    // let go of right then
    internal bool IsCreating => _lookupHook.Value != null;

    private T RecordAccess<T>(T asset) where T : IAsset
    {
        _accessedAssets.Value?.TryAdd(asset, 0);
        _lookupHook.Value?.Invoke(asset);
        return asset;
    }

    private ImmutableList<T> RecordAccess<T>(ImmutableList<T> assets) where T : IAsset
    {
        var accessedAssets = _accessedAssets.Value;
        if (accessedAssets == null)
        {
            return assets;
        }

        foreach (var asset in assets)
        {
            accessedAssets.TryAdd(asset, 0);
        }

        return assets;
    }

    public AssetManager() { }

    public AssetManager(Dictionary<LabURI, IAsset> assets)
    {
        AddAllAssets(assets);
    }

    public void AddAllAssets(Dictionary<LabURI, IAsset> assets)
    {
        foreach (var ass in assets)
        {
            _assets.Add(ass.Key, ass.Value);
        }
    }

    /// <summary>
    /// Attempts to add the asset with the specified URI and if it already exists then it doesn't get added
    /// </summary>
    /// <param name="uri">Asset's unique resource identifier</param>
    /// <param name="asset">Asset to add</param>
    /// <remarks>
    /// THIS METHOD SHOULD ONLY BE USED WHEN YOU ARE SURE THAT DUPLICATES ARE SKIPPED INTENTIONALLY.
    /// AS THIS SKIPS LOGGING A WARNING INTO A LOG CONSOLE OF TT Lab
    /// </remarks>
    public void TryAddAsset(LabURI uri, IAsset asset)
    {
        if (KeepInScope(asset))
        {
            return;
        }

        if (_assets.ContainsKey(uri))
        {
            return;
        }

        _assets.Add(uri, asset);
    }

    /// <summary>
    /// Adds the asset to the manager with a specified URI
    /// </summary>
    /// <param name="uri">Asset's unique resource identifier</param>
    /// <param name="asset">Asset to add</param>
    public void AddAsset(LabURI uri, IAsset asset)
    {
        if (KeepInScope(asset))
        {
            return;
        }

        if (_assets.ContainsKey(uri))
        {
            Log.WriteLine($"Attempted to add already existing asset {asset.Name} at {uri}! The asset was not added.", Log.LogType.Warning);
            return;
        }

        _assets.Add(uri, asset);
    }
    
    // Internal assets the data loaded by a build makes belong to its scope, the chunks building in parallel make their own
    private static bool KeepInScope(IAsset asset)
    {
        if (!asset.IsInternal || AssetDataScope.Current is not { } scope)
        {
            return false;
        }

        scope.AddInternalAsset(asset);
        return true;
    }

    /// <summary>
    /// Adds the asset to the manager with a specified URI
    /// </summary>
    /// <param name="uri">Asset's unique resource identifier</param>
    /// <param name="asset">Asset to add</param>
    /// <remarks>
    /// THIS METHOD SHOULD ONLY BE USED WHEN YOU ARE SURE THAT DUPLICATES ARE SKIPPED INTENTIONALLY.
    /// AS THIS SKIPS LOGGING A WARNING INTO A LOG CONSOLE OF TT Lab
    /// </remarks>
    public void TryAddAsset(IAsset asset)
    {
        asset.RegenerateLinks();
        TryAddAsset(asset.URI, asset);
    }

    /// <summary>
    /// Adds the asset to the manager
    /// </summary>
    /// <param name="asset">Asset to add</param>
    public void AddAsset(IAsset asset)
    {
        asset.RegenerateLinks();
        AddAsset(asset.URI, asset);
    }

    /// <summary>
    /// Only use during the project's creation stage to delay import of embedded assets in other assets.
    /// <para>Adds an asset to delayed import stage</para>
    /// </summary>
    /// <param name="asset">Asset to add</param>
    public void AddAssetToImport(IAsset asset)
    {
        lock (_assetAccessLock)
        {
            _importAssets.Add(asset.URI, asset);
        }
    }

    /// <summary>
    /// Removes internal assets whose owner's data is no longer loaded, the owner recreates them when its data gets loaded again
    /// </summary>
    /// <param name="keep">Assets that must stay even if their owner isn't loaded</param>
    /// <returns>Amount of removed assets</returns>
    public int RemoveOrphanedInternalAssets(IReadOnlySet<LabURI> keep)
    {
        var removedAssets = 0;
        bool removedAny;
        // Internal assets can own other internal assets so removing an owner can orphan more of them
        do
        {
            removedAny = false;
            foreach (var asset in GetAssets())
            {
                if (!asset.IsInternal || asset.InternalOwner == null || keep.Contains(asset.URI))
                {
                    continue;
                }

                var owner = asset.InternalOwner;
                if (owner.URI != null && DoesAssetExist(owner.URI) && owner.IsLoaded)
                {
                    continue;
                }

                RemoveAsset(asset);
                removedAssets++;
                removedAny = true;
            }
        } while (removedAny);

        return removedAssets;
    }

    /// <summary>
    /// Removes the asset from the manager
    /// </summary>
    /// <param name="uri">Asset's unique resource identifier</param>
    public void RemoveAsset(IAsset asset)
    {
        // The internal assets a build's data makes are its scope's
        if (asset.IsInternal && AssetDataScope.Current is { } scope && scope.RemoveInternalAsset(asset))
        {
            return;
        }

        if (!_assets.ContainsKey(asset.URI))
        {
            Log.WriteLine($"Unable to remove unexisting asset {asset.Name} at {asset.URI}!", Log.LogType.Warning);
            return;
        }

        _assets.Remove(asset.URI);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>AssetManager for the currently opened project</returns>
    public static AssetManager Get()
    {
        return Locator.Current.GetService<ProjectManager>()!.OpenedProject!.AssetManager;
    }

    private LabURI GetUriByTwinId(LabURI package, Type type, IAsset requester, UInt32 id, int? layoutId = null)
    {
        // Items that differ between chunks become a variant for each chunk after the first one. A requester finds the variants of its
        // chunk and the items without a variant, instances belong to their chunk without being variants
        var variation = !string.IsNullOrEmpty(requester.Variation) ? requester.Variation : ChunkVariation(requester.Chunk);
        var candidates = _assets.GetValuesByTypeAndId(type, id).Where(f => (string.IsNullOrEmpty(f.Variation) || f.Variation == variation)
                                                                          && (layoutId != null || f.LayoutID == layoutId)).ToList();
        // The PS2 and Xbox versions' assets share IDs, only the ones of packages the requester's package depends on count. Packages
        // depending on the requester's are the last resort
        var matchedAssets = candidates.Where(f => f.Package == package || DependsOn(package, f.Package)).ToList();
        if (matchedAssets.Count == 0)
        {
            matchedAssets = candidates.Where(f => DependsOn(f.Package, package)).ToList();
        }

        if (matchedAssets.Count == 0)
        {
            return LabURI.Empty;
        }

        // The game reuses IDs for other pictures in other chunks (a hub's 64x32 texture is a 128x64 one of a Totem scenery), so a
        // requester finds what's in its own folder first (a scenery's material its scenery's texture, a PSM's its own), then what
        // the package shares, and another chunk's folder only when nothing else has the ID. The order the assets were added in
        // decided before, which gave the hub's materials the Totem's textures
        var requesterFolder = requester.AdditionalPath ?? string.Empty;
        var savePathFolders = requester.SavePath.Replace('/', '\\').Split('\\');
        return matchedAssets
            .OrderByDescending(f => !string.IsNullOrEmpty(f.Variation))
            .ThenByDescending(f => (f.AdditionalPath ?? string.Empty) == requesterFolder)
            .ThenByDescending(f => string.IsNullOrEmpty(f.AdditionalPath))
            .ThenByDescending(SavePathMatches)
            .ThenBy(f => (string)f.URI, StringComparer.Ordinal)
            .First().URI;

        Int32 SavePathMatches(IAsset asset)
        {
            var folders = asset.SavePath.Replace('/', '\\').Split('\\');
            return folders.Zip(savePathFolders).Count(pair => pair.First == pair.Second);
        }
    }

    private static String ChunkVariation(String? chunk)
    {
        return string.IsNullOrEmpty(chunk) ? string.Empty : chunk.Replace('\\', '_').Replace('/', '_');
    }

    private Boolean DependsOn(LabURI? package, LabURI? dependency)
    {
        // Assets being created during a project's creation don't have their package yet
        if (package == null || dependency == null)
        {
            return false;
        }

        var visited = new HashSet<LabURI>();
        var pending = new Stack<LabURI>();
        pending.Push(package);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current) || !_assets.ContainsKey(current) || _assets[current] is not Package packageAsset)
            {
                continue;
            }

            foreach (var next in packageAsset.Dependencies)
            {
                if (next == dependency)
                {
                    return true;
                }

                pending.Push(next);
            }
        }

        return false;
    }

    public LabURI GetUriByTwinId<T>(IAsset requester, UInt32 id, int? layoutId = null) where T : IAsset
    {
        return GetUriByTwinId(requester.Package, typeof(T), requester, id, layoutId);
    }

    // Disallow obtaining URIs by pure strings publicly
    // ReSharper disable once UnusedMember.Local
    private LabURI GetUri(string uri)
    {
        return _assets.ContainsKey((LabURI)uri) ? _assets[(LabURI)uri].URI : LabURI.Empty;
    }

    public bool DoesAssetExist(LabURI uri)
    {
        return (AssetDataScope.Current?.TryGetInternalAsset(uri, out _) ?? false) || _assets.ContainsKey(uri);
    }

    /// <summary>
    /// Get asset by its package, folder, variant(if needed) and id
    /// </summary>
    /// <param name="package"></param>
    /// <param name="type"></param>
    /// <param name="requester"></param>
    /// <param name="id"></param>
    /// <returns>Any asset</returns>
    private IAsset GetAsset(LabURI package, Type type, IAsset requester, uint id)
    {
        return RecordAccess(_assets[GetUriByTwinId(package, type, requester, id)]);
    }

    /// <summary>
    /// Get asset by URI
    /// </summary>
    /// <param Name="labURI">Asset's URI</param>
    /// <returns>Any asset</returns>
    public IAsset GetAsset(LabURI labURI)
    {
        if (AssetDataScope.Current is { } scope && scope.TryGetInternalAsset(labURI, out var internalAsset))
        {
            return RecordAccess(internalAsset);
        }

        return RecordAccess(_assets[labURI]);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">Specific asset type</typeparam>
    /// <param name="package"></param>
    /// <param name="requester"></param>
    /// <param name="id"></param>
    /// <returns>Asset of a specific type</returns>
    /// <seealso cref="GetAsset(LabURI, Type, IAsset, uint)"/>
    public T GetAsset<T>(LabURI package, IAsset requester, uint id) where T : IAsset
    {
        return (T)GetAsset(package, typeof(T), requester, id);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">Specific asset type</typeparam>
    /// <param name="labURI"></param>
    /// <returns>Asset of a specific type</returns>
    /// <seealso cref="GetAsset(LabURI)"/>
    public T GetAsset<T>(LabURI labURI) where T : IAsset
    {
        return (T)GetAsset(labURI);
    }

    /// <summary>
    /// Get asset data by its package, folder, variant(if needed) and id
    /// </summary>
    /// <typeparam name="T">Specific asset data type</typeparam>
    /// <param name="package"></param>
    /// <param name="requester"></param>
    /// <param name="id"></param>
    /// <returns>Data of the asset of a specified type</returns>
    public T GetAssetData<T>(LabURI package, IAsset requester, uint id) where T : AbstractAssetData
    {
        return GetAsset(package, typeof(T), requester, id).GetData<T>();
    }

    /// <summary>
    /// Get asset data by its URI
    /// </summary>
    /// <typeparam name="T">Specific asset data type</typeparam>
    /// <param name="labURI"></param>
    /// <returns>Data of the asset of a specified type</returns>
    public T GetAssetData<T>(LabURI labURI) where T : AbstractAssetData
    {
        return GetAsset(labURI).GetData<T>();
    }

    /// <summary>
    /// Get generic asset data by its URI
    /// </summary>
    /// <param name="labUri"></param>
    /// <returns></returns>
    public AbstractAssetData GetAssetData(LabURI labUri)
    {
        return GetAsset(labUri).GetData<AbstractAssetData>();
    }

    /// <summary>
    /// Gets all assets of a particular type
    /// </summary>
    /// <typeparam name="T">Asset type deriving from <see cref="IAsset"/> </typeparam>
    /// <returns></returns>
    public ImmutableList<T> GetAllAssetsOf<T>() where T : IAsset
    {
        return RecordAccess(_assets.GetValuesByType(typeof(T)).Cast<T>().ToImmutableList());
    }

    /// <summary>
    /// Assets of a type in the package, the packages it depends on and the ones depending on it, which keeps the PS2 and Xbox versions' apart
    /// </summary>
    public ImmutableList<T> GetRelatedAssetsOf<T>(LabURI package) where T : IAsset
    {
        return RecordAccess(_assets.GetValuesByType(typeof(T)).Where(asset => IsRelated(package, asset.Package)).Cast<T>().ToImmutableList());
    }

    public Boolean IsRelated(LabURI? package, LabURI? other)
    {
        return package == other || DependsOn(package, other) || DependsOn(other, package);
    }

    /// <summary>
    /// Whether the package's assets can refer to the other package's: it's the same one or one it depends on. The global packages depend
    /// on nothing, so their assets only refer to their own
    /// </summary>
    public Boolean IsOwnOrDependency(LabURI? package, LabURI? other)
    {
        return package == other || DependsOn(package, other);
    }

    /// <summary>
    /// Assets of a type the requester can refer to: the ones of its package and the packages it depends on, its chunk's variant of an
    /// asset in place of the asset
    /// </summary>
    public ImmutableList<T> GetAssetsInScopeOf<T>(IAsset requester) where T : IAsset
    {
        return RecordAccess(AssetsInScopeOf<T>(requester).ToImmutableList());
    }

    /// <summary>
    /// The asset of a type the requester can refer to by the name, its own package's when a package it depends on has one of the name
    /// as well. Null when there's none or several are left
    /// </summary>
    public T? FindByName<T>(IAsset requester, string name) where T : class, IAsset
    {
        var matches = AssetsInScopeOf<T>(requester).Where(asset => asset.InvariantName == name).ToList();
        if (matches.Count > 1)
        {
            matches = matches.Where(asset => asset.Package == requester.Package).ToList();
        }

        return matches.Count == 1 ? RecordAccess(matches[0]) : null;
    }

    private IEnumerable<T> AssetsInScopeOf<T>(IAsset requester) where T : IAsset
    {
        var variation = !string.IsNullOrEmpty(requester.Variation) ? requester.Variation : ChunkVariation(requester.Chunk);
        var candidates = _assets.GetValuesByType(typeof(T))
            .Where(asset => IsOwnOrDependency(requester.Package, asset.Package) && (string.IsNullOrEmpty(asset.Variation) || asset.Variation == variation))
            .Cast<T>().ToList();
        var variants = candidates.Where(asset => !string.IsNullOrEmpty(asset.Variation)).Select(asset => asset.InvariantName).ToHashSet();
        return candidates.Where(asset => !string.IsNullOrEmpty(asset.Variation) || !variants.Contains(asset.InvariantName));
    }

    public ImmutableList<IAsset> GetAllAssetsOf(Type type)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"Given type {type.Name} must implement IAsset");
        return RecordAccess(_assets.GetValuesByType(type).ToImmutableList());
    }

    public ImmutableList<LabURI> GetAllAssetUrisOf<T>() where T : IAsset
    {
        return _assets.GetValuesByType(typeof(T)).Select(asset => asset.URI).ToImmutableList();
    }
        
    public ImmutableList<LabURI> GetAllAssetUris()
    {
        return _assets.Values.Select(asset => asset.URI).ToImmutableList();
    }

    public ImmutableList<LabURI> GetAllAssetUrisOf(Type type)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"Given type {type.Name} must implement IAsset");
        return _assets.GetValuesByType(type).Select(asset => asset.URI).ToImmutableList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>All the currently stored assets</returns>
    public ImmutableList<IAsset> GetAssets() { return _assets.Values.Distinct().ToImmutableList(); }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>All assets queued for delayed import stage</returns>
    public ImmutableList<IAsset> GetAssetsToImport()
    {
        lock (_assetAccessLock)
        {
            var immut = _importAssets.Values.Distinct().ToImmutableList();
            _importAssets.Clear();
            return immut;
        }
    }
}