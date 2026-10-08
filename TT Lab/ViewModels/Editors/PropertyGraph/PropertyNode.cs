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

    // A node's path is its parent's plus its own segment (".Name", "[index]", "[data]"), worked out when asked and kept until the
    // graph's structure changes: putting an element into a big list moves every element after it, renaming them all made an undo of
    // fifty deletions in a chunk take seconds
    private readonly string _initialPath;
    private string _segment;
    private string? _cachedPath;
    private int _cachedVersion = -1;

    public string Path
    {
        get
        {
            if (Parent == null)
            {
                return _initialPath;
            }

            var version = Graph?.StructureVersion ?? -1;
            if (_cachedPath == null || _cachedVersion != version)
            {
                _cachedPath = Parent.Path + _segment;
                _cachedVersion = version;
            }

            return _cachedPath;
        }
    }

    /// <summary>
    /// What the node adds to its parent's path
    /// </summary>
    public string Segment => _segment;
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

    // How many times the children were made again for a value of another type (a camera subtype picked, or taken away with undo),
    // editors showing them make theirs again when it changes
    public int ChildrenVersion { get; private set; }
    public Action<PropertyNode, object?>? SetValueDelegate { get; init; }
    public Func<PropertyNode, object>? GetValueDelegate { get; init; }

    // Setting the node sets its parent's value (a bit of flags): the parent's change is the one change, a click on a flag was two
    // steps to undo
    public bool SetsParent { get; init; }

    public PropertyNode(string name, string path, object target, PropertyMetadata? metadata = null, Type? specifiedType = null, int? index = null)
    {
        Name = name;
        _initialPath = path;
        _segment = LastSegment(path);
        Target = target;
        Metadata = metadata;
        PropertyType = specifiedType ?? metadata?.PropertyInfo.PropertyType ?? target.GetType();
        Index = index;
    }

    internal void SetGraph(PropertyGraph graph)
    {
        Graph = graph;
    }

    // The last ".Name" or "[index]" of a path
    private static string LastSegment(string path)
    {
        var start = Math.Max(path.LastIndexOf('.'), path.LastIndexOf('['));
        return start <= 0 ? path : path[start..];
    }

    // An element moved to another place of its list
    internal void SetSegment(string segment)
    {
        _segment = segment;
        _cachedPath = null;
    }

    /// <summary>
    /// The child that adds the segment to this node's path, elements by their index first
    /// </summary>
    public PropertyNode? FindChild(string segment)
    {
        if (segment.Length > 2 && segment[0] == '[' && char.IsDigit(segment[1]) && int.TryParse(segment.AsSpan(1, segment.Length - 2), out var index)
            && index >= 0 && index < Children.Count && Children[index]._segment == segment)
        {
            return Children[index];
        }

        foreach (var child in Children)
        {
            if (child._segment == segment)
            {
                return child;
            }
        }

        return null;
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

            // Nodes get indexed again when what's around them changes
            if (_fieldReactorHandlers.ContainsKey(linkedProp))
            {
                continue;
            }

            _fieldReactorHandlers.Add(linkedProp, () => LinkedPropOnChanged(linkedProp, reactors));
            linkedProp.Changed += _fieldReactorHandlers[linkedProp];
            foreach (var reactor in reactors)
            {
                reactor.Linked(this, linkedProp);
            }
        }
    }

    private void LinkedPropOnChanged(PropertyNode prop, List<IFieldChange> reactors)
    {
        // A field pasted over gets the copied value, an object's type pasted along with its lists doesn't fit them to the type
        if (Graph?.IsReplaying == true || Graph?.IsPastedOver(this) == true)
        {
            foreach (var reactor in reactors)
            {
                reactor.Linked(this, prop);
            }

            return;
        }

        Graph?.BeginConsequences();
        try
        {
            foreach (var reactor in reactors)
            {
                reactor.DataChanged(this, prop);
            }
        }
        finally
        {
            Graph?.EndConsequences();
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

    /// <summary>
    /// How many elements the game takes in the list (<see cref="DocumentCollectionViewModel.MaxCount"/>), none when it takes any number.
    /// Adding stops there, undo and redo still put back what they took out
    /// </summary>
    public int? MaxElements => Metadata?.EditorParams is { } parameters && parameters.TryGetValue(DocumentCollectionViewModel.MaxCount, out var max) ? Convert.ToInt32(max) : null;

    public bool IsFull => MaxElements is { } max && Children.Count >= max;

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
            nodeMetadata = nodeMetadata.ForParts();
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

        list.Insert(index, value);
        var nodeMetadata = Metadata;
        if (nodeMetadata != null)
        {
            nodeMetadata = nodeMetadata.ForParts();
        }

        var node = PropertyGraphBuilder.BuildNode(list, nodeMetadata, $"{Path}[{index}]", Graph!.Tracker, innerType: value.GetType(), index: index);
        node.Parent = this;
        Children.Insert(index, node);
        PropertyGraphBuilder.RebuildCollection(this);
        Graph.Index(node);
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
        list.RemoveAt(index);
        Children.RemoveAt(index);
        PropertyGraphBuilder.RebuildCollection(this);
        Graph!.Deindex(value);
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

    // NaNs of other bits are other values, shaders keep bits of the tools' memory in theirs and undo couldn't put them back
    internal static bool IsSameValue(object? a, object? b) => (a, b) switch
    {
        (null, null) => true,
        (Single x, Single y) => BitConverter.SingleToInt32Bits(x) == BitConverter.SingleToInt32Bits(y),
        (Double x, Double y) => BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(y),
        _ => a?.Equals(b) == true,
    };

    public void SetValue(object? value)
    {
        var oldValue = GetValue();
        if (IsSameValue(oldValue, value))
        {
            return;
        }
        
        if (SetValueDelegate != null)
        {
            SetValueDelegate(this, value);
            if (SetsParent)
            {
                return;
            }

            UpdateChildrenTarget(value);
            RaiseGraphChange(oldValue, GetValue());
            return;
        }
        
        if (Index.HasValue && Target is IList list)
        {
            list[Index.Value] = value;
            if (PropertyType.IsAssignableTo(typeof(LabURI)))
            {
                PropertyGraphBuilder.RebuildLink(this);
                Graph?.Index(this);
            }
            else if (NeedsNewChildren(oldValue, value))
            {
                RebuildChildren();
            }
            else
            {
                foreach (var childNode in Children)
                {
                    childNode.Target = value!;
                }
            }

            RaiseGraphChange(oldValue, value);
            RaiseDescendantsChanged();
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
        else if (NeedsNewChildren(oldValue, value))
        {
            RebuildChildren();
        }
        else
        {
            UpdateChildrenTarget(value);
        }

        RaiseGraphChange(oldValue, value);
        RaiseDescendantsChanged();
    }

    // The values under a replaced one are the new value's now, their editors show them again (flags' check boxes after an undo)
    private void RaiseDescendantsChanged()
    {
        foreach (var child in Children)
        {
            child.Changed?.Invoke();
            child.RaiseDescendantsChanged();
        }
    }

    // A value of another type (or none where there was one) has other values in it, the old children would show the old ones
    private bool NeedsNewChildren(object? oldValue, object? newValue)
    {
        if (oldValue?.GetType() == newValue?.GetType())
        {
            return false;
        }

        return PropertyGraphBuilder.CanHaveChildren(oldValue?.GetType()) || PropertyGraphBuilder.CanHaveChildren(newValue?.GetType());
    }

    private void RebuildChildren()
    {
        foreach (var child in Children)
        {
            Graph?.Deindex(child);
        }

        Children.Clear();
        var value = GetValue();
        if (value != null && Graph != null)
        {
            var built = PropertyGraphBuilder.BuildNode(Target, Metadata, Path, Graph.Tracker, value.GetType(), Index);
            // Every child under this node before any gets linked: a linked field is found among its siblings, and before they were
            // looked for under the node they were built in, which isn't in the graph
            var children = built.Children.ToList();
            foreach (var child in children)
            {
                AddChild(child);
            }

            foreach (var child in children)
            {
                Graph.Index(child);
            }
        }

        ChildrenVersion++;
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