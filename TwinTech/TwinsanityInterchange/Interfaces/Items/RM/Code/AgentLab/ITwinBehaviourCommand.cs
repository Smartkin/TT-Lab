using System;
using System.Collections.Generic;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab
{
    /// <summary>
    /// Agent lab's version
    /// </summary>
    public enum AgentLabVersion
    {
        PS2,
        Xbox,
        /// <summary>
        /// Currently unimplemented
        /// </summary>
        Demo
    }

    public interface ITwinBehaviourCommand : ITwinAgentLab
    {
        public UInt32 Bitfield { get; set; }
        /// <summary>
        /// Command function to call from game's list of delegates
        /// </summary>
        public UInt16 CommandIndex { get; set; }
        /// <summary>
        /// Function arguments
        /// </summary>
        public List<UInt32> Arguments { get; set; }
        /// <summary>
        /// Size of the command
        /// </summary>
        public UInt32 CommandSize { get; }
        /// <summary>
        /// Agent lab's version of this command
        /// </summary>
        public AgentLabVersion Version { get; }

        internal bool HasNext { get; set; }

        /// <summary>
        /// Read the binary format of the command
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="length"></param>
        /// <param name="commands">Command chain</param>
        public void Read(BinaryReader reader, int length, IList<ITwinBehaviourCommand> commands);

        /// <summary>
        /// Output the command in its text form
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="tabs"></param>
        public void WriteText(StreamWriter writer, Int32 tabs = 0);

        /// <summary>
        /// Interpret command from its text form
        /// </summary>
        /// <param name="line"></param>
        public void ReadText(String line);
    }
}
