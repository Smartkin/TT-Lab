using System;
using System.IO;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    internal class MBRecord : ITwinSerializable
    {
        private MHRecord RecordHeader;

        // Validation stuff
        private readonly Char[] MSVp = "MSVp".ToCharArray();
        private readonly UInt32 Key = 0x20000000;

        public Int32 SampleRate;
        public String Name;
        public Byte[] TrackData;

        public MBRecord(MHRecord header)
        {
            RecordHeader = header;
        }

        public Int32 GetLength()
        {
            return RecordHeader.Type switch
            {
                PS2MB.RecordType.MONO or PS2MB.RecordType.STEREO => RecordHeader.Size,
                _ => 0,
            };
        }

        public void Compile()
        {
            return;
        }

        public void Read(BinaryReader reader, Int32 length)
        {
            switch (RecordHeader.Type)
            {
                case PS2MB.RecordType.MONO:
                    var msvp = GameText.ReadChars(reader, 4);
                    if (!String.Equals(new String(msvp), new String(MSVp), StringComparison.Ordinal))
                    {
                        throw new Exception("MSVp key not provided!");
                    }
                    var testKey = reader.ReadUInt32();
                    var testZero = reader.ReadInt32();
                    if (testKey != Key || testZero != 0)
                    {
                        throw new Exception("Invalid key provided!");
                    }
                    var testSize = BitConv.FlipBytes(reader.ReadUInt32());
                    if (testSize != GetLength() - 0x30)
                    {
                        throw new Exception("Sizes in header and main archives do not match!");
                    }
                    SampleRate = BitConv.FlipBytes(reader.ReadInt32());
                    reader.ReadInt32();
                    reader.ReadInt64();
                    Name = GameText.ReadString(reader, 0x10);
                    TrackData = reader.ReadBytes(GetLength() - 0x30);
                    break;
                default:
                    TrackData = reader.ReadBytes(GetLength());
                    break;
            }
        }

        public void Write(BinaryWriter writer)
        {
            switch (RecordHeader.Type)
            {
                case PS2MB.RecordType.MONO:
                    writer.Write(MSVp, 0, 4);
                    writer.Write(Key);
                    writer.Write(0);
                    writer.Write(BitConv.FlipBytes(GetLength() - 0x30));
                    writer.Write(BitConv.FlipBytes(SampleRate));
                    writer.Write(0);
                    writer.Write((Int64)0);
                    GameText.Write(writer, Name.ToCharArray(), 0, 0x10);
                    break;
            }
            writer.Write(TrackData);
        }
    }
}
