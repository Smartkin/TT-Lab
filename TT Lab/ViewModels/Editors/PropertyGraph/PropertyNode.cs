using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public class PropertyNode
{
    public event Action? Changed;
    
    public string Name { get; }
    public string Path { get; }
    public bool IsReadOnly { get; set; }
    public PropertyGraph? Graph { get; private set; }
    public object Target { get; }
    public PropertyMetadata? Metadata { get; }
    public int? Index { get; }
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

    internal void InitPropertyLinks()
    {
        var links = Metadata?.FieldReactors;
        if (links == null)
        {
            return;
        }

        foreach (var (prop, reactors) in links)
        {
            var linkedProp = Find(prop);
            if (linkedProp == null)
            {
                Log.WriteLine($"Linked property {prop} not found!", Log.LogType.Warning);
                continue;
            }
            
            _fieldReactorHandlers.Add(linkedProp, () => LinkedPropOnChanged(linkedProp, reactors));
            linkedProp.Changed += _fieldReactorHandlers[linkedProp];
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
        if (Target is not IList list || Metadata?.ContainedTypeConstructor == null)
        {
            return null;
        }

        var addedValue = Metadata.ContainedTypeConstructor();
        list.Add(addedValue);
        PropertyGraphBuilder.RebuildCollection(this);
        RaiseGraphChange(null, addedValue);
        return Children[^1];
    }

    public void RemoveElement(object value)
    {
        if (Target is not IList list)
        {
            return;
        }
        
        list.Remove(value);
        PropertyGraphBuilder.RebuildCollection(this);
        RaiseGraphChange(value, null);
    }

    public void RemoveElement(int index)
    {
        if (Target is not IList list)
        {
            return;
        }
        
        var oldValue = list[index];
        list.RemoveAt(index);
        PropertyGraphBuilder.RebuildCollection(this);
        RaiseGraphChange(oldValue, null);
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
        if (SetValueDelegate != null)
        {
            SetValueDelegate(this, value);
            RaiseGraphChange(oldValue, GetValue());
            return;
        }
        
        if (Index.HasValue && Target is IList list)
        {
            list[Index.Value] = value;
            RaiseGraphChange(oldValue, value);
            return;
        }
        
        if (Metadata == null)
        {
            return;
        }
        
        Metadata.PropertyInfo.SetValue(Target, value);
        RaiseGraphChange(oldValue, value);
    }

    private readonly Dictionary<PropertyNode, Action> _fieldReactorHandlers = [];

    private void RaiseGraphChange(object? oldValue, object? newValue)
    {
        Debug.Assert(Graph != null, "PropertyGraph must not be null!");
        Graph.NotifyChange(this, oldValue, newValue);
        Changed?.Invoke();
    }
}