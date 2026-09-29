using System;
using System.IO;
using System.Text;
using Twinsanity.AgentLab.Resolvers;
using Twinsanity.AgentLab.Resolvers.Interfaces;
using Twinsanity.AgentLab.Resolvers.Interfaces.Decompiler;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    /// <summary>
    /// One receiver of a starter: which agent takes part when the behaviour starts and, for the first one, the graph run on it
    /// </summary>
    public class TwinBehaviourAssigner : ITwinAgentLab
    {
        /// <summary>
        /// Index of the behaviour graph the receiver runs, 0 for none. The game's starters have their own graph in the first assigner
        /// </summary>
        public Int32 Behaviour { get; set; }
        /// <summary>
        /// Which instance a GLOBAL_AGENT assigner is: the low byte of the instance's RefListIndex. 65535 when not used
        /// </summary>
        public UInt16 RefListIndex { get; set; }
        public AssignTypeID AssignType { get; set; }
        /// <summary>
        /// Never read by the retail game, its scripts all have ANYWHERE
        /// </summary>
        public AssignLocalityID AssignLocality { get; set; } = AssignLocalityID.ANYWHERE;
        /// <summary>
        /// Never read by the retail game, its scripts all have ANYSTATE
        /// </summary>
        public AssignStatusID AssignStatus { get; set; } = AssignStatusID.ANYSTATE;
        /// <summary>
        /// Never read by the retail game, its scripts all have ANYHOW
        /// </summary>
        public AssignPreferenceID AssignPreference { get; set; } = AssignPreferenceID.ANYHOW;

        public const UInt16 NoRefListIndex = 65535;

        public TwinBehaviourAssigner()
        {
            RefListIndex = NoRefListIndex;
        }

        public Int32 GetLength()
        {
            return 8;
        }

        public void Compile()
        {
            return;
        }

        public void Decompile(IResolver resolver, StreamWriter writer, int tabs = 0)
        {
            StringUtils.WriteLineTabulated(writer, "assigner = {", tabs);
            StringUtils.WriteLineTabulated(writer, $"{nameof(AssignType)} = {AssignType};", tabs + 1);
            if (RefListIndex != NoRefListIndex)
            {
                StringUtils.WriteLineTabulated(writer, $"{nameof(RefListIndex)} = {RefListIndex};", tabs + 1);
            }
            if (AssignLocality != AssignLocalityID.ANYWHERE)
            {
                StringUtils.WriteLineTabulated(writer, $"{nameof(AssignLocality)} = {AssignLocality};", tabs + 1);
            }
            if (AssignStatus != AssignStatusID.ANYSTATE)
            {
                StringUtils.WriteLineTabulated(writer, $"{nameof(AssignStatus)} = {AssignStatus};", tabs + 1);
            }
            if (AssignPreference != AssignPreferenceID.ANYHOW)
            {
                StringUtils.WriteLineTabulated(writer, $"{nameof(AssignPreference)} = {AssignPreference};", tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, "}", tabs);
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            Behaviour = reader.ReadInt32();
            var assigner = reader.ReadUInt32();
            {
                AssignType = (AssignTypeID)(assigner & 0xF);
                AssignLocality = (AssignLocalityID)(assigner >> 0x4 & 0xF);
                AssignStatus = (AssignStatusID)(assigner >> 0x8 & 0xF);
                RefListIndex = (UInt16)(assigner >> 0x10);
                AssignPreference = (AssignPreferenceID)(assigner >> 0xC & 0xF);
            }

        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Behaviour);
            UInt32 newAssigner = (UInt32)((RefListIndex & 0xFFFF) << 0x10);
            newAssigner |= (UInt32)AssignType;
            newAssigner |= (UInt32)AssignLocality << 0x4;
            newAssigner |= (UInt32)AssignStatus << 0x8;
            newAssigner |= (UInt32)AssignPreference << 0xC;
            writer.Write(newAssigner);
        }

        /// <summary>
        /// Which agent an assigner refers to (bits 0-3). The game resolves ME to the instance itself, GLOBAL_AGENT to the
        /// instance registered under <see cref="RefListIndex"/>, HUMAN_PLAYER to the player and ORIGINATOR to the caller.
        /// LINKED_OBJECT would be a linked object by its slot, the game's scripts never use it
        /// </summary>
        public enum AssignTypeID
        {
            ME = 0,
            //OBJECT_CHILD, Cut from retail
            LINKED_OBJECT = 2,
            GLOBAL_AGENT,
            HUMAN_PLAYER,
            // BACKGROUND_CHARACTER, Cut from retail
            // ANYBODY, Cut from retail
            // GENERATE_AGENT, Cut from retail
            ORIGINATOR = 8,
        }
        public enum AssignLocalityID
        {
            NEARBY = 0,
            LOCAL,
            GLOBAL,
            ANYWHERE,
        }
        public enum AssignStatusID
        {
            IDLE = 0,
            BUSY,
            ANYSTATE,
        }
        public enum AssignPreferenceID
        {
            NEAREST = 0,
            FURTHEST,
            STRONGEST,
            WEAKEST,
            BEST_ALIGNED,
            ANYHOW,
        }
    }
}
