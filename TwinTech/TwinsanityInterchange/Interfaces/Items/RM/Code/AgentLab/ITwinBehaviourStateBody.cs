using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourStateBody : ITwinAgentLab
    {
        /// <summary>
        /// Unknown bitfield
        /// </summary>
        public UInt32 Bitfield { get; set; }
        /// <summary>
        /// Marks whether jump to a different state happens after execution finishes
        /// </summary>
        public Boolean HasStateJump { get; set; }
        /// <summary>
        /// State index to jump to
        /// </summary>
        public Int32 JumpToState { get; set; }
        /// <summary>
        /// Condition under which the commands are executed
        /// </summary>
        public TwinBehaviourCondition Condition { get; set; }
        /// <summary>
        /// Command chain
        /// </summary>
        public List<ITwinBehaviourCommand> Commands { get; set; }

        internal bool HasNext { get; set; }

        /// <summary>
        /// Output state body's text form
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="tabs"></param>
        public void WriteText(StreamWriter writer, Int32 tabs = 0);
        /// <summary>
        /// Interpret state body's text form
        /// </summary>
        /// <param name="reader"></param>
        public void ReadText(StreamReader reader);
    }
}
