using System;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinObjectTriggerBehaviour
    {
        public UInt16 MessageID { get; set; }
        public UInt16 TriggerBehaviour { get; set; }
        public Byte BehaviourCallerIndex { get; set; }
        /// <summary>
        /// The Xbox version sets every bit above the caller index
        /// </summary>
        public Byte UpperBits { get; set; }

        public TwinObjectTriggerBehaviour() { }

        public TwinObjectTriggerBehaviour(UInt32 value)
        {
            MessageID = (UInt16)(value & 0x3FF);
            TriggerBehaviour = (UInt16)(value >> 0xA & 0x3FFF);
            BehaviourCallerIndex = (Byte)(value >> 0x18 & 0x1);
            UpperBits = (Byte)(value >> 0x19 & 0x7F);
        }

        public UInt32 Compress()
        {
            UInt32 result = MessageID;
            result |= (UInt32)((TriggerBehaviour & 0x3FFF) << 0xA);
            result |= (UInt32)((BehaviourCallerIndex & 0x1) << 0x18);
            result |= (UInt32)((UpperBits & 0x7F) << 0x19);

            return result;
        }
    }
}
