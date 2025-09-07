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
    /// Adds the asset to the manager with a specified URI
    /// </summary>
    /// <param name="uri">Asset's unique resource identifier</param>
    /// <param name="asset">Asset to add</param>
    /// <remarks>
    /// THIS METHOD SHOULD ONLY BE USED WHEN YOU ARE SURE THAT DUPLICATES ARE SKIPPED INTENTIONALLY.
    /// AS THIS SKIPS LOGGING A WARNING INTO A LOG CONSOLE OF TT Lab
    /// </remarks>
    public void AddAssetUnsafe(IAsset asset)
    {
        AddAssetUnsafe(asset.URI, asset);
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

    private LabURI GetUriByTwinId(LabURI package, Type type, IAsset requester, UInt32 id, int? layoutId = null)
    {
        var filteredAssets = GetAllAssetsOf(type);
        var variation = requester.Variation;
        var savePathFolders = requester.SavePath.Replace('/', '\\').Split('\\');
        var result = LabURI.Empty;
        var matchedAssets = filteredAssets.Where(f => f.ID == id
                                                      && (!string.IsNullOrEmpty(variation) || f.Variation == variation)
                                                      && (layoutId != null || f.LayoutID == layoutId)).ToList();
        if (matchedAssets.Count > 1)
        {
            matchedAssets.Sort((a1, a2) =>
            {
                var asset1SavePathFolders = a1.SavePath.Replace('/', '\\').Split('\\');
                var asset2SavePathFolders = a2.SavePath.Replace('/', '\\').Split('\\');
                var asset1Matches = 0;
                var asset2Matches = 0;
                var searchDepth = Math.Min(savePathFolders.Length, asset1SavePathFolders.Length);
                for (var i = 0; i < searchDepth; i++)
                {
                    if (savePathFolders[i] == asset1SavePathFolders[i])
                    {
                        asset1Matches++;
                    }
                }
                
                searchDepth = Math.Min(savePathFolders.Length, asset2SavePathFolders.Length);
                for (var i = 0; i < searchDepth; i++)
                {
                    if (savePathFolders[i] == asset2SavePathFolders[i])
                    {
                        asset2Matches++;
                    }
                }
                
                return asset2Matches - asset1Matches;
            });

            result = matchedAssets[0].URI;
        }
        else if (matchedAssets.Count == 1)
        {
            result = matchedAssets[0].URI;
        }
        
        if (result != LabURI.Empty)
        {
            return result;
        }
        
        var packageAsset = GetAsset<Package>(package);
        foreach (var dependency in packageAsset.Dependencies)
        {
            result = GetUriByTwinId(dependency, type, requester, id, layoutId);
            if (result != LabURI.Empty)
            {
                break;
            }
        }
        
        return result;
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
        return _assets.ContainsKey(uri);
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
        return _assets[GetUriByTwinId(package, type, requester, id)];
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