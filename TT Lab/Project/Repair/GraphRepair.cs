using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Project.Migration;

namespace TT_Lab.Project.Repair;

/// <summary>
/// A1: the game reuses graph IDs for other scripts in other chunks, and two of its starters have another ID than the one before their graph's.
/// 1.1.0's unpack linked every object to the version's graph of an ID without a variation (an object belongs to no chunk), left a slot naming
/// a starter of another ID empty, took a graph's starter to be the one before it and gave every graph of an ID the first chunk's starter. The
/// shadow's objects link their chunk's graphs: a link that's still what 1.1.0 made takes the shadow's, anything else is the user's and noted
/// </summary>
internal sealed class GraphRepair
{
    private const string AssetData = AssetOverrides.DataProperty;
    private static readonly string[] BehaviourLists = ["BehaviourSlots", "TriggerBehaviours", "GraphsWithoutStarter", "RefBehaviours"];

    private readonly RepairFiles _shadow;
    private readonly RepairFiles _project;
    private readonly RepairVersion _version;
    private readonly MigrationContext _context;
    private readonly Dictionary<string, RepairFiles.Entry> _shadowGraphs;
    private readonly Dictionary<string, RepairFiles.Entry> _projectGraphs;
    // The IDs of several scripts in the version, and the IDs of graphs whose starter has another ID than the one before theirs
    private readonly HashSet<UInt32> _reused;

    public GraphRepair(RepairFiles shadow, RepairFiles project, RepairVersion version, MigrationContext context)
    {
        _shadow = shadow;
        _project = project;
        _version = version;
        _context = context;
        _shadowGraphs = shadow.OfType(nameof(BehaviourGraph), version.Packages).ToDictionary(graph => graph.Uri, StringComparer.Ordinal);
        _projectGraphs = project.OfType(nameof(BehaviourGraph), version.Packages).ToDictionary(graph => graph.Uri, StringComparer.Ordinal);
        _reused = _shadowGraphs.Values.GroupBy(graph => graph.Id).Where(group => group.Count() > 1).Select(group => group.Key)
            .Concat(_shadowGraphs.Values.Where(graph => shadow.Metadata(graph)[nameof(BehaviourGraph.StarterId)] != null).Select(graph => graph.Id))
            .ToHashSet();
    }

    public void Run()
    {
        if (_reused.Count == 0)
        {
            return;
        }

        GiveStarterIds();
        PutBackStarters();
        FixObjects();
    }

    // The two starters of another ID than the one before their graph's: 1.1.0 never wrote the property
    private void GiveStarterIds()
    {
        foreach (var graph in _shadowGraphs.Values)
        {
            var metadata = _shadow.Metadata(graph);
            if (metadata[nameof(BehaviourGraph.StarterId)] is not { } starterId || _project.Find(graph.Uri) is not { } projectGraph)
            {
                continue;
            }

            var projectMetadata = _project.Metadata(projectGraph);
            if (projectMetadata[nameof(BehaviourGraph.StarterId)] != null)
            {
                continue;
            }

            RepairFiles.SetLike(projectMetadata, nameof(BehaviourGraph.StarterId), starterId.DeepClone(), metadata);
            _project.Changed(projectGraph.Path);
        }
    }

    // A graph 1.1.0 gave another chunk's starter of its ID (the bear's move l03stock's platform's, which no chunk starts it with): the
    // shadow's script while the project's is it with that starter and nothing else changed
    private void PutBackStarters()
    {
        var scripts = new Dictionary<string, BehaviourScript?>(StringComparer.Ordinal);
        BehaviourScript? ScriptOf(RepairFiles files, RepairFiles.Entry graph)
        {
            var key = $"{files.AssetsFolder}|{graph.Uri}";
            if (!scripts.TryGetValue(key, out var script))
            {
                scripts[key] = script = files.Text(graph.DataFile(".lab")) is { } text ? BehaviourScript.Parse(text) : null;
            }

            return script;
        }

        foreach (var graph in _shadowGraphs.Values.Where(graph => _reused.Contains(graph.Id)))
        {
            if (_project.Find(graph.Uri) is not { } projectGraph || ScriptOf(_shadow, graph) is not { } wanted || ScriptOf(_project, projectGraph) is not { } current)
            {
                continue;
            }

            if (current.Starter == null || current.Rest != wanted.Rest || current.SameStarterAs(wanted))
            {
                continue;
            }

            // The starter of a graph of the ID in another chunk, the graph's own priority where 1.1.0 wrote it next to the starter's
            var isAnothers = _shadowGraphs.Values.Where(other => other.Id == graph.Id && other.Uri != graph.Uri).Select(other => ScriptOf(_shadow, other))
                .Any(other => other?.Starter == current.Starter && other.Priority == current.Priority);
            if (!isAnothers || current.GraphPriority != wanted.OwnPriorityLine())
            {
                continue;
            }

            _project.SetText(projectGraph.DataFile(".lab"), _shadow.Text(graph.DataFile(".lab"))!);
        }
    }

    private void FixObjects()
    {
        var shadowValues = _version.OwnValuesOf(_shadow);
        var projectValues = _version.OwnValuesOf(_project);
        foreach (var shadowObject in _shadow.OfType(nameof(GameObject), _version.Packages))
        {
            if (_project.Find(shadowObject.Uri) is not { } projectObject || _shadow.Data(shadowObject) is not { } shadowData || _project.Data(projectObject) is not { } projectData)
            {
                continue;
            }

            var unpacked = (JObject)projectData.DeepClone();
            if (FixLinks(projectData, shadowData, null, label => $"{projectObject}: {label}", _ => true))
            {
                _project.Changed(projectObject.DataFile(".data"));
                FixReferences(projectObject, shadowObject, projectData);
            }

            var chunks = shadowValues.GetValueOrDefault(shadowObject.Uri, []).Select(values => values.Chunk.Uri)
                .Concat(projectValues.GetValueOrDefault(shadowObject.Uri, []).Select(values => values.Chunk.Uri)).Distinct().Order(StringComparer.Ordinal);
            foreach (var chunk in chunks)
            {
                FixChunkValues(chunk, projectObject, unpacked, projectData, shadowData, shadowValues.GetValueOrDefault(shadowObject.Uri, []),
                    projectValues.GetValueOrDefault(shadowObject.Uri, []));
            }
        }
    }

    // A chunk's own values of the object's behaviours: what the chunk had (1.1.0's object with its values) fixed the same way, then made again
    // of what differs from the object as it's repaired
    private void FixChunkValues(string chunkUri, RepairFiles.Entry projectObject, JObject unpacked, JObject repaired, JObject shadowData,
        List<RepairVersion.OwnValues> shadowValues, List<RepairVersion.OwnValues> projectValues)
    {
        if (_project.Find(chunkUri) is not { } chunk)
        {
            return;
        }

        var projectEntry = projectValues.FirstOrDefault(values => values.Chunk.Uri == chunkUri);
        var shadowEntry = shadowValues.FirstOrDefault(values => values.Chunk.Uri == chunkUri);
        if (!(projectEntry?.Values.Properties().Any(property => IsOfBehaviours(property.Name)) ?? false) &&
            !(shadowEntry?.Values.Properties().Any(property => IsOfBehaviours(property.Name)) ?? false))
        {
            return;
        }

        var wrap = (JObject data) => new JObject { [AssetData] = data.DeepClone() };
        var had = ChunkOwnValues.Apply(wrap(unpacked), projectEntry?.Values);
        var wanted = ChunkOwnValues.Apply(wrap(shadowData), shadowEntry?.Values);
        var fixedValues = (JObject)had.DeepClone();
        // The chunk's own values are noted, a value of the object's is noted with the object
        FixLinks((JObject)fixedValues[AssetData]!, (JObject)wanted[AssetData]!, RepairVersion.VariationOf(_project, chunk),
            label => $"{chunk}'s own value of {projectObject}: {label}",
            path => projectEntry != null && ChunkOwnValues.Covering(projectEntry.Values, $"{AssetData}.{path}") != null);

        var own = AssetOverrides.Diff(Behaviours(repaired), Behaviours((JObject)fixedValues[AssetData]!));
        var current = projectEntry?.Values.Properties().Where(property => IsOfBehaviours(property.Name)).ToDictionary(property => property.Name, property => property.Value)
                      ?? new Dictionary<string, JToken>();
        if (own.Count == current.Count && own.All(pair => current.TryGetValue(pair.Key, out var value) && JToken.DeepEquals(value, pair.Value)))
        {
            return;
        }

        var metadata = _project.Metadata(chunk);
        ChunkOwnValues.Replace(projectEntry?.Entry ?? ChunkOwnValues.EntryFor(metadata, projectObject.Uri), IsOfBehaviours, own);
        _project.Changed(chunk.Path);
    }

    private static JObject Behaviours(JObject data)
    {
        var behaviours = new JObject();
        foreach (var name in BehaviourLists.Where(name => data[name] != null))
        {
            behaviours[name] = data[name]!.DeepClone();
        }

        return new JObject { [AssetData] = behaviours };
    }

    private static bool IsOfBehaviours(string path) => BehaviourLists.Any(name => path == $"{AssetData}.{name}" || ChunkOwnValues.IsUnder(path, $"{AssetData}.{name}"));

    // The object's (or a chunk's view of it) links to graphs of reused IDs that are still 1.1.0's, the shadow's in their place. Whether any changed
    private bool FixLinks(JObject target, JObject wanted, string? chunkVariation, Func<string, string> describe, Func<string, bool> noted)
    {
        var changed = false;
        foreach (var (path, label, isSlot) in Sites(wanted))
        {
            if (target.SelectToken(path) is not JObject link || !_shadowGraphs.TryGetValue(RepairFiles.UriOf(wanted.SelectToken(path)), out var graph))
            {
                continue;
            }

            var current = RepairFiles.UriOf(link);
            if (current == graph.Uri || !_reused.Contains(graph.Id))
            {
                continue;
            }

            if (!IsUnpacked(current, graph.Id, chunkVariation, isSlot))
            {
                if (noted(path))
                {
                    _context.Note($"{describe(label)} was changed after creation, it should link {graph.Name}");
                }

                continue;
            }

            if (!_project.Has(graph.Uri))
            {
                _context.Note($"{describe(label)} should link {graph.Name}, which the project doesn't have", Log.LogType.Warning);
                continue;
            }

            link["_uri"] = graph.Uri;
            changed = true;
        }

        return FixGraphsWithoutStarter(target, wanted) || changed;
    }

    private static IEnumerable<(string Path, string Label, bool IsSlot)> Sites(JObject data)
    {
        if (data["BehaviourSlots"] is JArray slots)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                yield return ($"BehaviourSlots[{i}]", $"behaviour slot {i}", true);
            }
        }

        if (data["TriggerBehaviours"] is JArray messages)
        {
            for (var i = 0; i < messages.Count; i++)
            {
                yield return ($"TriggerBehaviours[{i}].TriggerBehaviour", $"trigger message {i}", false);
            }
        }

        if (data["RefBehaviours"] is JArray references)
        {
            for (var i = 0; i < references.Count; i++)
            {
                yield return ($"RefBehaviours[{i}]", $"behaviour reference {i}", false);
            }
        }
    }

    // What 1.1.0's unpack linked a graph of the ID with: the version's graph of the ID without a variation (a chunk's view of the object found
    // the chunk's own variant too), and nothing for a slot naming a starter no graph had
    private bool IsUnpacked(string uri, UInt32 id, string? chunkVariation, bool isSlot)
    {
        if (isSlot && uri == RepairFiles.EmptyUri)
        {
            return true;
        }

        return _projectGraphs.TryGetValue(uri, out var graph) && graph.Id == id && (!graph.IsVariant || graph.Variation == chunkVariation);
    }

    private bool IsReusedGraph(string uri) => (_shadowGraphs.GetValueOrDefault(uri) ?? _projectGraphs.GetValueOrDefault(uri)) is { } graph && _reused.Contains(graph.Id);

    // The graphs the object lists without their starter, which isn't edited in TT Lab: 1.1.0 took every starter to be the one before its graph.
    // The shadow's of the graphs of reused IDs, the others as they are
    private bool FixGraphsWithoutStarter(JObject target, JObject wanted)
    {
        if (target["GraphsWithoutStarter"] is not JArray current || wanted["GraphsWithoutStarter"] is not JArray shadow)
        {
            return false;
        }

        var currentUris = current.Select(RepairFiles.UriOf).ToList();
        var shadowUris = shadow.Select(RepairFiles.UriOf).Where(_project.Has).ToList();
        var kept = currentUris.Where(uri => !IsReusedGraph(uri)).ToList();
        var result = kept.SequenceEqual(shadowUris.Where(uri => !IsReusedGraph(uri))) ? shadowUris : kept.Concat(shadowUris.Where(IsReusedGraph)).Distinct().ToList();
        if (result.SequenceEqual(currentUris))
        {
            return false;
        }

        target["GraphsWithoutStarter"] = new JArray(result.Select(uri => new JObject { ["_uri"] = uri }));
        return true;
    }

    // The object's references (what links to what): the shadow's of the graphs of reused IDs, and any the user's links keep
    private void FixReferences(RepairFiles.Entry projectObject, RepairFiles.Entry shadowObject, JObject data)
    {
        var metadata = _project.Metadata(projectObject);
        if (metadata["References"] is not JArray references || _shadow.Metadata(shadowObject)["References"] is not JArray shadowReferences)
        {
            return;
        }

        var current = references.Select(RepairFiles.UriOf).ToList();
        var shadow = shadowReferences.Select(RepairFiles.UriOf).Where(_project.Has).ToList();
        var kept = current.Where(uri => !IsReusedGraph(uri)).ToList();
        var result = (kept.SequenceEqual(shadow.Where(uri => !IsReusedGraph(uri))) ? shadow : kept.Concat(shadow.Where(IsReusedGraph)))
            .Concat(RepairFiles.Links(data).Select(RepairFiles.UriOf).Where(IsReusedGraph)).Distinct().ToList();
        if (result.SequenceEqual(current))
        {
            return;
        }

        metadata["References"] = new JArray(result.Select(uri => new JObject { ["_uri"] = uri }));
        _project.Changed(projectObject.Path);
    }
}
