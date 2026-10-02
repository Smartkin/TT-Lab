using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout
{
    public class PS2AnyAIPosition : BaseTwinItem, ITwinAIPosition
    {
        public Vector4 Position { get; set; }
        /// <summary>
        /// Bits 1, 2 and 4 on some positions of the retail levels, 0 on most. No code of the PAL executable reads AI positions'
        /// values, the section only fills a table
        /// </summary>
        public AiPositionFlags Flags { get; set; }

        public PS2AnyAIPosition()
        {
            Position = new Vector4();
        }

        public override int GetLength()
        {
            return Constants.SIZE_VECTOR4 + 2;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Position.Read(reader, Constants.SIZE_VECTOR4);
            Flags = (AiPositionFlags)reader.ReadUInt16();
        }
        public override void Write(BinaryWriter writer)
        {
            Position.Write(writer);
            writer.Write((UInt16)Flags);
        }

        public override String GetName()
        {
            return $"AI Navigation Position {id:X}";
        }
    }
}
