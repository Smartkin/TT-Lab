using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public class PropertyNode
{
    private bool _isReadOnly;
    
    public event Action? Changed;
    public event Action? ReadOnlyChanged;
    
    public string Name { get; }
    public string Path { get; internal set; }
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (_isReadOnly == value)
            {
                return;
            }
            
            _isReadOnly = value;
            ReadOnlyChanged?.Invoke();
        }
    }
    public PropertyGraph? Graph { get; private set; }
    public object Target { get; internal set; }
    public PropertyMetadata? Metadata { get; }
    public int? Index { get; internal set; }
    public Type PropertyType { get; }
    public PropertyNode? Parent { get; private set; }
    public List<PropertyNode> Children { get; } = [];
    public Action<PropertyNode, object?>? SetValueDelegate { get; init; }
    public Func<PropertyNode, object>? GetValueDelegate { get; init; }

    public PropertyNode(string name, string path, object target, PropertyMetadata? metadata = null, Type? specifiedType = null, int? index = null)
    {
        Name = name;
        Path = path;
        Target = target;
        Metadata = metadata;
        PropertyType = specifiedType ?? metadata?.PropertyInfo.PropertyType ?? target.GetType();
        Index = index;
    }

    internal void SetGraph(PropertyGraph graph)
    {
        Graph = graph;
    }

    // Everything below is addressed through this node's path, so it has to move along with it
    internal void SetPath(string path)
    {
        var previousPath = Path;
        Path = path;
        if (previousPath == path)
        {
            return;
        }

        foreach (var child in Children)
        {
            child.SetPath(child.Path.StartsWith(previousPath, StringComparison.Ordinal) ? path + child.Path[previousPath.Length..] : child.Path);
        }
    }

    internal void InitPropertyLinks()
    {
        foreach (var childNode in Children)
        {
            childNode.InitPropertyLinks();
        }
        
        var links = Metadata?.FieldReactors;
        if (links == null)
        {
            return;
        }
        
        if (Parent == null)
        {
            return;
        }

        foreach (var (prop, reactors) in links)
        {
            var linkedProp = Parent.Find(prop);
            if (linkedProp == null)
            {
                Log.WriteLine($"Linked property {prop} not found!", Log.LogType.Warning);
                continue;
            }
            
            _fieldReactorHandlers.Add(linkedProp, () => LinkedPropOnChanged(linkedProp, reactors));
            linkedProp.Changed += _fieldReactorHandlers[linkedProp];
            
            LinkedPropOnChanged(linkedProp, reactors);
        }
    }

    private void LinkedPropOnChanged(PropertyNode prop, List<IFieldChange> reactors)
    {
        foreach (var reactor in reactors)
        {
            reactor.DataChanged(this, prop);
        }
    }

    public PropertyNode? this[string path] => Find(path);
    public PropertyNode? Find(string path)
    {
        var doIndexing = path.StartsWith('[');
        Debug.Assert(Graph != null, "PropertyGraph must not be null!");
        return Graph[$"{Path}{(doIndexing ? "" : ".")}{path}"];
    }

    public void AddChild(PropertyNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public PropertyNode? AddElement()
    {
        if (GetValue() is not IList list || Metadata?.ContainedTypeConstructor == null)
        {
            return null;
        }

        var addedValue = Metadata.ContainedTypeConstructor();
        list.Add(addedValue);
        
        var childPath = $"{Path}[{Children.Count}]";
        var nodeMetadata = Metadata;
        if (nodeMetadata != null)
        {
            nodeMetadata = nodeMetadata with { ContainedTypeConstructor = null };
        }
        var index = Children.Count;
        var node = PropertyGraphBuilder.BuildNode(list, nodeMetadata, childPath, Graph!.Tracker, innerType: addedValue.GetType(), index: index);
        AddChild(node);
        Graph.Index(node);
        RaiseStructureChange(PropertyChangeKind.Insert, index, addedValue);
        return node;
    }

    /// <summary>
    /// Puts the value into the list at the index, the elements after it move along with their nodes
    /// </summary>
    public PropertyNode? InsertElement(int index, object value)
    {
        if (GetValue() is not IList list || list.IsFixedSize)
        {
            return null;
        }

        Graph!.Deindex(this);
        list.Insert(index, value);
        var nodeMetadata = Metadata;
        if (nodeMetadata != null)
        {
            nodeMetadata = nodeMetadata with { ContainedTypeConstructor = null };
        }

        var node = PropertyGraphBuilder.BuildNode(list, nodeMetadata, $"{Path}[{index}]", Graph.Tracker, innerType: value.GetType(), index: index);
        node.Parent = this;
        Children.Insert(index, node);
        PropertyGraphBuilder.RebuildCollection(this);
        Graph.Index(this);
        RaiseStructureChange(PropertyChangeKind.Insert, index, value);
        return node;
    }

    // By its place, the list can have equal elements before it
    public void RemoveElement(PropertyNode value)
    {
        var index = Children.IndexOf(value);
        if (GetValue() is not IList list || index < 0)
        {
            return;
        }
        
        var removed = value.GetValue();
        Graph!.Deindex(this);
        list.RemoveAt(index);
        Children.RemoveAt(index);
        PropertyGraphBuilder.RebuildCollection(this);
        Graph.Index(this);
        RaiseStructureChange(PropertyChangeKind.Remove, index, removed);
    }

    public T? GetValue<T>()
    {
        var value = GetValue();
        if (value == null)
        {
            return default;
        }
        
        return (T)value;
    }

    public object? GetValue()
    {
        if (GetValueDelegate != null)
        {
            return GetValueDelegate(this);
        }
        
        if (Index.HasValue && Target is IList list)
        {
            return list[Index.Value];
        }
        
        if (Metadata == null)
        {
            return Target;
        }
        
        return Metadata.PropertyInfo.GetValue(Target);
    }

    public void SetValue(object? value)
    {
        var oldValue = GetValue();
        if (oldValue?.Equals(value) == true || (value == null && oldValue == null))
        {
            return;
        }
        
        if (SetValueDelegate != null)
        {
            SetValueDelegate(this, value);
            UpdateChildrenTarget(value);
            RaiseGraphChange(oldValue, GetValue());
            return;
        }
        
        if (Index.HasValue && Target is IList list)
        {
            list[Index.Value] = value;
            foreach (var childNode in Children)
            {
                childNode.Target = value;
            }

            if (PropertyType.IsAssignableTo(typeof(LabURI)))
            {
                PropertyGraphBuilder.RebuildLink(this);
                Graph?.Index(this);
            }
            RaiseGraphChange(oldValue, value);
            return;
        }
        
        if (Metadata == null)
        {
            return;
        }
        
        Metadata.PropertyInfo.SetValue(Target, value);
        if (PropertyType.IsAssignableTo(typeof(LabURI)))
        {
            PropertyGraphBuilder.RebuildLink(this);
            Graph?.Index(this);
        }
        else
        {
            UpdateChildrenTarget(value);
        }
        RaiseGraphChange(oldValue, value);
    }

    private void UpdateChildrenTarget(object? newTarget)
    {
        if (newTarget == null)
        {
            return;
        }

        foreach (var childNode in Children)
        {
            if (newTarget.GetType().IsAssignableTo(typeof(LabURI)))
            {
                childNode.Target = AssetManager.Get().GetAsset((LabURI)newTarget);
            }
            else
            {
                childNode.Target = newTarget;
            }

            childNode.UpdateChildrenTarget(childNode.GetValue());
        }
    }

    private readonly Dictionary<PropertyNode, Action> _fieldReactorHandlers = [];

    private void RaiseGraphChange(object? oldValue, object? newValue)
    {
        Debug.Assert(Graph != null, "PropertyGraph must not be null!");
        Graph.NotifyChange(this, oldValue, newValue);
        Changed?.Invoke();
    }

    private void RaiseStructureChange(PropertyChangeKind kind, int index, object? element)
    {
        Debug.Assert(Graph != null, "PropertyGraph must not be null!");
        Graph.NotifyChange(this, kind == PropertyChangeKind.Remove ? element : null, kind == PropertyChangeKind.Insert ? element : null, kind, index);
        Changed?.Invoke();
    }
}