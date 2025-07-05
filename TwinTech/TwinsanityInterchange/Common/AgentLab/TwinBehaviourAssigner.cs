using System;
using System.IO;
using System.Text;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    public class TwinBehaviourAssigner : ITwinAgentLab
    {
        public Int32 Behaviour { get; set; }
        public UInt16 GlobalObjectId { get; set; }
        public AssignTypeID AssignType { get; set; }
        public AssignLocalityID AssignLocality { get; set; }
        public AssignStatusID AssignStatus { get; set; }
        public AssignPreferenceID AssignPreference { get; set; }

        public Int32 GetLength()
        {
            return 8;
        }

        public void Compile()
        {
            return;
        }

        public void Decompile(StreamWriter writer, int tabs = 0)
        {
            StringUtils.WriteLineTabulated(writer, "assigner = {", tabs);
            if (GlobalObjectId != 65535)
            {
                StringUtils.WriteLineTabulated(writer, $"{nameof(GlobalObjectId)} = {GlobalObjectId};", tabs + 1);
            }
            StringUtils.WriteLineTabulated(writer, $"{nameof(AssignType)} = {AssignType};", tabs + 1);
            StringUtils.WriteLineTabulated(writer, $"{nameof(AssignLocality)} = {AssignLocality};", tabs + 1);
            StringUtils.WriteLineTabulated(writer, $"{nameof(AssignStatus)} = {AssignStatus};", tabs + 1);
            StringUtils.WriteLineTabulated(writer, $"{nameof(AssignPreference)} = {AssignPreference};", tabs + 1);
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
                GlobalObjectId = (UInt16)(assigner >> 0x10);
                AssignPreference = (AssignPreferenceID)(assigner >> 0xC & 0xF);
            }

        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Behaviour);
            UInt32 newAssigner = (UInt32)((GlobalObjectId & 0xFFFF) << 0x10);
            newAssigner |= (UInt32)AssignType;
            newAssigner |= (UInt32)AssignLocality << 0x4;
            newAssigner |= (UInt32)AssignStatus << 0x8;
            newAssigner |= (UInt32)AssignPreference << 0xC;
            writer.Write(newAssigner);
        }

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
