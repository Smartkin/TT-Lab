using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Code.Resolvers.Compiler;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using Twinsanity.AgentLab;
using Twinsanity.AgentLab.Resolvers.Decompiler;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.AssetData.Code.Behaviour;

public class BehaviourGraphData : AbstractAssetData
{
    private int _graphId = -1;
    private TwinBehaviourStarter? _starter;
        
    private static string _behaviourTemplate = """
                                               [StartFrom(StartingState)] // Optional, without it the first state is the starting one
                                               [Priority(20)] // Priority of the starter: a behaviour started with a higher priority takes over a running one. Optional, 0 without it
                                               behaviour COM_RENAME_ME { // Any name

                                                    // The starter lets the behaviour start on its own when the object gets an event (spawned, damaged, ...),
                                                    // without it the behaviour only runs when another script executes it. The first assigner is the object itself
                                                    // running this behaviour, more assigners name other agents (HUMAN_PLAYER, ORIGINATOR, GLOBAL_AGENT with the
                                                    // instance's RefListIndex) that commands address by their index
                                                    starter {
                                                        assigner = {
                                                            AssignType = ME;
                                                        }
                                                    }

                                                    state StartingState() { // Any name
                                                        // Every update the conditions of the state's bodies are evaluated. A body passes when its condition's result
                                                        // is above the threshold (below it with <) and among the passing bodies the one with the largest
                                                        // (result - threshold) * weight runs its commands top to bottom, then jumps where its execute says.
                                                        // weight = ...; sets the weight, 1 / threshold without it
                                                        if IsCollidable(0) > 0.5 {
                                                            window = 0.2; // Only some conditions read it: the event conditions (touched, spun, got a message) pass when the event happened within this many seconds
                                                            // Commands come from the action definitions (Ctrl+Space lists them with their parameters). Some arguments are
                                                            // several fields the game packs into one value, written in braces: any order, the fields left out are 0, {} for none.
                                                            // A number instead of the braces sets the whole value at once
                                                            DoAnimation({slotCount = 1, blendTimeGiven = true}, 0.2, 0.0, 0.0, 0.0, {slot1 = 0, slot2 = 255, slot3 = 255, slot4 = 255});
                                                            // tfloat, tint and tangle arguments (like the blend time above) take a literal or Prop(n), the instance's float, int
                                                            // or angle property n, read when the command runs. Their low 3 bits hold the tag, so a literal keeps 21 bits of precision
                                                            execute NextState;
                                                        }
                                                        else { // Runs when no other body of the state passes
                                                            execute NextState;
                                                        }
                                                    }

                                                    // What a state can have:
                                                    // state Name(COM_OTHER_BEHAVIOUR) - runs that behaviour as a child while the state lasts. Ctrl+Space between the parentheses lists
                                                    //                                  the behaviours of this package and the packages it depends on, Ctrl+click or F12 on the name opens it
                                                    // [UseObjectSlot(SLOT_NAME)] - the child behaviour is the one the object has in that event slot
                                                    // [ControlPacket(PACKET_NAME)] - moves the object the way the packet says while the state lasts
                                                    // completion { ... } - as the first body: runs when the control packet or child behaviour finishes, its commands and execute apply then
                                                    // [Interrupting] - the conditions keep being evaluated while the child behaviour runs, a passing body ends it
                                                    // restart = true; - in a body jumping to its own state: the state is entered again, restarting its child behaviour and timer

                                                    // A state without bodies ends the behaviour
                                                    state NextState() {
                                                    }

                                                    // Commands renamed since a script was written still compile under their old names (AUnknown_N and the like).
                                                    // Look at the game's scripts for control packets and the rest
                                               }
                                               """;

    /// <summary>
    /// The script a new behaviour graph starts with
    /// </summary>
    internal static string Template => _behaviourTemplate;
    
    public BehaviourGraphData(IAsset asset) : base(asset)
    {
        Graph = _behaviourTemplate[..];
    }

    public BehaviourGraphData(IAsset asset, ITwinBehaviourGraph mainScript, TwinBehaviourStarter? starter = null) : this(asset)
    {
        SetTwinItem(mainScript);
        SetStarter(starter);
    }

    [Editable(Caption = "Behaviour Graph Editor", EditorOrientation = Avalonia.Controls.Dock.Top, EditorDescType = typeof(CodeEditorDesc))]
    [EditorParam(CodeEditorViewModel.ValidateAgentLabCode, true)]
    public String Graph { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Graph = "";
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        using var fs = new FileStream(dataPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);
        writer.Write(Graph.ToCharArray());
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        using var fs = new FileStream(dataPath, FileMode.Open, FileAccess.Read);
        using var reader = new StreamReader(fs);
        Graph = reader.ReadToEnd();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var graph = GetTwinItem<ITwinBehaviourGraph>();
        var starter = _starter;
        // An assigner's index is an instance's RefListIndex, not an object, so nothing gets resolved for it
        IStarterAssignerGlobalObjectIdResolversList? globalObjectIdResolver = null;

        var stateList = new List<IStateResolver>();
        foreach (var state in graph.ScriptStates)
        {
            string? graphName = null;
            if (state.BehaviourIndexOrSlot != -1 && !state.UsesObjectSlot)
            {
                // By its name when the name finds it from this graph, by its URI otherwise
                var uri = AssetManager.Get().GetUriByTwinId<BehaviourGraph>(Owner, (uint)state.BehaviourIndexOrSlot);
                graphName = uri == LabURI.Empty ? uri : BehaviourReferences.ReferenceTo(Owner, AssetManager.Get().GetAsset<BehaviourGraph>(uri));
            }

            stateList.Add(new DefaultStateResolver(graphName));
        }
        var stateResolver = new DefaultStateResolversList(stateList.ToArray());
        var resolver = new DefaultGraphResolver(new DefaultStarterResolver(starter, globalObjectIdResolver), stateResolver);
        Graph = AgentLabDecompiler.Decompile(graph, resolver);
    }

    // A build compiles a behaviour for every chunk using it and again when writing objects' slots. Compiling only depends on the
    // script and its ID, so the chunks of a build share the results
    public AgentLabCompiler.CompilerResult GetCompiledBehaviour(ITwinItemFactory factory)
    {
        var graphId = _graphId >= 0 ? _graphId : (int)Owner.ExportTwinID;
        return factory.CompiledBehaviours.GetOrAdd((Owner.URI, graphId, Graph), _ => Compile(factory, graphId));
    }

    private AgentLabCompiler.CompilerResult Compile(ITwinItemFactory factory, int graphId)
    {
        using var ms = new MemoryStream();
        using var binaryWriter = new BinaryWriter(ms);
        binaryWriter.Write(graphId);
        using var writer = new StreamWriter(ms);
        writer.Write(Graph);
        writer.Flush();

        ms.Position = 0;
        return factory.GenerateBehaviourGraph(ms, Owner);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        _graphId = (int)id;
        var compiledBehaviour = GetCompiledBehaviour(factory);
        factory.Resolution.GraphReferences[Owner.URI] = GetReferences(factory);
        if (section.ContainsItem(id))
        {
            return null;
        }

        var item = compiledBehaviour.Get<ITwinBehaviourGraph>();
        item.SetID(id);
        section.AddItem(item);
        return item;
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        return GetCompiledBehaviour(factory).Get<ITwinBehaviourGraph>();
    }

    /// <summary>
    /// The objects and graphs the graph's code refers to
    /// </summary>
    public (IReadOnlyList<LabURI> Objects, IReadOnlyList<LabURI> Graphs) GetReferences(ITwinItemFactory factory)
    {
        var resolver = GetCompiledBehaviour(factory).CompilerOptions.Resolver;
        return (((LabGlobalObjectIdResolver)resolver.GetObjectIdResolver()).ResolvedObjects, ((LabStateGraphResolver)resolver.GetStateGraphResolver()).ResolvedGraphs);
    }

    // The game only has a graph's starter in the chunks with objects referencing it, so the objects put it there
    public void AddStarter(ITwinItemFactory factory, ITwinSection section, UInt32 graphId)
    {
        var compiledBehaviour = GetCompiledBehaviour(factory);
        if (!compiledBehaviour.Contains<TwinBehaviourStarter>() || section.ContainsItem(graphId - 1))
        {
            return;
        }

        var starter = compiledBehaviour.Get<TwinBehaviourStarter>();
        starter.SetID(graphId - 1);
        // Writes the ID into the starter's header
        starter.Compile();
        section.AddItem(starter);
    }

    private void SetStarter(TwinBehaviourStarter? starter)
    {
        _starter = starter;
    }
}