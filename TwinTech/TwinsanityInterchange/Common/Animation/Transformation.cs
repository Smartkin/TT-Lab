using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Animation
{
    public class Transformation : ITwinSerializable
    {
        Int16 transformValue;

        public Single Value
        {
            get => transformValue / 4096f;
            set => transformValue = (Int16)(value * 4096);
        }

        public Single RotationValue
        {
            get => (transformValue * 16) / (float)(UInt16.MaxValue + 1) * (float)Math.PI * 2;
            set => transformValue = (Int16)((UInt16.MaxValue + 1) * value / (16 * (float)Math.PI * 2));
        }
        
        /// <summary>
        /// Raw fixed point value as it's stored in the game's files
        /// </summary>
        public Int16 PureValue
        {
            get => transformValue;
            set => transformValue = value;
        }

        public Int32 GetLength()
        {
            return 2;
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            transformValue = reader.ReadInt16();
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(transformValue);
        }
    }
}
