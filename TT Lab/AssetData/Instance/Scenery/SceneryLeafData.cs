using System;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.Instance.Scenery;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;

namespace TT_Lab.AssetData.Instance.Scenery;

public class SceneryLeafData : SceneryBaseData
{
    public SceneryLeafData() { }
    public SceneryLeafData(IAsset owner, TwinSceneryBaseType baseType) : base(owner, baseType)
    {
    }
    public SceneryLeafData(BaseSceneryViewModel vm) : base(vm) { }
}