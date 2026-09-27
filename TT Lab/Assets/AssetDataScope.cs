using System;
using System.Collections.Generic;
using System.Threading;
using TT_Lab.AssetData;

namespace TT_Lab.Assets;

/// <summary>
/// Keeps the asset data loaded while it's active, and the internal assets that data creates, apart from the assets' own
/// </summary>
/// <remarks>
/// Chunks build in parallel, each in its own scope, so they never load, change or dispose each other's data or the data open
/// editors work on. Everything the scope loaded is released when it ends
/// </remarks>
public sealed class AssetDataScope : IDisposable
{
    private static readonly AsyncLocal<AssetDataScope?> CurrentScope = new();

    private readonly AssetDataScope? _previous;
    private readonly object _lock = new();
    private readonly Dictionary<IAsset, AbstractAssetData> _data = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<LabURI, IAsset> _internalAssets = new();
    private bool _disposed;

    public static AssetDataScope? Current => CurrentScope.Value;

    public AssetDataScope()
    {
        _previous = CurrentScope.Value;
        CurrentScope.Value = this;
    }

    public AbstractAssetData? GetData(IAsset asset)
    {
        lock (_lock)
        {
            return _data.GetValueOrDefault(asset);
        }
    }

    public void SetData(IAsset asset, AbstractAssetData data)
    {
        AbstractAssetData? previous;
        lock (_lock)
        {
            _data.Remove(asset, out previous);
            _data[asset] = data;
        }

        if (previous != null && previous != data)
        {
            previous.Dispose();
        }
    }

    /// <summary>
    /// Disposes the data the scope loaded for the asset
    /// </summary>
    /// <returns>Whether the scope had data of the asset</returns>
    public bool ReleaseData(IAsset asset)
    {
        AbstractAssetData? data;
        lock (_lock)
        {
            if (!_data.Remove(asset, out data))
            {
                return false;
            }
        }

        data.Dispose();
        return true;
    }

    /// <summary>
    /// Keeps the internal asset in the scope unless one with its URI is there already
    /// </summary>
    public void AddInternalAsset(IAsset asset)
    {
        lock (_lock)
        {
            _internalAssets.TryAdd(asset.URI, asset);
        }
    }

    public bool TryGetInternalAsset(LabURI uri, out IAsset asset)
    {
        lock (_lock)
        {
            return _internalAssets.TryGetValue(uri, out asset!);
        }
    }

    /// <summary>
    /// Forgets the internal asset, data deletes the ones it made when it's disposed
    /// </summary>
    /// <returns>Whether the scope kept the asset</returns>
    public bool RemoveInternalAsset(IAsset asset)
    {
        lock (_lock)
        {
            return _internalAssets.TryGetValue(asset.URI, out var kept) && ReferenceEquals(kept, asset) && _internalAssets.Remove(asset.URI);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<AbstractAssetData> data;
        lock (_lock)
        {
            data = [.. _data.Values];
            _data.Clear();
            foreach (var asset in _internalAssets.Values)
            {
                if (asset.IsLoaded)
                {
                    data.Add(asset.GetData<AbstractAssetData>());
                }
            }
        }

        // Data deletes the internal assets it made when it's disposed, looking them up outside of the scope found the ones of the data
        // editors have open
        foreach (var assetData in data)
        {
            assetData.Dispose();
        }

        lock (_lock)
        {
            _internalAssets.Clear();
        }

        CurrentScope.Value = _previous;
    }
}
