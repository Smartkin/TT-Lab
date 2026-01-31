using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    public class DirectionalLight : Light
    {
        public Vector4 Direction;
        public Int16 UnkShort;

        public DirectionalLight() : base()
        {
            Direction = new Vector4();
        }

        public override Int32 GetLength()
        {
            return base.GetLength() + 2 + Constants.SIZE_VECTOR4;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            Direction.Read(reader, Constants.SIZE_VECTOR4);
            UnkShort = reader.ReadInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            Direction.Write(writer);
            writer.Write(UnkShort);
        }
    }
}
