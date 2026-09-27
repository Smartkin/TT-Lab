using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace TT_Lab.Assets;

/// <summary>
/// Wrapper for the dictionary of assets for extending the interface and making it asset only
/// </summary>
/// <remarks>
/// Chunks build in parallel, so every access takes the lock and collections are handed out as copies
/// </remarks>
public class AssetStorage : IEnumerable<KeyValuePair<string, IAsset>>
{
    private readonly object _dictLock = new();
    private readonly Dictionary<string, IAsset> _storage = new();
    private readonly Dictionary<Type, Dictionary<string, IAsset>> _typeStorage = new();
    // Built on demand and dropped whenever assets of the type change, asset IDs are always set before the asset is added
    private readonly Dictionary<Type, ILookup<UInt32, IAsset>> _idIndex = new();

    public void Add(LabURI key, IAsset asset)
    {
        lock (_dictLock)
        {
            if (!_storage.TryAdd(key, asset))
            {
                throw new InvalidOperationException($"Tried to add existing URI {key} for asset {asset.Name}");
            }

            if (!_typeStorage.TryGetValue(asset.Type, out var typedStorage))
            {
                typedStorage = [];
                _typeStorage.Add(asset.Type, typedStorage);
            }

            typedStorage.Add(key, asset);
            _idIndex.Remove(asset.Type);
        }
    }

    public Boolean Remove(LabURI key)
    {
        lock (_dictLock)
        {
            foreach (var typeStorage in _typeStorage)
            {
                typeStorage.Value.Remove(key);
            }

            if (_storage.TryGetValue(key, out var asset))
            {
                _idIndex.Remove(asset.Type);
            }

            return _storage.Remove(key);
        }
    }

    public bool ContainsKey(LabURI key)
    {
        lock (_dictLock)
        {
            return _storage.ContainsKey(key);
        }
    }

    public Boolean TryGetValue(String key, [MaybeNullWhen(false)] out IAsset value)
    {
        lock (_dictLock)
        {
            return _storage.TryGetValue(key, out value);
        }
    }

    public void Add(KeyValuePair<String, IAsset> item)
    {
        lock (_dictLock)
        {
            _storage.Add((LabURI)item.Key, item.Value);
            _idIndex.Clear();
        }
    }

    public void Clear()
    {
        lock (_dictLock)
        {
            _typeStorage.Clear();
            _storage.Clear();
            _idIndex.Clear();
        }
    }

    public IEnumerable<IAsset> GetValuesByTypeAndId(Type type, UInt32 id)
    {
        lock (_dictLock)
        {
            if (!_idIndex.TryGetValue(type, out var index))
            {
                index = (_typeStorage.TryGetValue(type, out var typed) ? typed.Values : Enumerable.Empty<IAsset>()).ToLookup(asset => asset.ID);
                _idIndex.Add(type, index);
            }

            return index[id];
        }
    }

    public IEnumerator<KeyValuePair<String, IAsset>> GetEnumerator()
    {
        lock (_dictLock)
        {
            return _storage.ToList().GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IReadOnlyList<IAsset> GetValuesByType(Type type)
    {
        lock (_dictLock)
        {
            return _typeStorage.TryGetValue(type, out var result) ? result.Values.ToList() : [];
        }
    }

    public IReadOnlyList<IAsset> Values
    {
        get
        {
            lock (_dictLock)
            {
                return _storage.Values.ToList();
            }
        }
    }

    public IReadOnlyList<string> Keys
    {
        get
        {
            lock (_dictLock)
            {
                return _storage.Keys.ToList();
            }
        }
    }

    public IAsset this[LabURI key]
    {
        get
        {
            lock (_dictLock)
            {
                if (!_storage.TryGetValue(key, out var result))
                {
                    throw new Exception($"Provided URI doesn't exist for {key}");
                }

                return result;
            }
        }

        set
        {
            lock (_dictLock)
            {
                if (!_storage.ContainsKey(key))
                {
                    Add(key, value);
                    return;
                }

                _storage[key] = value;
                _idIndex.Clear();
            }
        }
    }
}
