using System;
using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.ViewModels.Editors.PropertyGraph;

public class PropertyGraph
{
    public event Action<PropertyChange>? Changed;

    /// <summary>
    /// A node's <see cref="PropertyNode.Presentation"/> changed, the composites showing its editor apply it
    /// </summary>
    public event Action<PropertyNode>? PresentationChanged;

    /// <summary>
    /// A chunk's view took what another editor changed in its asset (<see cref="TT_Lab.Assets.ChunkOverrideSession.Follow"/>): its nodes
    /// show it already, what's drawn of it follows. No step of the history and nothing to save
    /// </summary>
    public event Action<TT_Lab.Assets.IAsset>? Followed;
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

    /// <summary>
    /// The values of the asset (a chunk's view of it) at the paths in its JSON changed outside the document's editors: the nodes of them
    /// under every link to the asset are made again for them and their editors show them
    /// </summary>
    internal void Follow(TT_Lab.Assets.IAsset asset, IReadOnlyList<string> paths)
    {
        ShowAgain(DataNodesOf(asset), paths);
        Followed?.Invoke(asset);
    }

    // An asset is under each of its links, an object under every instance of it: changed under one of them, its nodes under the others
    // show the change. Their links led to what the asset linked before
    private void ShowUnderOtherLinks(PropertyNode changed)
    {
        var data = changed;
        while (data.Parent != null && data.Segment != "[data]")
        {
            data = data.Parent;
        }

        if (data == changed || data.Target is not TT_Lab.Assets.IAsset asset)
        {
            return;
        }

        var others = DataNodesOf(asset).Where(node => node != data).ToList();
        if (others.Count == 0)
        {
            return;
        }

        // The change itself is done, whatever happens to the other links' nodes it gets told
        try
        {
            ShowAgain(others, [changed.Path[data.Path.Length..]]);
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Couldn't show the change of {changed.Path} under the other links to {asset.Name}: {ex.Message}", Log.LogType.Error);
        }
    }

    // The nodes of the values at the paths under the data nodes are made again for them and their editors show them, linked fields only
    // setting up what editors show: the values are there already
    private void ShowAgain(IEnumerable<PropertyNode> dataNodes, IReadOnlyList<string> paths)
    {
        var nodes = new List<PropertyNode>();
        foreach (var data in dataNodes)
        {
            foreach (var path in paths)
            {
                if (FindFollowed(data, path) is { } node && !nodes.Contains(node))
                {
                    nodes.Add(node);
                }
            }
        }

        if (nodes.Count == 0)
        {
            return;
        }

        var wasReplaying = IsReplaying;
        IsReplaying = true;
        try
        {
            // What's under a node made again is made with it
            foreach (var node in nodes.Where(node => !nodes.Any(other => other != node && IsUnder(node, other))))
            {
                node.FollowValue();
            }
        }
        finally
        {
            IsReplaying = wasReplaying;
        }
    }

    // The node of the value at the JSON path, or the one showing it: a custom editor shows all of its value, and an object that was null
    // gets its nodes under the one it's in. Values nothing shows have none
    private static PropertyNode? FindFollowed(PropertyNode data, string path)
    {
        var node = data;
        var segments = TT_Lab.Assets.AssetOverrides.Segments(path);
        for (var i = 0; i < segments.Count; i++)
        {
            var child = node.FindChild(segments[i]);
            if (child != null)
            {
                node = child;
                continue;
            }

            if (node == data)
            {
                return null;
            }

            var isNodeOfItsOwn = i == segments.Count - 1 && segments[i].StartsWith('.') && node.GetValue() is { } value &&
                                 DocumentMetadataCache.Get(value.GetType()).Properties.Any(property => property.PropertyInfo.Name == segments[i][1..]);
            return isNodeOfItsOwn || node.FindChild("[custom_editor]") != null ? node : null;
        }

        return node == data ? null : node;
    }

    private static bool IsUnder(PropertyNode node, PropertyNode ancestor)
    {
        for (var parent = node.Parent; parent != null; parent = parent.Parent)
        {
            if (parent == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The data nodes of the links to the asset (a chunk's view of a shared one) that are in the graph
    /// </summary>
    internal IReadOnlyList<PropertyNode> DataNodesOf(TT_Lab.Assets.IAsset asset) => Tracker.DataNodesOf(asset, Contains);

    /// <summary>
    /// Whether the node is in the graph: nodes taken out keep their parent
    /// </summary>
    public bool Contains(PropertyNode node)
    {
        var current = node;
        while (current.Parent is { } parent)
        {
            var isChild = (current.Index is { } index && index < parent.Children.Count && parent.Children[index] == current) || parent.Children.Contains(current);
            if (!isChild)
            {
                return false;
            }

            current = parent;
        }

        return current == Root;
    }

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
        ShowUnderOtherLinks(node);
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