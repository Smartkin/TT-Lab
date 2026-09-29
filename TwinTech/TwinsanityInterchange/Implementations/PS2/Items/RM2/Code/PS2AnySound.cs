using System;
using System.IO;
using System.Runtime.InteropServices;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code
{
    public class PS2AnySound : BaseTwinItem, ITwinSound
    {
        internal Int32 offset;

        public UInt32 Header { get; set; }
        public UInt16 Pitch { get; set; }
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
            Pitch = reader.ReadUInt16();
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
            writer.Write(Pitch);
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
            Pitch = ITwinSound.PitchOf(freq);
        }

        public UInt16 GetFreq()
        {
            return ITwinSound.SampleRateOf(Pitch);
        }

        // A stereo sound is its left channel's blocks followed by its right channel's, both loop the same
        private Boolean IsStereo => (Header & 1) == 0;

        /// <inheritdoc/>
        public Int32 LoopStart
        {
            get
            {
                ADPCM.FindLoop(IsStereo ? Sound.AsSpan(0, Sound.Length / 2) : Sound, out var start, out _);
                return start;
            }
        }

        /// <inheritdoc/>
        public Int32 LoopEnd
        {
            get
            {
                ADPCM.FindLoop(IsStereo ? Sound.AsSpan(0, Sound.Length / 2) : Sound, out _, out var end);
                return end;
            }
        }

        public Byte[] ToPCM()
        {
            if (!IsStereo)
            {
                return MemoryMarshal.AsBytes(ADPCM.Decode(Sound).AsSpan()).ToArray();
            }

            var left = ADPCM.Decode(Sound.AsSpan(0, Sound.Length / 2));
            var right = ADPCM.Decode(Sound.AsSpan(Sound.Length / 2));
            var samples = new Int16[Math.Min(left.Length, right.Length) * 2];
            for (var i = 0; i < samples.Length / 2; i++)
            {
                samples[i * 2] = left[i];
                samples[i * 2 + 1] = right[i];
            }

            return MemoryMarshal.AsBytes(samples.AsSpan()).ToArray();
        }

        public void SetDataFromPCM(Byte[] data, Int32 loopStart = -1, Int32 loopEnd = -1)
        {
            var samples = MemoryMarshal.Cast<Byte, Int16>(data.AsSpan(0, data.Length & ~1));
            if (!IsStereo)
            {
                Sound = ADPCM.Encode(samples, loopStart, loopEnd);
                return;
            }

            var left = new Int16[samples.Length / 2];
            var right = new Int16[samples.Length / 2];
            for (var i = 0; i < left.Length; i++)
            {
                left[i] = samples[i * 2];
                right[i] = samples[i * 2 + 1];
            }

            var leftBlocks = ADPCM.Encode(left, loopStart, loopEnd);
            var rightBlocks = ADPCM.Encode(right, loopStart, loopEnd);
            Sound = new Byte[leftBlocks.Length + rightBlocks.Length];
            leftBlocks.CopyTo(Sound, 0);
            rightBlocks.CopyTo(Sound, leftBlocks.Length);
        }
    }
}
