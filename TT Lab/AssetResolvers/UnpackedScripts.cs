using System;
using System.Collections.Generic;
using TT_Lab.Assets;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetResolvers;

/// <summary>
/// The behaviour starters and graphs of the chunks of a version being unpacked. The game reuses graph IDs for other scripts in other chunks
/// (0xBED is COM_PIRATE_MISSILE_INIT in three of High Seas' chunks and COM_GENERIC_FURNITURE_WAKE_CONTACT_RESPONSE in eight of AltEarth's,
/// 0xEF3 and 0xF65 have two scripts each too), the chunks after the first one having a script of the ID making it a variant named after
/// that chunk, and two of its starters have another ID than the one before their graph's (AltEarth's 0xBFE of 0xBED, labext's 0xF6C of
/// 0xF65). So what a chunk's objects name by a graph's or a starter's ID is that chunk's, so is what a chunk's graphs run as a state's child
/// by its ID (docamok3's COM_DOCAMOK_BEAR_DEFAULT runs its COM_DOCAMOK_BEAR_MOVE, 0xEF3, a platform's script in l03stock), and a graph's
/// starter is the one its chunk has, else another chunk's of the same graph (its ID and name): the game only has a starter in the chunks
/// whose objects list it
/// </summary>
public sealed class UnpackedScripts
{
    // The first starter of each graph, by the graph's ID and name, in the order the chunks are unpacked
    private readonly Dictionary<(UInt32 Graph, String Name), TwinBehaviourStarter> _startersOfGraphs = new();
    private readonly Dictionary<String, ChunkScripts> _chunks = new(StringComparer.Ordinal);

    /// <summary>
    /// A chunk's starters with the names of the graphs they start, which every starter of the game has in its chunk: read with the chunk
    /// for <see cref="Add"/>, the chunks in parallel
    /// </summary>
    public static List<(TwinBehaviourStarter Starter, String Graph)> StartersOf(ITwinSection chunk)
    {
        var behaviours = BehavioursOf(chunk);
        var starters = new List<(TwinBehaviourStarter, String)>();
        for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
        {
            if (behaviours.GetItem(i) is TwinBehaviourStarter starter && GraphOf(starter) is { } graph && behaviours.ContainsItem(graph)
                && behaviours.GetItem<ITwinItem>(graph) is ITwinBehaviourGraph started)
            {
                starters.Add((starter, started.GetName()));
            }
        }

        return starters;
    }

    /// <summary>
    /// Takes in a chunk's starters (<see cref="StartersOf"/>), the chunks in the order they're unpacked
    /// </summary>
    public void Add(IEnumerable<(TwinBehaviourStarter Starter, String Graph)> starters)
    {
        foreach (var (starter, graph) in starters)
        {
            _startersOfGraphs.TryAdd((GraphOf(starter)!.Value, graph), starter);
        }
    }

    /// <summary>
    /// The chunk's scripts as it's being unpacked: its starters, and the assets its graphs become once they're resolved
    /// </summary>
    public ChunkScripts Begin(String chunkPath, ITwinSection chunk)
    {
        var scripts = new ChunkScripts(this, BehavioursOf(chunk));
        _chunks[chunkPath] = scripts;
        return scripts;
    }

    /// <summary>
    /// The chunk's scripts once it began being unpacked, none before
    /// </summary>
    public ChunkScripts? Of(String chunkPath) => _chunks.GetValueOrDefault(chunkPath);

    internal TwinBehaviourStarter? StarterOf(UInt32 graph, String name) => _startersOfGraphs.GetValueOrDefault((graph, name));

    // The graph a starter's first assigner runs (its ID plus one), none without an assigner
    internal static UInt32? GraphOf(TwinBehaviourStarter starter) => starter.Assigners.Count > 0 ? (UInt32)(starter.Assigners[0].Behaviour - 1) : null;

    private static ITwinSection BehavioursOf(ITwinSection chunk) => chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
}

/// <summary>
/// A chunk's starters and the assets its graphs became as it's being unpacked (<see cref="UnpackedScripts"/>)
/// </summary>
public sealed class ChunkScripts
{
    private readonly UnpackedScripts _unpacked;
    // The graphs the chunk's starters start by the starters' IDs, and the starters by their graphs' IDs
    private readonly Dictionary<UInt32, UInt32> _startedGraphs = new();
    private readonly Dictionary<UInt32, TwinBehaviourStarter> _starters = new();
    private readonly Dictionary<UInt32, LabURI> _graphs = new();

    internal ChunkScripts(UnpackedScripts unpacked, ITwinSection behaviours)
    {
        _unpacked = unpacked;
        for (var i = 0; i < behaviours.GetItemsAmount(); ++i)
        {
            if (behaviours.GetItem(i) is TwinBehaviourStarter starter && UnpackedScripts.GraphOf(starter) is { } graph)
            {
                _startedGraphs.TryAdd(starter.GetID(), graph);
                _starters.TryAdd(graph, starter);
            }
        }
    }

    /// <summary>
    /// The asset the chunk's graph of the ID became, made from it or from another chunk's of the same script
    /// </summary>
    internal void Resolved(UInt32 graph, LabURI asset) => _graphs[graph] = asset;

    /// <summary>
    /// The starter of the chunk's graph: the chunk's own, else another chunk's of the same graph, none when no chunk has one
    /// </summary>
    public TwinBehaviourStarter? StarterOf(ITwinBehaviourGraph graph) => _starters.GetValueOrDefault(graph.GetID()) ?? _unpacked.StarterOf(graph.GetID(), graph.GetName());

    /// <summary>
    /// The chunk's graph an object of the chunk names by the graph's ID or its starter's, or a graph of the chunk runs as a state's child by
    /// its ID, none when the chunk has neither of the ID
    /// </summary>
    public LabURI? GraphNamed(UInt32 id) => _graphs.GetValueOrDefault(_startedGraphs.TryGetValue(id, out var graph) ? graph : id);

    /// <summary>
    /// The ID of the starter of the chunk's graph of the ID: the one before it unless the chunk's starter of it has another
    /// </summary>
    public UInt32 StarterIdOf(UInt32 graph) => _starters.TryGetValue(graph, out var starter) ? starter.GetID() : graph - 1;
}
