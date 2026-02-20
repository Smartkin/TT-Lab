using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.ViewModels.Editors.Code.Behaviour;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace TT_Lab.Assets.Code;

public sealed class BehaviourGraph : Behaviour
{
    private int _starterId = -1;

    protected override String DataExt => ".lab";

    public event Action<IReadOnlyList<LabURI>>? ResolvedObjects;
    public event Action<IReadOnlyList<LabURI>>? ResolvedGraphs; 

    public BehaviourGraph() { }

    public BehaviourGraph(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinBehaviourGraph script, TwinBehaviourStarter? starter = null) : base(package, needVariant, variant, id, name)
    {
        AssetData = new BehaviourGraphData(this, script, starter);
        if (starter != null)
        {
            _starterId = (int)starter.GetID();
        }
        RegenerateUri();
    }

    public int MapStarterIdToSelf(int starterId)
    {
        if (_starterId == starterId)
        {
            return (int)ID;
        }

        return -1;
    }

    public void FireResolvedObjects(IReadOnlyList<LabURI> objects)
    {
        ResolvedObjects?.Invoke(objects);
    }

    public void FireResolvedGraphs(IReadOnlyList<LabURI> graphs)
    {
        ResolvedGraphs?.Invoke(graphs);
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