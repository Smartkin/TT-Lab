using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;

namespace TT_Lab.Rendering.Services;

/// <summary>
/// What a context made of assets, by their URIs (<see cref="MeshService"/>'s meshes), handed out until it's forgotten and let go of once
/// nothing draws it any more. Forgetting only took it out of the dictionary: a model made again (its data replaced whole, that undone or
/// redone) kept every copy of its meshes on the GPU until the tab closed
/// </summary>
internal sealed class RenderCache<T> where T : class
{
    private readonly Dictionary<LabURI, T?> _made = [];
    // Forgotten, waiting until nothing draws them
    private readonly List<T> _forgotten = [];
    // Forgotten once nothing draws them: the parts of data replaced whole, whose URIs are of their content. The ones the new data still
    // has are drawn again right away and stay
    private readonly HashSet<LabURI> _whenUndrawn = [];

    public bool TryGet(LabURI uri, out T? made) => _made.TryGetValue(uri, out made);

    public void Add(LabURI uri, T? made) => _made[uri] = made;

    /// <summary>
    /// Whether anything waits to be let go of
    /// </summary>
    public bool HasForgotten => _forgotten.Count > 0 || _whenUndrawn.Count > 0;

    /// <summary>
    /// Goes up whenever more is forgotten: what was forgotten before only gets undrawn when something stops being drawn
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// Made again the next time they're asked for, what was made of them before is handed back once nothing draws it (<see cref="TakeUndrawn"/>)
    /// </summary>
    public IReadOnlyList<T> Forget(IEnumerable<LabURI> uris)
    {
        var forgotten = new List<T>();
        foreach (var uri in uris)
        {
            _whenUndrawn.Remove(uri);
            if (_made.Remove(uri, out var made) && made != null)
            {
                forgotten.Add(made);
            }
        }

        if (forgotten.Count > 0)
        {
            _forgotten.AddRange(forgotten);
            Version++;
        }

        return forgotten;
    }

    /// <summary>
    /// Kept while they're drawn and forgotten as soon as they aren't
    /// </summary>
    public void ForgetWhenUndrawn(IEnumerable<LabURI> uris)
    {
        var count = _whenUndrawn.Count;
        _whenUndrawn.UnionWith(uris.Where(_made.ContainsKey));
        if (_whenUndrawn.Count != count)
        {
            Version++;
        }
    }

    /// <summary>
    /// What's forgotten and no longer drawn, taken out to be released
    /// </summary>
    public List<T> TakeUndrawn(Func<T, bool> isDrawn)
    {
        var undrawn = new List<T>();
        if (!HasForgotten)
        {
            return undrawn;
        }

        foreach (var uri in _whenUndrawn.ToList())
        {
            if (!_made.TryGetValue(uri, out var made) || made == null)
            {
                _whenUndrawn.Remove(uri);
                continue;
            }

            if (isDrawn(made))
            {
                continue;
            }

            _whenUndrawn.Remove(uri);
            _made.Remove(uri);
            undrawn.Add(made);
        }

        var stillDrawn = new List<T>();
        foreach (var made in _forgotten)
        {
            (isDrawn(made) ? stillDrawn : undrawn).Add(made);
        }

        _forgotten.Clear();
        _forgotten.AddRange(stillDrawn);
        return undrawn;
    }
}
