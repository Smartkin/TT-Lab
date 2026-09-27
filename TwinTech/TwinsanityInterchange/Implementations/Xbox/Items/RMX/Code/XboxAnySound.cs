using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code
{
    public class XboxAnySound : BaseTwinItem, ITwinSound
    {
        UInt32 frequency;

        public UInt32 Header { get; set; }
        public Byte UnkFlag { get; set; }
        public Byte FreqFac { get; set; }
        public UInt16 Param1 { get; set; }
        public UInt16 Param2 { get; set; }
        public UInt16 Param3 { get; set; }
        public UInt16 Param4 { get; set; }
        public Byte[] Sound { get; set; }

        static readonly byte[] headerStatic1 = new byte[] { 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00 };
        static readonly byte[] headerStatic2 = new byte[] { 0x02, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

        public override Int32 GetLength()
        {
            return 0x4C + Sound.Length;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            Header = reader.ReadUInt32();
            frequency = reader.ReadUInt32();
            reader.ReadBytes(headerStatic1.Length + headerStatic2.Length + 8);
            // 16 bit PCM samples
            var soundSize = reader.ReadInt32();
            Sound = reader.ReadBytes(soundSize);
            reader.ReadBytes(8); // SoundSize and zero
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Header);
            writer.Write(frequency);
            writer.Write(headerStatic1);
            writer.Write(frequency);
            writer.Write(frequency * 2);
            writer.Write(headerStatic2);
            writer.Write(Sound.Length);
            writer.Write(Sound);
            writer.Write(Sound.Length);
            writer.Write(0U);
        }

        public void SetFreq(UInt16 freq)
        {
            frequency = freq;
        }

        public UInt16 GetFreq()
        {
            return (UInt16)frequency;
        }

        public Byte[] ToPCM()
        {
            return Sound;
        }

        public void SetDataFromPCM(Byte[] data)
        {
            Sound = data;
        }

        public override String GetName()
        {
            return $"Sound {id:X}";
        }
    }
}
