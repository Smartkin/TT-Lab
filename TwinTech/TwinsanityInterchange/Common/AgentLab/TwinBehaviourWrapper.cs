using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace Twinsanity.TwinsanityInterchange.Common.AgentLab
{
    public abstract class TwinBehaviourWrapper : BaseTwinItem, ITwinBehaviour
    {
        private UInt16 scriptID;
        public Byte Priority { get; set; }

        public override int GetLength()
        {
            return 4;
        }

        public override void Compile()
        {
            base.Compile();
            scriptID = (UInt16)GetID();
        }
        
        public abstract void Decompile(StreamWriter writer, int tabs = 0);

        public override void Read(BinaryReader reader, int length)
        {
            scriptID = reader.ReadUInt16();
            Priority = reader.ReadByte();
            reader.ReadByte(); // Skip flag
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(scriptID);
            writer.Write(Priority);
            writer.Write((Byte)((id + 1) % 2));
        }
    }
}
