using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyAIPath : BaseTwinItem, ITwinAIPath
    {
        public UInt16 PositionA { get; set; }
        public UInt16 PositionB { get; set; }
        public AiPathFlags Flags { get; set; }
        public UInt16 ChunkA { get; set; }
        public UInt16 ChunkB { get; set; }

        public override int GetLength()
        {
            return 10;
        }

        public override void Read(BinaryReader reader, int length)
        {
            PositionA = reader.ReadUInt16();
            PositionB = reader.ReadUInt16();
            Flags = (AiPathFlags)reader.ReadUInt16();
            ChunkA = reader.ReadUInt16();
            ChunkB = reader.ReadUInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(PositionA);
            writer.Write(PositionB);
            writer.Write((UInt16)Flags);
            writer.Write(ChunkA);
            writer.Write(ChunkB);
        }

        public override String GetName()
        {
            return $"AI Navigation Path {id:X}";
        }
    }
}
