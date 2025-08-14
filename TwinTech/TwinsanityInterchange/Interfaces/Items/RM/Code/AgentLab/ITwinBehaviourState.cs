using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourState : ITwinAgentLab
    {
        /// <summary>
        /// Packages unsolved flags
        /// </summary>
        public UInt16 Bitfield { get; set; }
        /// <summary>
        /// Unknown flags :(
        /// </summary>
        public UInt16 Unknown { get; set; }
        /// <summary>
        /// This can be a behaviour index or point to a slot(event) in the object
        /// </summary>
        public Int16 BehaviourIndexOrSlot { get; set; }
        /// <summary>
        /// If the first state body should be skipped and the next one it points to used
        /// </summary>
        public Boolean SkipsFirstStateBody { get; set; }
        /// <summary>
        /// If the behaviour index points to a behaviour index or to a slot(event) in the object
        /// </summary>
        public Boolean UsesObjectSlot { get; set; }
        /// <summary>
        /// If the execution of this state blocks executing any other behaviours in parallel
        /// </summary>
        public Boolean NoneBlocking { get; set; }
        /// <summary>
        /// Control packet for manipulation position, speed, etc. of the object executing the behaviour
        /// </summary>
        public TwinBehaviourControlPacket ControlPacket { get; set; }
        /// <summary>
        /// State bodies
        /// </summary>
        public List<ITwinBehaviourStateBody> Bodies { get; set; }

        internal bool HasNext { get; set; }

        public void Read(BinaryReader reader, int length, IList<ITwinBehaviourState> scriptStates);
        public void WriteText(StreamWriter writer, Int32 i, Int32 tabs = 0);
        public void ReadText(StreamReader reader);

        public enum ObjectBehaviourSlots
        {
            // Generic behaviour slots
            OnSpawn,
            OnTrigger,
            OnDamage,
            OnTouch,
            OnHeadbutt,
            OnLand,
            OnGettingSpinAttacked,
            OnGettingBodyslamAttacked,
            OnGettingSlideAttacked,
            OnPhysicsCollision,
            OnUnknownCollision,
            // Other slots are object type specific
            Slot_11,
            Slot_12,
            Slot_13,
            Slot_14,
            Slot_15,
            Slot_16,
            Slot_17,
            Slot_18,
            Slot_19,
            Slot_20,
            Slot_21,
            Slot_22,
            Slot_23,
            Slot_24,
            Slot_25,
            Slot_26,
            Slot_27,
            Slot_28,
            Slot_29,
            Slot_30,
            Slot_31,
            Slot_32,
            Slot_33,
            Slot_34,
            Slot_35,
            Slot_36,
            Slot_37,
            Slot_38,
            Slot_39,
            Slot_40,
            Slot_41,
            Slot_42,
            Slot_43,
            Slot_44,
            Slot_45,
            Slot_46,
            Slot_47,
            Slot_48,
            Slot_49,
            Slot_50,
            Slot_51,
            Slot_52,
            Slot_53,
            Slot_54,
            Slot_55,
            Slot_56,
            Slot_57,
            Slot_58,
            Slot_59,
            Slot_60,
            Slot_61,
            Slot_62,
            Slot_63,
            Slot_64,
            Slot_65,
            Slot_66,
            Slot_67,
            Slot_68,
            Slot_69, // nice
            Slot_70,
            Slot_71,
            Slot_72,
            Slot_73,
            Slot_74,
            Slot_75,
            Slot_76,
            Slot_77,
            Slot_78,
            Slot_79,
            Slot_80,
            Slot_81,
            Slot_82,
            Slot_83,
            Slot_84,
            Slot_85,
            Slot_86,
            Slot_87,
            Slot_88,
            Slot_89,
            Slot_90,
            Slot_91,
            Slot_92,
            Slot_94,
            Slot_95,
            Slot_96,
            Slot_97,
            Slot_98,
            Slot_99,
            Slot_100,
            Slot_101,
            Slot_102,
            Slot_103,
            Slot_104,
            Slot_105,
            Slot_106,
            Slot_107,
            Slot_108,
            Slot_109,
            Slot_110,
        }
    }
}
