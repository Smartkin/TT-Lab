using System;
using System.Collections.Generic;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public class PropertyGraph
{
    public event Action<PropertyChange>? Changed;

    /// <summary>
    /// A node's <see cref="PropertyNode.Presentation"/> changed, the composites showing its editor apply it
    /// </summary>
    public event Action<PropertyNode>? PresentationChanged;
    public PropertyNode Root { get; }
    public PropertyGraphTracker Tracker { get; }

    /// <summary>
    /// Views of the assets a chunk's document shares with other chunks, other documents don't have any
    /// </summary>
    public TT_Lab.Assets.ChunkOverrideSession? Overrides => Tracker.Overrides;
    
    /// <summary>
    /// Goes up whenever nodes come, go or move, the nodes' paths are worked out again after
    /// </summary>
    public int StructureVersion { get; private set; }

    internal void OnPresentationChanged(PropertyNode node) => PresentationChanged?.Invoke(node);

    public PropertyGraph(PropertyNode root, PropertyGraphTracker tracker)
    {
        Tracker = tracker;
        Root = root;
        Index(root);
    }

    // How deep linked fields are in following a change
    private int _consequenceDepth;

    /// <summary>
    /// Whether undo or redo is putting values back, what linked fields wrote with them included, so linked fields only set up what
    /// editors show (<see cref="Attributes.IFieldChange.Linked"/>)
    /// </summary>
    public bool IsReplaying { get; internal set; }

    /// <summary>
    /// The node values are being pasted into: what's under it gets the copied values as they were copied, so linked fields under it
    /// only set up what editors show, while the ones following it from outside work their values out the way an edit does
    /// </summary>
    internal PropertyNode? PasteRoot { get; set; }

    internal bool IsPastedOver(PropertyNode node)
    {
        if (PasteRoot == null)
        {
            return false;
        }

        for (var parent = node.Parent; parent != null; parent = parent.Parent)
        {
            if (parent == PasteRoot)
            {
                return true;
            }
        }

        return false;
    }

    internal void NotifyChange(PropertyNode node, object? oldValue, object? newValue, PropertyChangeKind kind = PropertyChangeKind.Value, int index = -1)
    {
        Changed?.Invoke(new PropertyChange(node, oldValue, newValue, kind, index, _consequenceDepth > 0));
    }

    internal void BeginConsequences() => _consequenceDepth++;

    internal void EndConsequences() => _consequenceDepth--;

    // A node taken out of the graph
    internal void Deindex(PropertyNode node)
    {
        StructureVersion++;
    }

    // A node (and everything under it) put into the graph, with its linked fields: nodes made later (a placed trigger, a link pointed
    // elsewhere, resources put back by undo) had theirs follow nothing
    internal void Index(PropertyNode node)
    {
        Attach(node);
        StructureVersion++;
        node.InitPropertyLinks();
    }

    private void Attach(PropertyNode node)
    {
        node.SetGraph(this);
        foreach (var child in node.Children)
        {
            Attach(child);
        }
    }

    public PropertyNode? this[string path] => Find(path);

    /// <summary>
    /// The node at the path, walked from the root a segment at a time
    /// </summary>
    public PropertyNode? Find(string path)
    {
        var rootPath = Root.Path;
        if (!path.StartsWith(rootPath, StringComparison.Ordinal))
        {
            return null;
        }

        var node = Root;
        var position = rootPath.Length;
        while (position < path.Length)
        {
            int end;
            if (path[position] == '[')
            {
                end = path.IndexOf(']', position);
                if (end < 0)
                {
                    return null;
                }

                end++;
            }
            else if (path[position] == '.')
            {
                end = position + 1;
                while (end < path.Length && path[end] != '.' && path[end] != '[')
                {
                    end++;
                }
            }
            else
            {
                return null;
            }

            node = node.FindChild(path[position..end]);
            if (node == null)
            {
                return null;
            }

            position = end;
        }

        return node;
    }
}

public enum PropertyChangeKind
{
    Value,
    // An element put into the list at the index, its value is the new value
    Insert,
    // The element at the index taken out of the list, its value is the old value
    Remove,
}

/// <param name="IsConsequence">Made by a linked field following another change (a trigger's kind following its header), part of that
/// change's step</param>
public record PropertyChange(PropertyNode Node, object? OldValue, object? NewValue, PropertyChangeKind Kind = PropertyChangeKind.Value, int Index = -1, bool IsConsequence = false);