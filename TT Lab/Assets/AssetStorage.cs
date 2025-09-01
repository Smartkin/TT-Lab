using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace TT_Lab.Assets;

/// <summary>
/// Wrapper for the dictionary of assets for extending the interface and making it asset only
/// </summary>
public class AssetStorage : IEnumerable<KeyValuePair<string, IAsset>>
{
    readonly Dictionary<string, IAsset> _storage = new();

    public static implicit operator Dictionary<string, IAsset>(AssetStorage storage) => storage._storage;

    public void Add(LabURI key, IAsset asset)
    {
        if (_storage.ContainsKey(key))
        {
            throw new InvalidOperationException($"Tried to add existing URI {key} for asset {asset.Name}");
        }
        _storage.Add(key, asset);
    }

    public Boolean Remove(LabURI key)
    {
        return _storage.Remove(key);
    }

    public bool ContainsKey(LabURI key)
    {
        return _storage.ContainsKey(key);
    }

    public Boolean TryGetValue(String key, [MaybeNullWhen(false)] out IAsset value)
    {
        return _storage.TryGetValue(key, out value);
    }

    public void Add(KeyValuePair<String, IAsset> item)
    {
        _storage.Add((LabURI)item.Key, item.Value);
    }

    public void Clear()
    {
        _storage.Clear();
    }

    public IEnumerator<KeyValuePair<String, IAsset>> GetEnumerator()
    {
        return _storage.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _storage.GetEnumerator();
    }

    public Dictionary<string, IAsset>.ValueCollection Values => _storage.Values;

    public Dictionary<string, IAsset>.KeyCollection Keys => _storage.Keys;

    public IAsset this[LabURI key]
    {
        get
        {
            if (!_storage.ContainsKey(key))
            {
                throw new Exception($"Provided URI doesn't exist for {key}");
            }
            return _storage[key];
        }
        set
        {
            if (!_storage.ContainsKey(key))
            {
                Add(key, value);
                return;
            }
            _storage[key] = value;
        }
    }

}