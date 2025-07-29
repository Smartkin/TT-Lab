using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.AgentLab.AgentLabObjectDescs;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    public interface ITwinBehaviourGraph : ITwinBehaviour
    {
        /// <summary>
        /// Graph's name
        /// </summary>
        public String Name { get; set; }
        /// <summary>
        /// Starting state of the graph
        /// </summary>
        public Int32 StartState { get; set; }
        /// <summary>
        /// States to jump between
        /// </summary>
        public List<ITwinBehaviourState> ScriptStates { get; set; }

        /// <summary>
        /// Output the graph into its text form
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="tabs"></param>
        public void WriteText(StreamWriter writer, Int32 tabs = 0);

        /// <summary>
        /// Interpret graph from its text form
        /// </summary>
        /// <param name="reader"></param>
        public void ReadText(StreamReader reader);
        public String ToString();
    }
}
