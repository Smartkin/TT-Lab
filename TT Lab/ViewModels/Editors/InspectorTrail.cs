using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// A node of the inspector's trail as the inspector's header shows it: an asset by its name, an element by its name or index
/// </summary>
public sealed record InspectorCrumb(int Index, PropertyNode Node, string Caption, string Hint, bool IsCurrent)
{
    public bool HasSeparator => Index > 0;

    public static InspectorCrumb Of(int index, PropertyNode node, bool isCurrent)
    {
        var value = node.GetValue();
        if (value is IAsset asset)
        {
            var caption = string.IsNullOrEmpty(asset.Alias) ? asset.Name : asset.Alias;
            var hint = asset is SerializableAsset { OverriddenAsset: not null }
                ? $"{asset.Type.Name} {asset.URI}: this chunk's version of an asset other chunks share"
                : $"{asset.Type.Name} {asset.URI}";
            return new InspectorCrumb(index, node, caption, hint, isCurrent);
        }

        var name = value?.GetType().GetProperty("Name")?.GetValue(value) as string;
        var fallback = node.Index is { } elementIndex ? $"{node.Parent?.Name} {elementIndex}" : node.Name;
        return new InspectorCrumb(index, node, string.IsNullOrWhiteSpace(name) ? fallback : name, node.Path, isCurrent);
    }
}

/// <summary>
/// What the inspector went through by following links from what got picked, like a browser's history: the inspected node and the ones
/// before and after it. Picking something else to inspect starts it over
/// </summary>
public sealed class InspectorTrail
{
    private readonly List<PropertyNode> _nodes = [];

    public IReadOnlyList<PropertyNode> Nodes => _nodes;

    public int Position { get; private set; } = -1;

    public PropertyNode? Current => Position >= 0 ? _nodes[Position] : null;

    public bool CanGoBack => Position > 0;

    public bool CanGoForward => Position >= 0 && Position < _nodes.Count - 1;

    public void Start(PropertyNode? node)
    {
        _nodes.Clear();
        Position = -1;
        if (node != null)
        {
            _nodes.Add(node);
            Position = 0;
        }
    }

    // What was ahead of the current node goes, like a browser's forward history when a link is followed
    public void Follow(PropertyNode node)
    {
        if (Current == node)
        {
            return;
        }

        _nodes.RemoveRange(Position + 1, _nodes.Count - Position - 1);
        _nodes.Add(node);
        Position = _nodes.Count - 1;
    }

    public PropertyNode? GoTo(int position)
    {
        if (position < 0 || position >= _nodes.Count)
        {
            return null;
        }

        Position = position;
        return Current;
    }

    public PropertyNode? Back() => CanGoBack ? GoTo(Position - 1) : null;

    public PropertyNode? Forward() => CanGoForward ? GoTo(Position + 1) : null;

    public void ReplaceCurrent(PropertyNode node)
    {
        if (Position < 0)
        {
            Start(node);
            return;
        }

        _nodes[Position] = node;
    }

    /// <summary>
    /// Drops the nodes that aren't in the graph any more (their resource deleted, its placing undone). The position stays on its node, or
    /// goes to the one before it when that one went
    /// </summary>
    public void Prune(Func<PropertyNode, bool> isInGraph)
    {
        for (var i = _nodes.Count - 1; i >= 0; i--)
        {
            if (isInGraph(_nodes[i]))
            {
                continue;
            }

            _nodes.RemoveAt(i);
            if (i <= Position)
            {
                Position--;
            }
        }

        if (Position < 0 && _nodes.Count > 0)
        {
            Position = 0;
        }
    }
}
