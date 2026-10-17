using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// Sets the values under a node that differ from another object's of its type (a material's settings Blender changed), every one a change
/// of the document: what's the same stays, editors keep showing it, and one step of the history undoes them all
/// </summary>
internal static class NodeValues
{
    public static void Apply(PropertyNode node, object? value)
    {
        var current = node.GetValue();
        // Arrays of the same length take their elements, others are set whole
        if (current is IList currentList && value is IList list && node.Children.Count == currentList.Count && node.Children.All(child => child.Index != null)
            && (!currentList.IsFixedSize || list.Count == currentList.Count))
        {
            ApplyList(node, list);
            return;
        }

        // A flag's bit, a computed value's part, a custom editor's and a linked asset's nodes are another node's value or another asset's
        var children = node.Children.Where(child => child.Metadata != null && child.Index == null && child.GetValueDelegate == null && child.SetValueDelegate == null
                                                    && child.Segment is not ("[data]" or "[custom_editor]")).ToList();
        if (current == null || value == null || current.GetType() != value.GetType() || IsPlain(current.GetType()) || children.Count == 0)
        {
            if (!IsSame(current, value))
            {
                node.SetValue(value);
            }

            return;
        }

        foreach (var child in children)
        {
            Apply(child, child.Metadata!.PropertyInfo.GetValue(value));
        }
    }

    private static void ApplyList(PropertyNode node, IList list)
    {
        for (var i = 0; i < Math.Min(node.Children.Count, list.Count); i++)
        {
            Apply(node.Children[i], list[i]);
        }

        for (var i = node.Children.Count - 1; i >= list.Count; i--)
        {
            node.RemoveElement(node.Children[i]);
        }

        for (var i = node.Children.Count; i < list.Count; i++)
        {
            node.InsertElement(i, list[i]!);
        }
    }

    private static bool IsPlain(Type type)
    {
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type.IsAssignableTo(typeof(LabURI));
    }

    // Values without nodes of their parts (a shader's animation) are the same when they're kept the same
    private static bool IsSame(object? current, object? value)
    {
        if (PropertyNode.IsSameValue(current, value))
        {
            return true;
        }

        if (current == null || value == null || current.GetType() != value.GetType() || IsPlain(current.GetType()))
        {
            return false;
        }

        return JsonConvert.SerializeObject(current) == JsonConvert.SerializeObject(value);
    }
}
