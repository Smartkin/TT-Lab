using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourState : ITwinSerializable
    {
        /// <summary>
        /// Packages unsolved flags
        /// </summary>
        public UInt16 Bitfield { get; set; }
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
    }
}
