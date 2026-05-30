using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.ViewModels.Editors.Code;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.Assets.Code;

public class GameObject : SerializableAsset
{
    public class ReferencedResourceUris
    {
        public IReadOnlyList<LabURI> RefObjects { get; init; } = [];
        public IReadOnlyList<LabURI> RefOgis { get; init; } = [];
        public IReadOnlyList<LabURI> RefAnimations { get; init; } = [];
        public IReadOnlyList<LabURI> RefSounds { get; init; } = [];
        public IReadOnlyList<LabURI> RefBehaviours { get; init; } = [];
    }
    public event Action<ReferencedResourceUris>? ReferencesObtained;
        
    public override UInt32 Section => Constants.CODE_GAME_OBJECTS_SECTION;
    public override String IconPath => "Game_Object.png";

    public GameObject() { }

    public GameObject(LabURI package, Boolean needVariant, String variant, UInt32 id, String name, ITwinObject @object, Dictionary<string, TwinBehaviourStarter> starterMap) : base(id, name, package, needVariant, variant)
    {
        AssetData = new GameObjectData(this, @object, starterMap);
    }

    public override Type GetEditorType()
    {
        return typeof(GameObjectViewModel);
    }

    public override AbstractAssetData GetData()
    {
        if (!IsLoaded || AssetData.Disposed)
        {
            AssetData = new GameObjectData(this);
            AssetData.Load(DataLoadPath);
        }
        return AssetData;
    }

    public void FireReferencedBehavioursObtained(ReferencedResourceUris resourceReferences)
    {
        ReferencesObtained?.Invoke(resourceReferences);
    }
}