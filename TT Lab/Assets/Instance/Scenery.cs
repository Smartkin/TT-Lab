using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.ViewModels.Editors.Instance;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.Assets.Instance;

public class Scenery : SerializableInstance
{
    protected override String DataExt => ".tlm";
    
    public override bool IsInScenery => true;
    public override UInt32 Section => Constants.SCENERY_SECENERY_ITEM;
    public override String IconPath => "Collision.png";

    public Scenery()
    {
        Parameters = new Dictionary<string, object?>();
    }

    public Scenery(LabURI package, UInt32 id, String name, String chunk, ITwinScenery scenery, LabURI dynamicScenery) : base(package, id, name, chunk, null)
    {
        AssetData = new SceneryData(this, scenery)
        {
            DynamicScenery = dynamicScenery
        };
    }

    public void LinkCollision(LabURI collision)
    {
        if (IsLoaded)
        {
            ((SceneryData)AssetData!).Collision = collision;
        }
    }

    public override Type GetEditorType()
    {
        return typeof(SceneryViewModel);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new SceneryData(this);
            AssetData.Load(DataLoadPath);
        }
            
        return AssetData;
    }

    protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new SceneryElementViewModel(URI, parent);
    }
}