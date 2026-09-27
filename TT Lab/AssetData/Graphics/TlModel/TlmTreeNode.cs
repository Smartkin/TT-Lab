using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;

namespace TT_Lab.AssetData.Graphics.TlModel;

/// <summary>
/// A node of a TT Lab model file with the nodes around it, for reading trees where the hierarchy means something
/// </summary>
public sealed class TlmTreeNode
{
    private TlmTreeNode(JsonObject json, TlmTreeNode? parent)
    {
        Json = json;
        Parent = parent;
        Children = json.GetChildren().Select(child => new TlmTreeNode(child, this)).ToList();
    }

    /// <summary>
    /// The tree of a file's root, the root's own transform is where the file's space is
    /// </summary>
    public static TlmTreeNode Of(JsonObject root)
    {
        return new TlmTreeNode(root, null);
    }

    public JsonObject Json { get; }
    public TlmTreeNode? Parent { get; }
    public List<TlmTreeNode> Children { get; }
    public string? Kind => Json.GetKind();
    public string Name => Json.GetString(TlmNodes.NameKey) ?? string.Empty;
    public JsonObject Data => Json.GetData();
    public Boolean HasData => Json[TlmNodes.DataKey] is JsonObject;
    public JsonObject? Mesh => Json[TlmNodes.MeshKey] as JsonObject;

    public Matrix4x4 LocalMatrix => Parent == null ? Matrix4x4.Identity : Json.GetTransform();

    /// <summary>
    /// The node's transform in the file's space
    /// </summary>
    public Matrix4x4 WorldMatrix => Parent == null ? Matrix4x4.Identity : LocalMatrix * Parent.WorldMatrix;

    /// <summary>
    /// The node and every node under it, parents before their children
    /// </summary>
    public IEnumerable<TlmTreeNode> Traverse()
    {
        yield return this;
        foreach (var descendant in Children.SelectMany(child => child.Traverse()))
        {
            yield return descendant;
        }
    }

    /// <summary>
    /// Where the node was moved relative to the given node, which geometry that isn't placed on its own takes over. Null when it
    /// wasn't moved
    /// </summary>
    public Matrix4x4? GetBakedTransform(TlmTreeNode? relativeTo = null)
    {
        // Multiplied up the hierarchy instead of going through the file's space, so nodes that weren't moved stay exactly in place
        var transform = Matrix4x4.Identity;
        for (var current = this; current != relativeTo; current = current.Parent)
        {
            if (current == null)
            {
                var world = WorldMatrix;
                if (relativeTo != null && Matrix4x4.Invert(relativeTo.WorldMatrix, out var inverse))
                {
                    world *= inverse;
                }

                return TlmNodes.IsIdentity(world) ? null : world;
            }

            transform *= current.LocalMatrix;
        }

        return TlmNodes.IsIdentity(transform) ? null : transform;
    }
}
