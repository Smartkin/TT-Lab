using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Threading;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Assets.Factory;

/// <summary>
/// Where the game finds the layout elements of the chunk being built (the decomp's instancesection.cpp). It numbers a layout's
/// elements of a kind in the order it reads them, the order of their IDs, so an element is found by its place among the chunk's
/// elements of its kind and layout: IDs with a gap (an element taken out) made every reference past it point at the next one.
/// Positions and paths go into one list per chunk, the layouts' after each other: the chunk's own layouts (0-2 and 7) find
/// positions from the list's start, the others past the positions of the last own layout before them that has any
/// (LayoutInstances::RegisterPosition, ChunkEntry::OtherLayoutPosition), and paths are found from the start. Instances, triggers
/// and cameras find instances among their own layout's (RegisterTrigger, RegisterCamera, LayoutInstances::Finish), the AI
/// positions and paths are one table per chunk that every layout with any makes anew (RegisterAiPosition)
/// </summary>
public sealed class LayoutIndexes
{
    private const int LayoutCount = 8;
    private static readonly AsyncLocal<LayoutIndexes?> CurrentIndexes = new();

    private readonly string _chunk;
    private readonly Dictionary<LabURI, (int Layout, UInt32 Index)> _elements = new();
    private readonly int[] _positionStarts = new int[LayoutCount];
    private readonly int[] _pathStarts = new int[LayoutCount];
    // The positions of the last of the own layouts 0-2 that has any, what the other layouts' references start after
    private readonly int _ownPositions;

    public LayoutIndexes(string chunk, IEnumerable<IAsset> resources)
    {
        _chunk = chunk;
        var kinds = resources.Where(asset => asset.LayoutID is >= 0 and < LayoutCount)
            .GroupBy(asset => (Layout: asset.LayoutID!.Value, asset.Section))
            .ToDictionary(kind => kind.Key, kind => kind.OrderBy(asset => asset.ID).ToList());
        foreach (var ((layout, _), elements) in kinds)
        {
            for (var index = 0; index < elements.Count; index++)
            {
                _elements[elements[index].URI] = (layout, (UInt32)index);
            }
        }

        var positions = 0;
        var paths = 0;
        for (var layout = 0; layout < LayoutCount; layout++)
        {
            _positionStarts[layout] = positions;
            _pathStarts[layout] = paths;
            var layoutPositions = kinds.GetValueOrDefault((layout, (UInt32)Constants.LAYOUT_POSITIONS_SECTION))?.Count ?? 0;
            positions += layoutPositions;
            paths += kinds.GetValueOrDefault((layout, (UInt32)Constants.LAYOUT_PATHS_SECTION))?.Count ?? 0;
            if (layoutPositions > 0 && layout < 3)
            {
                _ownPositions = layoutPositions;
            }
        }

        AiLayouts = kinds.Keys.Where(kind => kind.Section is Constants.LAYOUT_AI_POSITIONS_SECTION or Constants.LAYOUT_AI_PATHS_SECTION)
            .Select(kind => kind.Layout).Distinct().Order().ToList();
    }

    /// <summary>
    /// The indexes of the chunk being built on this flow
    /// </summary>
    public static LayoutIndexes? Current => CurrentIndexes.Value;

    /// <summary>
    /// The layouts the chunk has AI positions or paths in, the game keeps the last one's
    /// </summary>
    public IReadOnlyList<int> AiLayouts { get; }

    public IDisposable Use()
    {
        var previous = CurrentIndexes.Value;
        CurrentIndexes.Value = this;
        return Disposable.Create(() => CurrentIndexes.Value = previous);
    }

    /// <summary>
    /// The element's place among the chunk's elements of its kind and layout, none for what isn't one of them
    /// </summary>
    public UInt32? IndexOf(IAsset asset) => _elements.TryGetValue(asset.URI, out var element) ? element.Index : null;

    /// <summary>
    /// An instance an element of the layout refers to, which has to be in the same layout
    /// </summary>
    public UInt32 InstanceReference(int fromLayout, IAsset instance)
    {
        var (layout, index) = Find(instance);
        if (layout != fromLayout)
        {
            throw new InvalidOperationException($"{instance.Alias} is in layout {layout}, the game only finds the instances of the same layout ({fromLayout}) there");
        }

        return index;
    }

    public UInt32 PositionReference(int fromLayout, IAsset position)
    {
        var (layout, index) = Find(position);
        if (layout > fromLayout)
        {
            throw new InvalidOperationException($"{position.Alias} is in layout {layout}, the game only has the positions of layouts up to the instance's ({fromLayout}) when it links them");
        }

        var reference = _positionStarts[layout] + (int)index - (IsChunkOwn(fromLayout) ? 0 : _ownPositions);
        if (reference < 0)
        {
            throw new InvalidOperationException($"{position.Alias} is in layout {layout}, an instance of layout {fromLayout} only finds positions past those of the chunk's own layouts 0-2");
        }

        return (UInt32)reference;
    }

    public UInt32 PathReference(int fromLayout, IAsset path)
    {
        var (layout, index) = Find(path);
        if (layout > fromLayout)
        {
            throw new InvalidOperationException($"{path.Alias} is in layout {layout}, the game only has the paths of layouts up to the instance's ({fromLayout}) when it links them");
        }

        return (UInt32)(_pathStarts[layout] + (int)index);
    }

    private (int Layout, UInt32 Index) Find(IAsset asset)
    {
        if (!_elements.TryGetValue(asset.URI, out var element))
        {
            throw new InvalidOperationException($"{asset.Alias} isn't one of {_chunk}'s resources");
        }

        return element;
    }

    private static bool IsChunkOwn(int layout) => layout < 3 || layout == 7;
}
