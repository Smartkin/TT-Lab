using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public static class PropertyGraphBuilder
{
    public static PropertyGraph Build(IDocumentModel document)
    {
        var graphTracker = new PropertyGraphTracker();
        var rootNode = BuildNode(document, null, "Root", graphTracker);
        return new PropertyGraph(rootNode, graphTracker);
    }

    public static void RebuildCollection(PropertyNode node)
    {
        BuildCollection(node, node.GetValue()!, node.Path, node.Graph!.Tracker);
    }

    public static void RebuildLink(PropertyNode node)
    {
        node.Children.Clear();
        BuildLink(node, node.Path, node.Graph!.Tracker);
    }

    public static PropertyNode BuildNode(object target, PropertyMetadata? property, string path, PropertyGraphTracker tracker, Type? innerType = null, int? index = null)
    {
        var name = innerType?.Name ?? property?.PropertyInfo.Name ?? target.GetType().Name;
        var node = new PropertyNode(name, path, target, property, specifiedType: innerType, index: index);
        var type = innerType ?? property?.PropertyInfo.PropertyType ?? target.GetType();

        if (IsEnumFlags(type))
        {
            BuildEnumFlagsCollection(node, node.GetValue()!, path);
            return node;
        }

        if (IsLeaf(type))
        {
            return node;
        }

        if (IsLink(type))
        {
            BuildLink(node, path, tracker);
            return node;
        }

        if (IsIndexableCollection(type))
        {
            BuildCollection(node, node.GetValue()!, path, tracker);
            return node;
        }

        BuildObject(node, node.GetValue()!, path, tracker);
        return node;
    }

    private static void BuildEnumFlagsCollection(PropertyNode node, object enumFlags, string path)
    {
        var enumValues = Enum.GetValues(node.PropertyType);
        var shiftIdx = 0;
        foreach (var enumValue in enumValues)
        {
            var childPath = $"{path}.{enumValue}";
            var child = new PropertyNode($"{enumValue}", childPath, enumFlags, node.Metadata, index: shiftIdx)
            {
                SetValueDelegate = (property, value) =>
                {
                    var currentValue = Convert.ToUInt64(property.Target);
                    var shiftValue = (UInt64)(1UL << (property.Index!));
                    var setOrUnset = (bool)value!;
                    if (setOrUnset)
                    {
                        currentValue |= shiftValue;
                    }
                    else
                    {
                        currentValue &= ~shiftValue;
                    }

                    var converted = Convert.ChangeType(currentValue,
                        Enum.GetUnderlyingType(property.Parent!.PropertyType));
                    property.Parent?.SetValue(converted);
                },
                GetValueDelegate = (property) =>
                {
                    var currentValue = Convert.ToUInt64(property.Target);
                    var shiftValue = (UInt64)(1UL << (property.Index!));
                    return (currentValue & shiftValue) != 0;
                }
            };
            node.AddChild(child);
            shiftIdx++;
        }
    }

    private static void BuildLink(PropertyNode node, string path, PropertyGraphTracker tracker)
    {
        var uri = node.GetValue<LabURI>();
        if (uri == LabURI.Empty || uri == null)
        {
            return;
        }

        var setBranchLimit = false;
        if (node.Metadata?.Editable?.MaxLinkGraphDepth is not null && node.Metadata?.Editable?.MaxLinkGraphDepth != -1)
        {
            tracker.SetMaxDepth((int)node.Metadata?.Editable?.MaxLinkGraphDepth!);
            setBranchLimit = true;
        }
        
        var asset = AssetManager.Get().GetAsset(uri);
        asset.GetData<AbstractAssetData>(); // Load the asset data
        var childPath = $"{path}[data]";
        var child = new PropertyNode(asset.Type.Name, childPath, asset, specifiedType: asset.Type)
        {
            SetValueDelegate = (property, value) =>
            {
                property.Target = value;
                RebuildLink(property);
            }
        };
        node.AddChild(child);

        if (!tracker.Enter(uri) || tracker.DepthExceeded())
        {
            return;
        }
        
        BuildObject(child, asset, childPath, tracker);
        
        tracker.Exit(uri);

        if (setBranchLimit)
        {
            tracker.SetMaxDepth(-1);
        }
    }

    private static void BuildCollection(PropertyNode node, object collection, string path, PropertyGraphTracker tracker)
    {
        if (collection is not IList list)
        {
            return;
        }

        for (var i = 0; i < list.Count; ++i)
        {
            if (node.Children.Count > i)
            {
                node.Children[i].Path = $"{path}[{i}]";
                node.Children[i].Index = i;
                continue;
            }
            
            var item = list[i];
            if (item == null)
            {
                continue;
            }

            var childPath = $"{path}[{i}]";
            var nodeMetadata = node.Metadata;
            if (nodeMetadata != null)
            {
                nodeMetadata = nodeMetadata with { ContainedTypeConstructor = null };
            }
            
            var child = BuildNode(collection, nodeMetadata, childPath, tracker, innerType: item.GetType(), index: i);
            node.AddChild(child);
        }
    }

    private static void BuildObject(PropertyNode node, object? target, string path, PropertyGraphTracker tracker)
    {
        if (target == null)
        {
            return;
        }
        
        var metadata = DocumentMetadataCache.Get(target.GetType());
        if (metadata.Editable != null)
        {
            var customEditorNode =
                new PropertyNode($"CustomEditor_{path}", $"{path}[custom_editor]", target, new PropertyMetadata
                {
                    Editable = metadata.Editable,
                    EditorDescType = metadata.EditorDescType,
                    EditorParams = metadata.EditorParams,
                    EditorParamWrappers = metadata.EditorParamWrappers,
                    FieldReactors = metadata.FieldReactors,
                    PropertyInfo = node.Metadata!.PropertyInfo,
                }, target.GetType())
                {
                    GetValueDelegate = propertyNode => propertyNode.Target
                };
            node.AddChild(customEditorNode);
            return;
        }
        foreach (var prop in metadata.Properties)
        {
            var value = prop.PropertyInfo.GetValue(target);
            if ((value == null && prop.Editable?.IsConstructible != true) || IsExcludedType(prop.PropertyInfo.PropertyType))
            {
                continue;
            }
            
            var childPath = $"{path}.{prop.PropertyInfo.Name}";
            var child = BuildNode(target, prop, childPath, tracker);
            node.AddChild(child);
        }
    }

    private static bool IsLeaf(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal);
    }

    private static bool IsExcludedType(Type type)
    {
        return type == typeof(DummyData);
    }

    private static bool IsEnumFlags(Type type)
    {
        return type.IsEnum && type.GetCustomAttribute<FlagsAttribute>() != null;
    }

    private static bool IsIndexableCollection(Type type)
    {
        return type.IsArray || typeof(IList).IsAssignableFrom(type);
    }

    private static bool IsLink(Type type)
    {
        return type == typeof(LabURI);
    }
}

public class PropertyGraphTracker
{
    private readonly HashSet<LabURI> _currentVisits = [];
    private readonly HashSet<LabURI> _trackedUris = [];
    private int _maxDepth = -1;
    private int _currentDepth = 0;

    public IReadOnlyList<LabURI> ExploredUris => _trackedUris.ToList();

    public bool Enter(LabURI uri)
    {
        _currentDepth++;
        _trackedUris.Add(uri);
        return _currentVisits.Add(uri);
    }
    
    public void Exit(LabURI uri)
    {
        _currentDepth--;
        _currentVisits.Remove(uri);
    }

    public void SetMaxDepth(int maxDepth)
    {
        _maxDepth = maxDepth;
    }

    public bool DepthExceeded()
    {
        return _maxDepth != -1 && _currentDepth >= _maxDepth;
    }

    public bool IsInPath(LabURI uri)
    {
        return _currentVisits.Contains(uri);
    }
}