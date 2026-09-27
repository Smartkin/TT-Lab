using System;
using System.Collections.Generic;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public class PropertyGraph
{
    public event Action<PropertyChange>? Changed;
    public PropertyNode Root { get; }
    public PropertyGraphTracker Tracker { get; }

    /// <summary>
    /// Views of the assets a chunk's document shares with other chunks, other documents don't have any
    /// </summary>
    public TT_Lab.Assets.ChunkOverrideSession? Overrides => Tracker.Overrides;
    
    private readonly Dictionary<string, PropertyNode> _properties = new();
    
    public PropertyGraph(PropertyNode root, PropertyGraphTracker tracker)
    {
        Tracker = tracker;
        Root = root;
        Index(root);
        Root.InitPropertyLinks();
    }

    internal void NotifyChange(PropertyNode node, object? oldValue, object? newValue, PropertyChangeKind kind = PropertyChangeKind.Value, int index = -1)
    {
        Changed?.Invoke(new PropertyChange(node, oldValue, newValue, kind, index));
    }

    internal void Deindex(PropertyNode node)
    {
        _properties.Remove(node.Path);
        foreach (var child in node.Children)
        {
            Deindex(child);
        }
    }

    internal void Index(PropertyNode node)
    {
        node.SetGraph(this);
        _properties[node.Path] = node;

        foreach (var child in node.Children)
        {
            Index(child);
        }
    }
    
    public PropertyNode? this[string path] => _properties.TryGetValue(path, out var node) ? node : null;
    public PropertyNode? Find(string path) => this[path];
}

public enum PropertyChangeKind
{
    Value,
    // An element put into the list at the index, its value is the new value
    Insert,
    // The element at the index taken out of the list, its value is the old value
    Remove,
}

public record PropertyChange(PropertyNode Node, object? OldValue, object? NewValue, PropertyChangeKind Kind = PropertyChangeKind.Value, int Index = -1);