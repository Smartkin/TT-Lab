using Caliburn.Micro;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
    /// Adds the asset to the manager with a specified URI
    /// </summary>
    /// <param name="uri">Asset's unique resource identifier</param>
    /// <param name="asset">Asset to add</param>
    /// <remarks>
    /// THIS METHOD SHOULD ONLY BE USED WHEN YOU ARE SURE THAT DUPLICATES ARE SKIPPED INTENTIONALLY.
    /// AS THIS SKIPS LOGGING A WARNING INTO A LOG CONSOLE OF TT Lab
    /// </remarks>
    public void AddAssetUnsafe(LabURI uri, IAsset asset)
    {
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
        if (_assets.ContainsKey(uri))
        {
            Log.WriteLine($"WARNING: Attempted to add already existing asset at {uri}! The asset was not added.");
            return;
        }

        _assets.Add(uri, asset);
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
    /// Removes the asset from the manager
    /// </summary>
    /// <param name="uri">Asset's unique resource identifier</param>
    public void RemoveAsset(LabURI uri)
    {
        if (!_assets.ContainsKey(uri))
        {
            Log.WriteLine($"WARNING: Unable to remove unexisting asset {uri}!");
            return;
        }

        foreach (var pair in _assets)
        {
            pair.Value.RemoveReference(uri);
        }

        _assets.Remove(uri);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>AssetManager for the currently opened project</returns>
    public static AssetManager Get()
    {
        return IoC.Get<ProjectManager>().OpenedProject!.AssetManager;
    }

    private LabURI GetUriByTwinId(LabURI package, Type type, String? variant, UInt32 id, int? layoutId = null)
    {
        var filteredAssets = GetAllAssetsOf(type);
        var result = filteredAssets.Where(f => f.ID == id
                                               && (!string.IsNullOrEmpty(variant) || f.Variation == variant)
                                               && (layoutId != null || f.LayoutID == layoutId)).Select(f => f.URI).FirstOrDefault(LabURI.Empty);
        if (result != LabURI.Empty)
        {
            return result;
        }
        
        var packageAsset = GetAsset<Package>(package);
        foreach (var dependency in packageAsset.Dependencies)
        {
            result = GetUriByTwinId(dependency, type, variant, id);
            if (result != LabURI.Empty)
            {
                break;
            }
        }
        
        return result;
    }

    public LabURI GetUriByTwinId<T>(LabURI package, String? variant, UInt32 id) where T : IAsset
    {
        return GetUriByTwinId(package, typeof(T), variant, id);
    }
    
    public LabURI GetUriByTwinId<T>(LabURI package, String? variant, UInt32 id, int? layoutId) where T : IAsset
    {
        return GetUriByTwinId(package, typeof(T), variant, id, layoutId);
    }

    // Disallow obtaining URIs by pure strings publicly
    // ReSharper disable once UnusedMember.Local
    private LabURI GetUri(string uri)
    {
        return _assets.ContainsKey((LabURI)uri) ? _assets[(LabURI)uri].URI : LabURI.Empty;
    }

    public bool DoesAssetExist(LabURI uri)
    {
        return _assets.ContainsKey(uri);
    }

    /// <summary>
    /// Get asset by its package, folder, variant(if needed) and id
    /// </summary>
    /// <param name="package"></param>
    /// <param name="type"></param>
    /// <param name="variant"></param>
    /// <param name="id"></param>
    /// <returns>Any asset</returns>
    private IAsset GetAsset(LabURI package, Type type, String? variant, uint id)
    {
        return _assets[GetUriByTwinId(package, type, variant, id)];
    }

    /// <summary>
    /// Get asset by URI
    /// </summary>
    /// <param Name="labURI">Asset's URI</param>
    /// <returns>Any asset</returns>
    public IAsset GetAsset(LabURI labURI)
    {
        return _assets[labURI];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">Specific asset type</typeparam>
    /// <param name="package"></param>
    /// <param name="folder"></param>
    /// <param name="variant"></param>
    /// <param name="id"></param>
    /// <returns>Asset of a specific type</returns>
    /// <seealso cref="GetAsset(LabURI, string, string?, uint)"/>
    public T GetAsset<T>(LabURI package, String? variant, uint id) where T : IAsset
    {
        return (T)GetAsset(package, typeof(T), variant, id);
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
    /// <param name="folder"></param>
    /// <param name="variant"></param>
    /// <param name="id"></param>
    /// <returns>Data of the asset of a specified type</returns>
    public T GetAssetData<T>(LabURI package, String? variant, uint id) where T : AbstractAssetData
    {
        return GetAsset(package, typeof(T), variant, id).GetData<T>();
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
    /// Gets all assets of a particular type
    /// </summary>
    /// <typeparam name="T">Asset type deriving from <see cref="IAsset"/> </typeparam>
    /// <returns></returns>
    public ImmutableList<T> GetAllAssetsOf<T>() where T : IAsset
    {
        return _assets.GetValuesByType(typeof(T)).Cast<T>().ToImmutableList();
    }

    public ImmutableList<IAsset> GetAllAssetsOf(Type type)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"Given type {type.Name} must implement IAsset");
        return _assets.GetValuesByType(type).ToImmutableList();
    }

    public ImmutableList<LabURI> GetAllAssetUrisOf<T>() where T : IAsset
    {
        return _assets.GetValuesByType(typeof(T)).Select(asset => asset.URI).ToImmutableList();
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