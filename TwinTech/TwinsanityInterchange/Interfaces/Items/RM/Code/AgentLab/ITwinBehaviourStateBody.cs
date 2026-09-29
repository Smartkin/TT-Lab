using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourStateBody : ITwinAgentLab
    {
        /// <summary>
        /// The flags as read: bits 0-7 command count, 8 restarts the state, 9 has a condition, 10 has a jump, 11 has a next body
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
        /// The jump counts as entering a new state even when it goes to the state the body is in: the state's child behaviour
        /// starts over and its timer resets. The game's scripts only set it on bodies jumping to their own state
        /// </summary>
        public Boolean RestartsState { get; set; }
        /// <summary>
        /// Condition under which the commands are executed. Every body of the game's scripts has one, the completion bodies
        /// have the one that always passes
        /// </summary>
        public TwinBehaviourCondition Condition { get; set; }
        /// <summary>
        /// Command chain
        /// </summary>
        public List<ITwinBehaviourCommand> Commands { get; set; }
        /// <summary>
        /// The body is the first of a state with a completion body, it runs when the state's control packet or child behaviour
        /// finishes instead of on its condition
        /// </summary>
        public Boolean IsCompletionBody { get; set; }

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
