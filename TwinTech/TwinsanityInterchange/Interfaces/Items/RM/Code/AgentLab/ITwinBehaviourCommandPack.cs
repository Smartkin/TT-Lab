using System;
using System.Collections.Generic;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourCommandPack : ITwinAgentLab
    {
        /// <summary>
        /// Command chain
        /// </summary>
        public List<ITwinBehaviourCommand> Commands { get; set; }

        /// <summary>
        /// Output the pack in its text form
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="tabs"></param>
        public void WriteText(StreamWriter writer, Int32 tabs = 0);
        /// <summary>
        /// Interpret the pack from its text form
        /// </summary>
        /// <param name="reader"></param>
        public bool ReadText(StreamReader reader);
        public String ToString();
    }
}
