using Newtonsoft.Json;
using System;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Collision;

namespace TT_Lab.AssetData.Instance.Collision
{
    public class CollisionTrigger
    {
        public Vector3 V1 { get; set; }
        public Vector3 V2 { get; set; }
        public Int32 MinTriggerIndex { get; set; }
        public Int32 MaxTriggerIndex { get; set; }

        public CollisionTrigger()
        {
            V1 = new Vector3();
            V2 = new Vector3();
        }

        public CollisionTrigger(TwinCollisionTrigger trigger)
        {
            V1 = CloneUtils.Clone(trigger.V1);
            V2 = CloneUtils.Clone(trigger.V2);
            MinTriggerIndex = trigger.MinTriggerIndex;
            MaxTriggerIndex = trigger.MaxTriggerIndex;
        }
    }
}
