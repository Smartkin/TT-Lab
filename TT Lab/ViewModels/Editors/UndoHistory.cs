using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors;

/// <summary>
/// What a document's editors changed, as a tree to undo and redo along. Every editor keeps its own
/// </summary>
/// <remarks>
/// Changing something after undoing starts a branch next to the one undone, which stays to go back to. Steps are kept by the paths of
/// what they changed, nodes get made again when links and lists change. Replacing a whole asset's data (a texture's picture, a sound)
/// can't be undone, the history starts over after it
/// </remarks>
public sealed class UndoHistory
{
    // Changes of one value this close to each other, like typing or dragging, are one step
    private static readonly TimeSpan MergeTime = TimeSpan.FromSeconds(1);
    private const int MaxEntries = 2000;

    /// <summary>
    /// A state of the document, reached by the step from its parent's
    /// </summary>
    public sealed class Entry
    {
        internal Entry(Entry? parent, Step? step, string description, int number, int branch)
        {
            Parent = parent;
            Step = step;
            Description = description;
            Number = number;
            Branch = branch;
        }

        public Entry? Parent { get; internal set; }

        public List<Entry> Children { get; } = [];

        internal Step? Step { get; set; }

        public string Description { get; internal set; }

        public DateTime Time { get; internal set; } = DateTime.Now;

        /// <summary>
        /// Order the entries got made in
        /// </summary>
        public int Number { get; }

        /// <summary>
        /// Entries continuing their parent's first branch are on its branch, later ones start new branches
        /// </summary>
        public int Branch { get; }

        // The branch redoing goes along, the one last taken
        internal Entry? RedoChild { get; set; }

        // Typing or dragging on changes a single change's step, a group's step is done
        internal bool IsOpen { get; set; }

        public bool IsAncestorOf(Entry entry)
        {
            for (var current = entry.Parent; current != null; current = current.Parent)
            {
                if (current == this)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private readonly PropertyGraph.PropertyGraph _graph;
    private GroupStep? _group;
    private string? _groupDescription;
    private int _groupDepth;
    private Entry? _saved;
    private int _entries;
    private int _branches;

    public event Action? Changed;

    public UndoHistory(PropertyGraph.PropertyGraph graph)
    {
        _graph = graph;
        Root = new Entry(null, null, "Opened", 0, 0);
        Current = Root;
        _saved = Root;
    }

    public Entry Root { get; private set; }

    public Entry Current { get; private set; }

    /// <summary>
    /// Whether the changes being made are an undo or a redo, which aren't steps of their own
    /// </summary>
    public bool IsApplying { get; private set; }

    public bool CanUndo => Current != Root;

    public bool CanRedo => Current.Children.Count > 0;

    /// <summary>
    /// Whether the document is the way it was saved
    /// </summary>
    public bool IsAtSavePoint => Current == _saved;

    /// <summary>
    /// Where the document was saved, none when it can't be gotten back to
    /// </summary>
    public Entry? Saved => _saved;

    public void Record(PropertyChange change)
    {
        if (IsApplying)
        {
            return;
        }

        if (!CanUndoChange(change))
        {
            Clear();
            return;
        }

        var step = change.Kind switch
        {
            PropertyChangeKind.Insert => new InsertStep(change.Node.Path, change.Index, change.NewValue),
            PropertyChangeKind.Remove => (Step)new RemoveStep(change.Node.Path, change.Index, change.OldValue),
            _ => new ValueStep(change.Node.Path, change.OldValue, change.NewValue),
        };
        if (change.IsConsequence)
        {
            AddConsequence(step);
            return;
        }

        Add(step, Describe(change), true);
    }

    // What linked fields write following a change is part of its step, one edit of a trigger's header was a step for each of its fields.
    // Undo and redo put it back as it was instead of making it again from the value it follows (the graph runs no linked fields while
    // they apply): a link's object matrix is its chunk matrix's inverse only up to rounding, undoing gave other floats than the game's
    private void AddConsequence(Step step)
    {
        if (_group != null)
        {
            if (!_group.Absorb(step))
            {
                _group.Steps.Add(step);
            }

            return;
        }

        // The change it follows couldn't be undone and started the history over
        if (Current.Step == null)
        {
            return;
        }

        if (Current.Step is not GroupStep group)
        {
            group = new GroupStep();
            group.Steps.Add(Current.Step);
            Current.Step = group;
        }

        if (!group.Absorb(step))
        {
            group.Steps.Add(step);
        }
    }

    // A value of another type is undone too, its node makes its children again for whichever type it has (PropertyNode.SetValue)
    private static bool CanUndoChange(PropertyChange change)
    {
        return change.OldValue is not AbstractAssetData && change.NewValue is not AbstractAssetData;
    }

    private void Add(Step step, string description, bool isOpen)
    {
        if (_group != null)
        {
            _groupDescription ??= description;
            if (!_group.Merge(step))
            {
                _group.Steps.Add(step);
            }

            return;
        }

        // Typing or dragging on keeps changing the last step, unless it got saved or undone to since. What linked fields wrote goes along
        // with the value they follow
        if (Current.IsOpen && step is ValueStep value && Current != _saved && Current.Children.Count == 0
            && (Current.Step is ValueStep previous && previous.Merge(value) || Current.Step is GroupStep { Steps: [ValueStep first, ..] } && first.Merge(value)))
        {
            Current.Description = description;
            Current.Time = DateTime.Now;
            Changed?.Invoke();
            return;
        }

        var branch = Current.Children.Count == 0 ? Current.Branch : ++_branches;
        var entry = new Entry(Current, step, description, ++_entries, branch) { IsOpen = isOpen };
        Current.Children.Add(entry);
        Current.RedoChild = entry;
        Current = entry;
        Trim();
        Changed?.Invoke();
    }

    // The oldest branches away from where the document is go first, then the oldest steps
    private void Trim()
    {
        while (Count(Root) > MaxEntries)
        {
            var old = Root.Children.FirstOrDefault(child => child != Current && !child.IsAncestorOf(Current));
            if (old != null)
            {
                Root.Children.Remove(old);
                continue;
            }

            var next = Root.Children.Single();
            next.Parent = null;
            next.Step = null;
            if (_saved == Root)
            {
                _saved = null;
            }

            Root = next;
        }
    }

    private static int Count(Entry entry) => 1 + entry.Children.Sum(Count);

    /// <summary>
    /// Makes everything changed until it's disposed one step, like placing an instance and moving it to the cursor
    /// </summary>
    /// <summary>
    /// Ends the step typing or dragging keeps changing, so the next change starts a new one
    /// </summary>
    public void CloseStep()
    {
        Current.IsOpen = false;
    }

    public IDisposable BeginGroup(string? description = null)
    {
        if (_groupDepth++ == 0)
        {
            _group = new GroupStep();
            _groupDescription = description;
        }

        return new GroupEnd(this);
    }

    private void EndGroup()
    {
        if (--_groupDepth > 0 || _group == null)
        {
            return;
        }

        var group = _group;
        var description = _groupDescription;
        _group = null;
        _groupDescription = null;
        // Like a drag given up on, which put back what it started from
        group.Steps.RemoveAll(step => step is ValueStep { IsUnchanged: true });
        if (group.Steps.Count > 0)
        {
            Add(group.Steps.Count == 1 ? group.Steps[0] : group, description ?? "Changes", false);
        }
    }

    public void Undo()
    {
        if (Current.Parent == null)
        {
            return;
        }

        var entry = Current;
        Apply(() => entry.Step!.Undo(_graph));
        entry.Parent!.RedoChild = entry;
        Current = entry.Parent;
        Changed?.Invoke();
    }

    public void Redo()
    {
        var entry = Current.RedoChild ?? Current.Children.LastOrDefault();
        if (entry == null)
        {
            return;
        }

        Apply(() => entry.Step!.Redo(_graph));
        // A change after a redo is a step of its own, it would otherwise change the redone one
        entry.IsOpen = false;
        Current = entry;
        Changed?.Invoke();
    }

    /// <summary>
    /// Undoes back to where the entry's branch left the current one and redoes along it to the entry
    /// </summary>
    public void GoTo(Entry target)
    {
        while (Current != target && !Current.IsAncestorOf(target) && Current.Parent != null)
        {
            Undo();
        }

        var path = new Stack<Entry>();
        for (var entry = target; entry != null && entry != Current; entry = entry.Parent)
        {
            path.Push(entry);
        }

        while (path.Count > 0)
        {
            Current.RedoChild = path.Pop();
            Redo();
        }
    }

    private void Apply(Action apply)
    {
        IsApplying = true;
        _graph.IsReplaying = true;
        try
        {
            apply();
        }
        finally
        {
            IsApplying = false;
            _graph.IsReplaying = false;
        }
    }

    public void MarkSaved()
    {
        _saved = Current;
        Changed?.Invoke();
    }

    // What got changed can't be undone, so the document can't get back to how it was saved either
    public void Clear(string description = "Couldn't be undone past here")
    {
        Root = new Entry(null, null, description, ++_entries, 0);
        Current = Root;
        _saved = null;
        _branches = 0;
        Changed?.Invoke();
    }

    /// <summary>
    /// Whether a step anywhere in the history changes something at a path the predicate takes
    /// </summary>
    internal bool Touches(Func<string, bool> path)
    {
        var entries = new Stack<Entry>([Root]);
        while (entries.TryPop(out var entry))
        {
            if (entry.Step?.Paths.Any(path) == true)
            {
                return true;
            }

            foreach (var child in entry.Children)
            {
                entries.Push(child);
            }
        }

        return false;
    }

    /// <summary>
    /// Takes the other history's entries, the document they were made in made again over the same data. Steps are kept by their paths,
    /// which find the same values in the new graph
    /// </summary>
    internal void Adopt(UndoHistory other)
    {
        Root = other.Root;
        Current = other.Current;
        _saved = other._saved;
        _entries = other._entries;
        _branches = other._branches;
        Current.IsOpen = false;
        Changed?.Invoke();
    }

    // What the change is to the one looking at the history, the asset it's in and what of it changed
    private static string Describe(PropertyChange change)
    {
        var node = change.Node;
        var owner = node;
        while (owner.Parent != null && owner.Target is not IAsset)
        {
            owner = owner.Parent;
        }

        var property = node.Path.Length > owner.Path.Length ? node.Path[owner.Path.Length..].TrimStart('.') : string.Empty;
        property = property.Replace("AssetData.", string.Empty).Replace("[data]", " › ").Replace(".", " › ");
        var what = owner.Target is IAsset asset ? string.IsNullOrEmpty(property) ? asset.Alias : $"{asset.Alias} › {property}" : property;
        return change.Kind switch
        {
            PropertyChangeKind.Insert => $"Added {what} [{change.Index}]",
            PropertyChangeKind.Remove => $"Removed {what} [{change.Index}]",
            _ => $"{what} = {Short(change.NewValue)}",
        };
    }

    private static string Short(object? value)
    {
        var text = value switch
        {
            null => "nothing",
            LabURI uri => uri == LabURI.Empty ? "nothing" : AssetManager.Get().DoesAssetExist(uri) ? AssetManager.Get().GetAsset(uri).Alias : uri.ToString(),
            string text1 => $"\"{text1.ReplaceLineEndings(" ")}\"",
            // The game's vectors have no text of their own, a whole one set (the scenery's bounds typed into) showed its type's name
            Vector2 vector => $"({vector.X}, {vector.Y})",
            Vector3 vector => $"({vector.X}, {vector.Y}, {vector.Z})",
            Vector4 vector => $"({vector.X}, {vector.Y}, {vector.Z}, {vector.W})",
            _ => value.ToString() ?? string.Empty,
        };
        return text.Length > 40 ? $"{text[..40]}…" : text;
    }

    private sealed class GroupEnd(UndoHistory history) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            history.EndGroup();
        }
    }

    internal abstract class Step
    {
        public abstract void Undo(PropertyGraph.PropertyGraph graph);

        public abstract void Redo(PropertyGraph.PropertyGraph graph);

        // Of what the step changes, lists for what goes into or out of them
        public abstract IEnumerable<string> Paths { get; }
    }

    private sealed class ValueStep(string path, object? oldValue, object? newValue) : Step
    {
        private object? _newValue = newValue;
        private DateTime _time = DateTime.UtcNow;

        public bool Merge(ValueStep next)
        {
            if (next._path != _path || next._time - _time > MergeTime)
            {
                return false;
            }

            _newValue = next._newValue;
            _time = next._time;
            return true;
        }

        private readonly string _path = path;

        public bool IsUnchanged => PropertyNode.IsSameValue(oldValue, _newValue);

        // Another change of the same value within the step, whenever it came
        public bool Absorb(ValueStep next)
        {
            if (next._path != _path)
            {
                return false;
            }

            _newValue = next._newValue;
            return true;
        }

        public override void Undo(PropertyGraph.PropertyGraph graph) => graph.Find(_path)?.SetValue(oldValue);

        public override void Redo(PropertyGraph.PropertyGraph graph) => graph.Find(_path)?.SetValue(_newValue);

        public override IEnumerable<string> Paths => [_path];
    }

    private sealed class InsertStep(string list, int index, object? value) : Step
    {
        public override void Undo(PropertyGraph.PropertyGraph graph) => Remove(graph, list, index);

        public override void Redo(PropertyGraph.PropertyGraph graph) => graph.Find(list)?.InsertElement(index, value!);

        public override IEnumerable<string> Paths => [list];
    }

    private sealed class RemoveStep(string list, int index, object? value) : Step
    {
        public override void Undo(PropertyGraph.PropertyGraph graph) => graph.Find(list)?.InsertElement(index, value!);

        public override void Redo(PropertyGraph.PropertyGraph graph) => Remove(graph, list, index);

        public override IEnumerable<string> Paths => [list];
    }

    private static void Remove(PropertyGraph.PropertyGraph graph, string list, int index)
    {
        if (graph.Find(list) is { } node && index < node.Children.Count)
        {
            node.RemoveElement(node.Children[index]);
        }
    }

    private sealed class GroupStep : Step
    {
        public List<Step> Steps { get; } = [];

        // Moving what the group placed keeps being part of placing it
        public bool Merge(Step step)
        {
            return step is ValueStep value && Steps.LastOrDefault() is ValueStep last && last.Merge(value);
        }

        // A value the group changed already changes again
        public bool Absorb(Step step)
        {
            return step is ValueStep value && Steps.OfType<ValueStep>().Any(existing => existing.Absorb(value));
        }

        public override void Undo(PropertyGraph.PropertyGraph graph)
        {
            for (var i = Steps.Count - 1; i >= 0; i--)
            {
                Steps[i].Undo(graph);
            }
        }

        public override void Redo(PropertyGraph.PropertyGraph graph)
        {
            foreach (var step in Steps)
            {
                step.Redo(graph);
            }
        }

        public override IEnumerable<string> Paths => Steps.SelectMany(step => step.Paths);
    }
}
