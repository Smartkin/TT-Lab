using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Splat;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Instance;

// Emitters name the particle system they play, their chunk's own or the default chunk's, so a renamed system takes its emitters along:
// the ones in the same particles as part of the rename's step, and once a renamed system of the default chunk is saved, every other
// chunk's of the version playing it, off the UI thread and logged when done (collision surfaces keep the default chunk's systems by their
// index). A system put into a list gets a name no other system of the version has
internal sealed class ParticleSystemLinks
{
    private readonly PropertyGraph.PropertyGraph _graph;
    // The default chunk's systems renamed since the document was last saved, with the name they had then
    private readonly Dictionary<ParticleSystem, (string Saved, IAsset Owner)> _renamed = new(ReferenceEqualityComparer.Instance);

    public ParticleSystemLinks(PropertyGraph.PropertyGraph graph)
    {
        _graph = graph;
        graph.Changed += OnChanged;
    }

    // The renames of the document made again over the same data, its systems are the same objects
    public void TakeOver(ParticleSystemLinks other)
    {
        foreach (var (system, renamed) in other._renamed)
        {
            _renamed.TryAdd(system, renamed);
        }
    }

    private void OnChanged(PropertyChange change)
    {
        if (change.IsConsequence)
        {
            return;
        }

        if (change is { Kind: PropertyChangeKind.Value, Node: { Target: ParticleSystem system, Metadata.PropertyInfo.Name: nameof(ParticleSystem.Name) } })
        {
            Renamed(change.Node, system, change.OldValue as string ?? string.Empty, change.NewValue as string ?? string.Empty);
        }
        else if (change is { Kind: PropertyChangeKind.Insert, NewValue: ParticleSystem added } && !_graph.IsReplaying)
        {
            NameAdded(change.Node.Children[change.Index], added);
        }
    }

    private void Renamed(PropertyNode node, ParticleSystem system, string from, string to)
    {
        if (DataNodeOf(node) is not { } dataNode || dataNode.GetValue() is not ParticleData data)
        {
            return;
        }

        if (data is DefaultParticleData)
        {
            _renamed.TryAdd(system, (from, data.GetOwner()));
        }

        // Undo and redo put the emitters back with the name
        if (_graph.IsReplaying)
        {
            return;
        }

        // Emitters play the first system of their name, a system of the same name after it had none of them
        var played = data.ParticleSystems.FirstOrDefault(other => ReferenceEquals(other, system) || other.Name == from);
        if (!ReferenceEquals(played, system) || dataNode.FindChild($".{nameof(ParticleData.ParticleInstances)}") is not { } emitters)
        {
            return;
        }

        _graph.BeginConsequences();
        try
        {
            foreach (var emitter in emitters.Children)
            {
                if (emitter.FindChild($".{nameof(ParticleSystemInstance.Name)}") is { } name && name.GetValue() as string == from)
                {
                    name.SetValue(to);
                }
            }
        }
        finally
        {
            _graph.EndConsequences();
        }
    }

    private void NameAdded(PropertyNode element, ParticleSystem added)
    {
        if (DataNodeOf(element) is not { } dataNode || dataNode.GetValue() is not ParticleData data
            || element.FindChild($".{nameof(ParticleSystem.Name)}") is not { } name)
        {
            return;
        }

        var unique = ParticleSystemNames.MakeUnique(added.Name, data.GetOwner(), added);
        if (unique == added.Name)
        {
            return;
        }

        _graph.BeginConsequences();
        try
        {
            name.SetValue(unique);
        }
        finally
        {
            _graph.EndConsequences();
        }
    }

    // The node the particles are the value of, above the system's
    private static PropertyNode? DataNodeOf(PropertyNode node)
    {
        for (var current = node.Parent; current != null; current = current.Parent)
        {
            if (current.GetValue() is ParticleData)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>
    /// The document got saved: the other chunks' emitters of the default chunk's renamed systems follow them
    /// </summary>
    public void Saved()
    {
        var renamed = _renamed.Where(pair => pair.Key.Name != pair.Value.Saved).GroupBy(pair => pair.Value.Owner)
            .Select(group => (Defaults: group.Key, Renames: group.ToDictionary(pair => pair.Value.Saved, pair => pair.Key.Name))).ToList();
        _renamed.Clear();
        foreach (var (defaults, renames) in renamed)
        {
            _ = RenameInOtherChunksAsync(defaults, renames);
        }
    }

    internal static async Task<int> RenameInOtherChunksAsync(IAsset defaults, IReadOnlyDictionary<string, string> renames)
    {
        if (Locator.Current.GetService<ProjectManager>()?.OpenedProject is not TT_Lab.Project.Project project)
        {
            return 0;
        }

        var described = string.Join(", ", renames.Select(rename => $"{rename.Key} to {rename.Value}"));
        Log.WriteLine($"Renaming the emitters of the default chunk's particle systems {described} in every chunk...");
        var watch = Stopwatch.StartNew();
        var chunks = ParticleSystemNames.ParticleAssets(project, project.GetPlatform(defaults.Package)).Where(asset => asset is not DefaultParticles).ToList();
        var emitters = 0;
        var changed = 0;
        try
        {
            // An editor's data is changed on the UI thread, through its document when it has one open
            foreach (var asset in chunks.Where(asset => asset.IsLoaded))
            {
                var count = RenameInLoaded(asset, renames);
                emitters += count;
                changed += count > 0 ? 1 : 0;
            }

            var unloaded = chunks.Where(asset => !asset.IsLoaded).ToList();
            var (fileEmitters, fileChanged) = await Task.Run(() =>
            {
                var (renamedEmitters, changedFiles) = (0, 0);
                foreach (var asset in unloaded)
                {
                    var data = new ParticleData(asset);
                    data.Load(asset.FullDataPath);
                    var count = RenameEmitters(data, renames, (emitter, name) => emitter.Name = name);
                    if (count == 0)
                    {
                        continue;
                    }

                    data.Save(asset.FullDataPath);
                    renamedEmitters += count;
                    changedFiles++;
                }

                return (renamedEmitters, changedFiles);
            });
            emitters += fileEmitters;
            changed += fileChanged;
            Log.WriteLine($"Renamed the default chunk's particle systems {described} in {emitters} emitters of {changed} chunks ({watch.Elapsed.TotalSeconds:F1} s)");
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Renaming the emitters of {described} failed: {ex.Message}", Log.LogType.Error);
        }

        return emitters;
    }

    private static int RenameInLoaded(SerializableAsset asset, IReadOnlyDictionary<string, string> renames)
    {
        if (asset.AssetData is not ParticleData data)
        {
            return 0;
        }

        var nodes = FindEmitterNameNodes(data);
        var count = RenameEmitters(data, renames, (emitter, name) =>
        {
            if (nodes.TryGetValue(emitter, out var node))
            {
                node.SetValue(name);
                return;
            }

            emitter.Name = name;
        });
        if (count > 0 && nodes.Count == 0)
        {
            asset.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        }

        return count;
    }

    // An emitter plays its chunk's own system of its name before the default chunk's
    private static int RenameEmitters(ParticleData data, IReadOnlyDictionary<string, string> renames, Action<ParticleSystemInstance, string> rename)
    {
        var own = data.ParticleSystems.Select(system => system.Name).ToHashSet();
        var count = 0;
        foreach (var emitter in data.ParticleInstances.ToList())
        {
            if (own.Contains(emitter.Name) || !renames.TryGetValue(emitter.Name, out var name))
            {
                continue;
            }

            rename(emitter, name);
            count++;
        }

        return count;
    }

    // The name nodes of the data's emitters in the open documents, a change there is the document's to save and undo
    private static Dictionary<ParticleSystemInstance, PropertyNode> FindEmitterNameNodes(ParticleData data)
    {
        var nodes = new Dictionary<ParticleSystemInstance, PropertyNode>(ReferenceEqualityComparer.Instance);
        if (Locator.Current.GetService<EditorsViewModel>() is not { } editors)
        {
            return nodes;
        }

        var emitters = data.ParticleInstances.ToHashSet(ReferenceEqualityComparer.Instance);
        var documents = editors.ScenesEditorsViewModel.Tabs.Concat(editors.ResourcesEditorsViewModel.Tabs).Select(tab => tab.Document).OfType<DocumentViewModel>();
        foreach (var document in documents)
        {
            Collect(document.PropertyGraph.Root);
            if (nodes.Count > 0)
            {
                break;
            }
        }

        return nodes;

        void Collect(PropertyNode node)
        {
            if (node is { Target: ParticleSystemInstance emitter, Metadata.PropertyInfo.Name: nameof(ParticleSystemInstance.Name) } && emitters.Contains(emitter))
            {
                nodes.TryAdd(emitter, node);
                return;
            }

            foreach (var child in node.Children)
            {
                Collect(child);
            }
        }
    }
}
