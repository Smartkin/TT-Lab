using System;
using System.Collections.Generic;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public class PropertyGraph
{
    public event Action<PropertyChange>? Changed;
    public PropertyNode Root { get; }
    
    private readonly Dictionary<string, PropertyNode> _properties = new();
    
    public PropertyGraph(PropertyNode root)
    {
        Root = root;
        Index(root);
        Root.InitPropertyLinks();
    }

    internal void NotifyChange(PropertyNode node, object? oldValue, object? newValue)
    {
        Changed?.Invoke(new PropertyChange(node, oldValue, newValue));
    }

    private void Index(PropertyNode node)
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

public record PropertyChange(PropertyNode Node, object? OldValue, object? NewValue);