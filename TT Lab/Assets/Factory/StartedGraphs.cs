using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets.Factory;

// The graphs a holder starts by naming their starters (an instance's spawn script, a template's starters), brought into the chunk being
// built the way an object's behaviours come in: each graph with its starter in the holder's order, then the graphs they run and the objects
// their scripts make. A level leaves a graph the default chunk has to it, which the game always has loaded, and only what the graph makes
// comes in, like a startup object's copy does: no level of the game has the crates' outline its crates spawn with (starter 0x28), which
// docamok1's and docamok2's crates of layout 0 brought in
internal static class StartedGraphs
{
    private const string DefaultChunkPath = "startup/default";

    public static void Resolve(ITwinItemFactory factory, ITwinSection codeSection, IEnumerable<LabURI> graphs)
    {
        var assets = AssetManager.Get();
        var behavioursSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        var inChunk = new List<LabURI>();
        var inDefaultChunk = new List<LabURI>();
        foreach (var uri in graphs.Where(uri => uri != LabURI.Empty))
        {
            var graph = assets.GetAsset(uri);
            if (IsTheDefaultChunks(factory, graph))
            {
                inDefaultChunk.Add(uri);
                continue;
            }

            graph.ResolveChunkResources(factory, behavioursSection);
            assets.GetAssetData<BehaviourGraphData>(uri).AddStarter(factory, behavioursSection);
            inChunk.Add(uri);
        }

        // The graphs they run without their starters, like GameObjectData's walk of an object's behaviours
        var made = new List<LabURI>();
        var sequences = new List<LabURI>();
        var visited = new HashSet<LabURI>();
        var pending = new Stack<LabURI>(inChunk);
        while (pending.Count > 0)
        {
            var behaviour = assets.GetAsset(pending.Pop());
            if (!visited.Add(behaviour.URI))
            {
                continue;
            }

            if (behaviour is BehaviourCommandsSequence)
            {
                sequences.Add(behaviour.URI);
                continue;
            }

            behaviour.ResolveChunkResources(factory, behavioursSection);
            if (!factory.Resolution.GraphReferences.TryGetValue(behaviour.URI, out var references))
            {
                continue;
            }

            made.AddRange(references.Objects);
            foreach (var child in references.Graphs)
            {
                pending.Push(child);
            }
        }

        var sequenceSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
        foreach (var sequence in sequences.Select(assets.GetAsset).Where(sequence => sequence.ExportTwinID != 0 || factory.IsDefaultResolution))
        {
            sequence.ResolveChunkResources(factory, sequenceSection);
        }

        made.AddRange(GameObjectData.ObjectsOfBehaviours(inDefaultChunk, factory));
        var objectsSection = codeSection.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        foreach (var gameObject in made.Distinct())
        {
            assets.GetAsset(gameObject).ResolveChunkResources(factory, objectsSection);
        }
    }

    // The version's global package's graphs are the default chunk's, and so are the ones its templates start: an Xbox project made while its
    // default chunk was unpacked after the levels has the crates' outline in the XBOX package
    private static bool IsTheDefaultChunks(ITwinItemFactory factory, IAsset graph)
    {
        if (factory.IsDefaultResolution)
        {
            return false;
        }

        return graph.Package == factory.GlobalPackage?.URI || DefaultTemplateGraphs(factory).Contains(graph.URI);
    }

    // The default chunk's templates are told by their chunk's path: every chunk looked through for the default one would be something the
    // chunk being built depends on (the build cache). The whole path, the startup folder's default (a level chunk of the global package can
    // be named default too), with the separators of the system the project was made on
    private static HashSet<LabURI> DefaultTemplateGraphs(ITwinItemFactory factory)
    {
        if (factory.Resolution.DefaultTemplateGraphs is { } known)
        {
            return known;
        }

        var package = factory.GlobalPackage?.URI;
        var graphs = AssetManager.Get().GetAllAssetsOf<InstanceTemplate>()
            .Where(template => template.Package == package && template.Chunk?.Replace('\\', '/').Equals(DefaultChunkPath, StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(template => ((IAsset)template).GetData<InstanceTemplateData>().BehaviourStarters)
            .ToHashSet();
        factory.Resolution.DefaultTemplateGraphs = graphs;
        return graphs;
    }
}
