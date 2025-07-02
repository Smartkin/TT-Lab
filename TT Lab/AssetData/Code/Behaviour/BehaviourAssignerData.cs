using Newtonsoft.Json;
using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using static Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourAssigner;

namespace TT_Lab.AssetData.Code.Behaviour
{
    [ReferencesAssets]
    public class BehaviourAssignerData
    {
        [JsonProperty(Required = Required.Always)]
        public LabURI Behaviour { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI Object { get; set; }
        [JsonProperty(Required = Required.Always)]
        public AssignTypeID AssignType { get; set; }
        [JsonProperty(Required = Required.Always)]
        public AssignLocalityID AssignLocality { get; set; }
        [JsonProperty(Required = Required.Always)]
        public AssignStatusID AssignStatus { get; set; }
        [JsonProperty(Required = Required.Always)]
        public AssignPreferenceID AssignPreference { get; set; }

        public BehaviourAssignerData()
        {
            Behaviour = LabURI.Empty;
            Object = LabURI.Empty;
        }

        public BehaviourAssignerData(LabURI package, String? variant, TwinBehaviourAssigner assigner)
        {
            Behaviour = assigner.Behaviour - 1 == -1 ? LabURI.Empty : AssetManager.Get().GetUri(package, typeof(BehaviourGraph).Name, variant, (UInt32)assigner.Behaviour - 1);
            Object = assigner.GlobalObjectId != 65535 ? AssetManager.Get().GetUri(package, typeof(GameObject).Name, variant, assigner.GlobalObjectId) : LabURI.Empty;
            AssignType = assigner.AssignType;
            AssignLocality = assigner.AssignLocality;
            AssignStatus = assigner.AssignStatus;
            AssignPreference = assigner.AssignPreference;
        }
    }
}
