using Newtonsoft.Json;
using System;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.Instance.Scenery;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetData.Instance.Scenery;

public class SceneryRootData : SceneryNodeData
{
    public UInt32 TreeDepth { get; set; }

    public SceneryRootData() { }

    public SceneryRootData(IAsset owner, TwinSceneryBaseType baseType) : base(owner, baseType)
    {
        var root = (TwinSceneryRoot)baseType;
        TreeDepth = root.TreeDepth;
    }

    public SceneryRootData(SceneryRootViewModel vm) : base(vm)
    {
        TreeDepth = vm.TreeDepth;
    }

    public override ITwinScenery.SceneryType GetSceneryType()
    {
        return ITwinScenery.SceneryType.Root;
    }

    public override void Write(BinaryWriter writer)
    {
        writer.Write(TreeDepth);
        base.Write(writer);
    }
}