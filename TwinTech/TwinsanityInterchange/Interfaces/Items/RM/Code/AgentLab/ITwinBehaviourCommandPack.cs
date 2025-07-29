using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.AgentLab.AgentLabObjectDescs;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourCommandPack : ITwinAgentLab
    {
        /// <summary>
        /// Command chain
        /// </summary>
        List<ITwinBehaviourCommand> Commands { get; set; }

        /// <summary>
        /// Output the pack in its text form
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="tabs"></param>
        void WriteText(StreamWriter writer, Int32 tabs = 0);
        /// <summary>
        /// Interpret the pack from its text form
        /// </summary>
        /// <param name="reader"></param>
        bool ReadText(StreamReader reader);
        String ToString();
    }
}
