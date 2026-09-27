using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;

namespace TT_Lab.Assets.Factory;

/// <summary>
/// The chunk being built's overrides of the assets it shares, and the views of them it builds
/// </summary>
/// <remarks>
/// A view is made once per chunk, the chunk's resolution tells assets apart by their object
/// </remarks>
public sealed class ChunkOverrides(IEnumerable<AssetOverride> overrides)
{
    private readonly Dictionary<LabURI, AssetOverride> _overrides = overrides.Where(@override => @override.Values.Count > 0)
        .GroupBy(@override => @override.Asset).ToDictionary(group => group.Key, group => group.Last());
    private readonly Dictionary<LabURI, IAsset> _views = new();
    private static readonly AsyncLocal<ChunkOverrides?> CurrentOverrides = new();

    /// <summary>
    /// Overrides of the chunk being built on this flow, what refers to an overridden asset by its ID gets the ID of the chunk's view of it
    /// </summary>
    public static ChunkOverrides? Current => CurrentOverrides.Value;

    public IDisposable Use()
    {
        var previous = CurrentOverrides.Value;
        CurrentOverrides.Value = this;
        return Disposable.Create(() => CurrentOverrides.Value = previous);
    }

    public IAsset? GetView(IAsset asset)
    {
        if (!_overrides.TryGetValue(asset.URI, out var @override))
        {
            return null;
        }

        if (!_views.TryGetValue(asset.URI, out var view))
        {
            view = AssetOverrides.CreateView(asset, @override.Values);
            _views[asset.URI] = view;
        }

        return view;
    }
}
