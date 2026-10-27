using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
// the ones in the same particles as part of the rename's step, and once the default chunk's particles are saved with a system renamed,
// whichever editor saves them, every other chunk's of the version playing it, off the UI thread and logged when done (collision surfaces
// keep the default chunk's systems by their index). A system put into a list gets a name no other system of its chunk has, nor a default
// system's for a level's: it would be played in that one's place. A pasted one keeps its name, a level's own version of a default system
internal sealed class ParticleSystemLinks
{
    private readonly PropertyGraph.PropertyGraph _graph;

    // The default chunk's particles' systems with the names their saved file has, kept at the data's first change of their names or list,
    // whichever document made it (the particles' editor and the default chunk's scene share the data), and kept as any save writes them: a
    // rename was followed by the renaming document's save alone, the other one's wrote the new name and followed nothing. Kept with the data,
    // which a document made again over it shares
    private static readonly ConditionalWeakTable<DefaultParticleData, SavedSystem[]> SavedSystems = new();

    private readonly record struct SavedSystem(ParticleSystem System, string Name);

    // What saves renamed that the document saving them hasn't followed yet (FollowSaved)
    private static readonly List<(IAsset Defaults, IReadOnlyDictionary<string, string> Renames)> SavedRenames = [];

    // The follows that haven't ended, which closing the project or TT Lab waits for (EditorsViewModel.SaveOrDiscardUnsavedChanges)
    private static readonly List<Task> Follows = [];

    // A follow's files are renamed after the follow's before it, which can have given them the name it renames
    private static Task _renamingFiles = Task.CompletedTask;

    static ParticleSystemLinks()
    {
        DefaultParticles.DataSaved += Saved;
    }

    public ParticleSystemLinks(PropertyGraph.PropertyGraph graph)
    {
        _graph = graph;
        graph.Changed += OnChanged;
    }

    // The systems renamed since the data was last saved (what's kept of its file against what's written now) are followed once the document
    // saving them is done. Emitters play the first system of a name, a later one of the name renamed takes nothing along. Data whose systems
    // never changed is saved as it is
    private static void Saved(DefaultParticleData data, IReadOnlyList<(ParticleSystem System, string Name)> systems)
    {
        if (!SavedSystems.TryGetValue(data, out var before))
        {
            return;
        }

        SavedSystems.AddOrUpdate(data, systems.Select(saved => new SavedSystem(saved.System, saved.Name)).ToArray());
        var written = new Dictionary<ParticleSystem, string>(ReferenceEqualityComparer.Instance);
        foreach (var (system, name) in systems)
        {
            written.TryAdd(system, name);
        }

        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (system, name) in before)
        {
            if (names.Add(name) && written.TryGetValue(system, out var now) && now != name)
            {
                renames[name] = now;
            }
        }

        if (renames.Count == 0)
        {
            return;
        }

        lock (SavedRenames)
        {
            SavedRenames.Add((data.GetOwner(), renames));
        }
    }

    /// <summary>
    /// A document got saved: the other chunks' emitters of the default chunk's systems its saves renamed follow them, the loaded chunks' right
    /// away and the files of the others in the background (<see cref="WhenFollowed"/>)
    /// </summary>
    public static void FollowSaved()
    {
        List<(IAsset Defaults, IReadOnlyDictionary<string, string> Renames)> saved;
        lock (SavedRenames)
        {
            saved = [..SavedRenames];
            SavedRenames.Clear();
        }

        foreach (var (defaults, renames) in saved)
        {
            var follow = RenameInOtherChunksAsync(defaults, renames);
            lock (Follows)
            {
                Follows.RemoveAll(task => task.IsCompleted);
                Follows.Add(follow);
            }
        }
    }

    /// <summary>
    /// Whether a save's follow hasn't ended
    /// </summary>
    public static bool IsFollowing
    {
        get
        {
            lock (Follows)
            {
                return Follows.Any(task => !task.IsCompleted);
            }
        }
    }

    /// <summary>
    /// Ends once the follows of every save so far have: the files they rename written. A follow logs what fails and never throws
    /// </summary>
    public static Task WhenFollowed()
    {
        lock (Follows)
        {
            Follows.RemoveAll(task => task.IsCompleted);
            return Task.WhenAll(Follows.ToArray());
        }
    }

    private void OnChanged(PropertyChange change)
    {
        KeepSavedSystems(change);
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

    // The first change of a default chunk's particles' systems keeps them as they were before it, an undo's too: as they were saved, which
    // what's kept stays from then on (a save keeps them as it saved them)
    private static void KeepSavedSystems(PropertyChange change)
    {
        var data = change switch
        {
            { Kind: PropertyChangeKind.Value, Node: { Target: ParticleSystem, Metadata.PropertyInfo.Name: nameof(ParticleSystem.Name) } } => DataNodeOf(change.Node)?.GetValue(),
            { Kind: PropertyChangeKind.Insert, NewValue: ParticleSystem } or { Kind: PropertyChangeKind.Remove, OldValue: ParticleSystem } => change.Node.Parent?.GetValue(),
            _ => null
        };
        if (data is not DefaultParticleData particles || SavedSystems.TryGetValue(particles, out _))
        {
            return;
        }

        var saved = particles.ParticleSystems.Select(system => new SavedSystem(system, system.Name)).ToList();
        switch (change.Kind)
        {
            case PropertyChangeKind.Insert:
                saved.RemoveAt(change.Index);
                break;
            case PropertyChangeKind.Remove:
                var taken = (ParticleSystem)change.OldValue!;
                saved.Insert(change.Index, new SavedSystem(taken, taken.Name));
                break;
            default:
                var renamed = saved.FindIndex(entry => ReferenceEquals(entry.System, change.Node.Target));
                if (renamed >= 0)
                {
                    saved[renamed] = saved[renamed] with { Name = change.OldValue as string ?? string.Empty };
                }

                break;
        }

        SavedSystems.AddOrUpdate(particles, saved.ToArray());
    }

    private void Renamed(PropertyNode node, ParticleSystem system, string from, string to)
    {
        if (DataNodeOf(node) is not { } dataNode || dataNode.GetValue() is not ParticleData data)
        {
            return;
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
        // A paste names what it puts in itself
        if (_graph.IsPastedOver(element) || DataNodeOf(element) is not { } dataNode || dataNode.GetValue() is not ParticleData data
            || element.FindChild($".{nameof(ParticleSystem.Name)}") is not { } name)
        {
            return;
        }

        var unique = ParticleSystemNames.MakeUnique(added.Name, data.GetOwner(), added, avoidsDefaults: true);
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

    internal static async Task<int> RenameInOtherChunksAsync(IAsset defaults, IReadOnlyDictionary<string, string> renames)
    {
        // A rename saved outside a document's save (deleting what the particles link fixes and saves them) waits for the next document's save,
        // which leaves it once its project closed: another project's chunks of the same packages would follow it
        if (Locator.Current.GetService<ProjectManager>()?.OpenedProject is not TT_Lab.Project.Project project || !project.AssetManager.DoesAssetExist(defaults.URI)
            || !ReferenceEquals(project.AssetManager.GetAsset(defaults.URI), defaults))
        {
            return 0;
        }

        var described = string.Join(", ", renames.Select(rename => $"{rename.Key} to {rename.Value}"));
        Log.WriteLine($"Renaming the emitters of the default chunk's particle systems {described} in every chunk...");
        var watch = Stopwatch.StartNew();
        var emitters = 0;
        var changed = 0;
        // Chunks with a system of their own of a new name, whose renamed emitters play that one in the default chunk's place
        var playOwn = new List<(string Chunk, string Name)>();
        try
        {
            var chunks = ParticleSystemNames.ParticleAssets(project, project.GetPlatform(defaults.Package)).Where(asset => asset is not DefaultParticles).ToList();
            // An editor's data is changed on the UI thread, through its document when it has one open
            foreach (var asset in chunks.Where(asset => asset.IsLoaded))
            {
                var count = RenameInLoaded(asset, renames, name => playOwn.Add((asset.Alias, name)));
                emitters += count;
                changed += count > 0 ? 1 : 0;
            }

            // The files' paths are taken here, nothing in the background looks at the opened project: the project closed (or another opened)
            // while the files were written, and the rest failed (or went into the other project's files of the same paths)
            var unloaded = chunks.Where(asset => !asset.IsLoaded).Select(asset => new ChunkFile(asset, asset.Alias, asset.FullDataPath)).ToList();
            var previous = _renamingFiles;
            var renaming = Task.Run(async () =>
            {
                await previous;
                return RenameInFiles(unloaded, renames);
            });
            _renamingFiles = renaming.ContinueWith(_ => { }, TaskScheduler.Default);
            var (fileEmitters, fileChanged, filesPlayingOwn) = await renaming;
            emitters += fileEmitters;
            changed += fileChanged;
            playOwn.AddRange(filesPlayingOwn);
            Log.WriteLine($"Renamed the default chunk's particle systems {described} in {emitters} emitters of {changed} chunks ({watch.Elapsed.TotalSeconds:F1} s)");
            foreach (var chunk in playOwn.Distinct().GroupBy(entry => entry.Chunk))
            {
                Log.WriteLine($"{chunk.Key} has particle systems of its own named {string.Join(", ", chunk.Select(entry => entry.Name))}: its renamed emitters play those " +
                              "in place of the default chunk's", Log.LogType.Warning);
            }
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Renaming the emitters of {described} failed: {ex.Message}", Log.LogType.Error);
        }

        return emitters;
    }

    private readonly record struct ChunkFile(SerializableAsset Asset, string Alias, string Path);

    private static (int Emitters, int Files, List<(string Chunk, string Name)> PlayOwn) RenameInFiles(IReadOnlyList<ChunkFile> files, IReadOnlyDictionary<string, string> renames)
    {
        var (renamedEmitters, changedFiles, ownPlayed) = (0, 0, new List<(string Chunk, string Name)>());
        foreach (var file in files)
        {
            var data = new ParticleData(file.Asset);
            data.PopulateFrom(File.ReadAllText(file.Path));
            var count = RenameEmitters(data, renames, (emitter, name) => emitter.Name = name, name => ownPlayed.Add((file.Alias, name)));
            if (count == 0)
            {
                continue;
            }

            data.SaveTo(file.Path);
            renamedEmitters += count;
            changedFiles++;
        }

        return (renamedEmitters, changedFiles, ownPlayed);
    }

    private static int RenameInLoaded(SerializableAsset asset, IReadOnlyDictionary<string, string> renames, Action<string> playsOwn)
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
        }, playsOwn);
        if (count > 0 && nodes.Count == 0)
        {
            asset.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        }

        return count;
    }

    // An emitter plays its chunk's own system of its name before the default chunk's: one of the old name stays, one of the new name gets
    // played in the renamed one's place
    private static int RenameEmitters(ParticleData data, IReadOnlyDictionary<string, string> renames, Action<ParticleSystemInstance, string> rename, Action<string> playsOwn)
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
            if (own.Contains(name))
            {
                playsOwn(name);
            }
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
