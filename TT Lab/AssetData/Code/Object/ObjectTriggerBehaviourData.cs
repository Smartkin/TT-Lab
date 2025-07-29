using Newtonsoft.Json;
using System;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Code.Object
{
    [ReferencesAssets]
    public class ObjectTriggerBehaviourData
    {
        [JsonProperty(Required = Required.Always)]
        public LabURI TriggerBehaviour { get; set; }
        [JsonProperty(Required = Required.Always)]
        public UInt16 MessageID { get; set; }
        [JsonProperty(Required = Required.Always)]
        public Byte BehaviourCallerIndex { get; set; }

        public ObjectTriggerBehaviourData()
        {
            TriggerBehaviour = LabURI.Empty;
            MessageID = 0;
        }

        public ObjectTriggerBehaviourData(LabURI package, String? variant, TwinObjectTriggerBehaviour triggerBehaviour)
        {
            TriggerBehaviour = AssetManager.Get().GetUri(package, nameof(BehaviourGraph), variant, triggerBehaviour.TriggerBehaviour + 1U);
            MessageID = triggerBehaviour.MessageID;
            BehaviourCallerIndex = triggerBehaviour.BehaviourCallerIndex;
        }
    }
}
