using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetResolvers;
using TT_Lab.ViewModels.Editors.Code.Behaviour;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.Assets.Code;

public sealed class BehaviourGraph : Behaviour
{
    // The ID of the starter the graph was unpacked with, the one its objects' slots and messages name
    private int _starterId = -1;

    protected override String DataExt => ".lab";

    public BehaviourGraph() { }

    // The scripts of the chunk the graph is unpacked from, whose graphs its states run
    public BehaviourGraph(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinBehaviourGraph script, TwinBehaviourStarter? starter = null,
        ChunkScripts? chunkScripts = null) : base(package, needVariant, variant, id, name)
    {
        AssetData = new BehaviourGraphData(this, script, starter, chunkScripts);
        if (starter != null)
        {
            _starterId = (int)starter.GetID();
            if (starter.GetID() != id - 1)
            {
                StarterId = starter.GetID();
            }
        }
        RegenerateUri();
    }

    /// <summary>
    /// The ID of the graph's starter where it isn't the one before the graph's: two of the game's starters have another (AltEarth's 0xBFE of
    /// COM_GENERIC_FURNITURE_WAKE_CONTACT_RESPONSE, 0xBED, and labext's 0xF6C of COM_DOOR_CORTEX_LAB_DEFAULT, 0xF65), which their chunks'
    /// objects name. None for every other graph, a copy of an ID of its own included
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public UInt32? StarterId { get; set; }

    /// <summary>
    /// The ID the graph's starter is built with: the game's (<see cref="StarterId"/>) while the graph is built with its own ID, else the one
    /// before the graph's
    /// </summary>
    public UInt32 StarterExportID => StarterIdOf(this, ExportTwinID);

    /// <summary>
    /// The ID of the starter of the graph a slot, a trigger message, an instance's spawn script or a template's starter links to
    /// </summary>
    public static UInt32 StarterIdOf(IAsset graph) => StarterIdOf(graph, graph.ExportTwinID);

    /// <summary>
    /// The ID of the graph's starter when the graph is built with the ID: the game's (<see cref="StarterId"/>) while it's the graph's own,
    /// else the one before it
    /// </summary>
    public static UInt32 StarterIdOf(IAsset graph, UInt32 graphId)
    {
        return graph is BehaviourGraph { StarterId: { } starterId } && graphId == graph.ID ? starterId : graphId - 1;
    }

    /// <summary>
    /// The graph whose starter the unpack found with the ID, among the graphs related to the requester's package: what an object of a chunk
    /// without the starter, an instance's spawn script and a template's starter name. None when no graph has it
    /// </summary>
    public static BehaviourGraph? OfStarter(IAsset requester, UInt32 starterId)
    {
        // The starter compared first: every instance's spawn script asks, and telling what's related walks the packages' dependencies
        var assets = AssetManager.Get();
        return assets.GetAllAssetsOf<BehaviourGraph>().FirstOrDefault(graph => graph._starterId == starterId && assets.IsRelated(requester.Package, graph.Package));
    }

    /// <summary>
    /// The graph a starter of the ID starts, read the way unpacking reads the game's files: the one whose starter the unpack found with the
    /// ID (<see cref="OfStarter"/>), else the related graph of the ID after it
    /// </summary>
    public static LabURI GraphOfStarter(IAsset requester, UInt32 starterId)
    {
        return OfStarter(requester, starterId)?.URI ?? AssetManager.Get().GetUriByTwinId<BehaviourGraph>(requester, starterId + 1);
    }

    public override Type GetEditorType()
    {
        return typeof(BehaviourGraphViewModel);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new BehaviourGraphData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }
}