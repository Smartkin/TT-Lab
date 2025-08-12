using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

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

        public ObjectTriggerBehaviourData(LabURI package, String? variant, TwinObjectTriggerBehaviour triggerBehaviour, Dictionary<string, TwinBehaviourStarter> starterMap)
        {
            var starter = starterMap.Values.First(s => s.GetID() == triggerBehaviour.TriggerBehaviour);
            TriggerBehaviour = AssetManager.Get().GetUri(package, nameof(BehaviourGraph), variant, (uint)(starter.Assigners[0].Behaviour - 1));
            Debug.Assert(TriggerBehaviour != LabURI.Empty, "Trigger behaviour must not link to an empty behaviour");
            MessageID = triggerBehaviour.MessageID;
            BehaviourCallerIndex = triggerBehaviour.BehaviourCallerIndex;
        }
    }
}
