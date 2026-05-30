using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class TwinIntegerRotation : ITwinSerializable
    {
        public UInt16 Angle { get; set; }
        public UInt16 Fract { get; set; }
        public int GetLength()
        {
            return Constants.SIZE_UINT32;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, int length)
        {
            Angle = reader.ReadUInt16();
            Fract = reader.ReadUInt16();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Angle);
            writer.Write(Fract);
        }

        public Single GetRotation()
        {
            var result = Angle / (Single)UInt16.MaxValue * 360.0f;
            return result;
        }

        public void SetRotation(Single angle)
        {
            var writeAngle = angle;
            Angle = (UInt16)(Math.Abs(writeAngle) / 360.0f * UInt16.MaxValue);
            Fract = writeAngle > 180.0f ? UInt16.MaxValue : (ushort)0;
        }
    }
}
