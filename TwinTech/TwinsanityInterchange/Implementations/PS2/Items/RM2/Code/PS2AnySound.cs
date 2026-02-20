using System;
using System.IO;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code
{
    public class PS2AnySound : BaseTwinItem, ITwinSound
    {
        internal Int32 offset;

        public UInt32 Header { get; set; }
        public Byte UnkFlag { get; set; }
        public Byte FreqFac { get; set; }
        public UInt16 Param1 { get; set; }
        public UInt16 Param2 { get; set; }
        public UInt16 Param3 { get; set; }
        public UInt16 Param4 { get; set; }
        public Byte[] Sound { get; set; }

        public override Int32 GetLength()
        {
            return 22;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            Header = reader.ReadUInt32();
            UnkFlag = reader.ReadByte();
            FreqFac = reader.ReadByte();
            Param1 = reader.ReadUInt16();
            Param2 = reader.ReadUInt16();
            Param3 = reader.ReadUInt16();
            Param4 = reader.ReadUInt16();
            var soundSize = reader.ReadUInt32();
            if ((Header & 1) == 0)
            {
                soundSize *= 2;
            }
            Sound = new Byte[soundSize];
            offset = (int)reader.ReadUInt32(); // Discard offset
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write(Header);
            writer.Write(UnkFlag);
            writer.Write(FreqFac);
            writer.Write(Param1);
            writer.Write(Param2);
            writer.Write(Param3);
            writer.Write(Param4);
            if ((Header & 1) == 0)
            {
                writer.Write(Sound.Length / 2);
            }
            else
            {
                writer.Write(Sound.Length);
            }
            writer.Write(offset);
        }

        public override String GetName()
        {
            return $"Sound {id:X}";
        }

        public void SetFreq(UInt16 freq)
        {
            FreqFac = freq switch
            {
                8000 => 0x2,
                10000 => 0x3,
                11025 => 0x4,
                16000 => 0x5,
                18000 => 0x6,
                22050 => 0x7,
                32000 => 0xA,
                44100 => 0xE,
                48000 => 0x10,
                _ => throw new ArgumentException($"Unsupported sfx frequency. Value was: {freq}", nameof(freq))
            };
        }

        public UInt16 GetFreq()
        {
            return FreqFac switch
            {
                0x2 => 8000,
                0x3 => 10000,
                0x4 => 11025,
                0x5 => 16000,
                0x6 => 18000,
                0x7 => 22050,
                0xA => 32000,
                0xE => 44100,
                0x10 => 48000,
                _ => throw new ArgumentException($"Unhandled sfx frequency. Value was: {FreqFac}", "freq"),
            };
        }

        public Byte[] ToPCM()
        {
            ADPCM adpcm = new();
            using MemoryStream input = new(Sound);
            using MemoryStream output = new();
            BinaryReader reader = new(input);
            BinaryWriter writer = new(output);
            if ((Header & 1) == 0)
            {
                adpcm.ToPCMStereo(reader, writer, Sound.Length / 2);
            }
            else
            {
                adpcm.ToPCMMono(reader, writer);
            }

            return output.ToArray();
        }

        public void SetDataFromPCM(Byte[] data)
        {
            ADPCM adpcm = new();
            using MemoryStream input = new(data);
            using MemoryStream output = new();
            BinaryReader reader = new(input);
            BinaryWriter writer = new(output);
            if ((Header & 1) == 0)
            {
                adpcm.ToADPCMStereo(reader, writer);
            }
            else
            {
                adpcm.ToADPCMMono(reader, writer);
            }

            Sound = output.ToArray();
        }
    }
}
