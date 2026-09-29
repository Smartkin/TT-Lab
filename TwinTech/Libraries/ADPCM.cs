using System;
using System.Collections.Generic;

namespace Twinsanity.Libraries
{
    /// <summary>
    /// The flags of an ADPCM block, its second byte
    /// </summary>
    [Flags]
    public enum SampleLineFlags : byte
    {
        /// <summary>
        /// A block like any other
        /// </summary>
        None = 0,
        /// <summary>
        /// The last block the SPU2 plays: the sound stops after it, or goes back to the loop's start with <see cref="Loop"/>
        /// </summary>
        LoopEnd = 1,
        /// <summary>
        /// Repeat: on the last block, play from the loop's start again instead of stopping. The game's loops set it on every block from
        /// the loop's start on
        /// </summary>
        Loop = 2,
        /// <summary>
        /// Where the loop starts: the SPU2 keeps this block's address as the one to go back to
        /// </summary>
        LoopStart = 4
    }

    /// <summary>
    /// The SPU2's ADPCM: blocks of 16 bytes holding 28 samples of one channel, the second byte flags (bit 0 the sound's or the loop's
    /// last block, bit 1 repeat from the loop's start after it instead of stopping, bit 2 the loop's first block)
    /// </summary>
    public static class ADPCM
    {
        // PCM to ADPCM conversion code is based on PS2's PS2SDK repo
        // https://github.com/ps2dev/ps2sdk/blob/master/tools/ps2adpcm/README
        //
        // ADPCM to PCM conversion is based on code by bITmASTER and nextvolume
        // https://github.com/simias/psxsdk/blob/master/tools/vag2wav.c

        /// <summary>
        /// The samples a block holds, so where a loop can start and end
        /// </summary>
        public const Int32 SamplesPerBlock = 28;
        /// <summary>
        /// The bytes of a block: the shift and filter, the flags and 14 bytes of 4 bit samples
        /// </summary>
        public const Int32 BlockSize = 16;

        private static readonly Double[,] F =
        {
            { 0.0, 0.0 },
            { -0.9375, 0.0 },
            { -1.796875, 0.8125 },
            { -1.53125, 0.859375 },
            { -1.90625, 0.9375 },
        };

        private sealed class EncoderState
        {
            public Double S1;
            public Double S2;
            public Double Ps1;
            public Double Ps2;
        }

        /// <summary>
        /// Where a channel's blocks loop, in samples: from the start of the first block flagged as the loop's start to the end of the
        /// block ending the sound, when that one repeats (flags 3, or 7 for a loop of one block). -1 for both when the sound plays once
        /// </summary>
        public static void FindLoop(ReadOnlySpan<Byte> blocks, out Int32 loopStart, out Int32 loopEnd)
        {
            loopStart = -1;
            loopEnd = -1;
            var start = -1;
            for (var block = 0; (block + 1) * BlockSize <= blocks.Length; block++)
            {
                var flags = (SampleLineFlags)blocks[block * BlockSize + 1];
                if ((flags & SampleLineFlags.LoopStart) != 0 && start == -1)
                {
                    start = block;
                }

                if ((flags & SampleLineFlags.LoopEnd) == 0)
                {
                    continue;
                }

                if ((flags & SampleLineFlags.Loop) != 0 && start != -1)
                {
                    loopStart = start * SamplesPerBlock;
                    loopEnd = (block + 1) * SamplesPerBlock;
                }

                return;
            }
        }

        /// <summary>
        /// A channel's samples: its blocks up to the one ending the sound or its loop, which holds the last samples (the blocks after
        /// it are never played, the game's sounds have a silent block there)
        /// </summary>
        public static Int16[] Decode(ReadOnlySpan<Byte> blocks)
        {
            var samples = new List<Int16>(blocks.Length / BlockSize * SamplesPerBlock);
            Double s0 = 0;
            Double s1 = 0;
            for (var block = 0; (block + 1) * BlockSize <= blocks.Length; block++)
            {
                var line = blocks.Slice(block * BlockSize, BlockSize);
                var shift = line[0] & 0xF;
                var predict = Math.Min((line[0] >> 4) & 0xF, 4);
                for (var i = 0; i < 14; i++)
                {
                    samples.Add(DecodeSample(line[i + 2] & 0xF, shift, predict, ref s0, ref s1));
                    samples.Add(DecodeSample((line[i + 2] >> 4) & 0xF, shift, predict, ref s0, ref s1));
                }

                if (((SampleLineFlags)line[1] & SampleLineFlags.LoopEnd) != 0)
                {
                    break;
                }
            }

            return samples.ToArray();
        }

        private static Int16 DecodeSample(Int32 nibble, Int32 shift, Int32 predict, ref Double s0, ref Double s1)
        {
            var sample = (Int16)(nibble << 12) >> shift;
            var value = sample - s0 * F[predict, 0] - s1 * F[predict, 1];
            s1 = s0;
            s0 = value;
            return (Int16)Math.Clamp(Math.Round(value), Int16.MinValue, Int16.MaxValue);
        }

        /// <summary>
        /// A channel's blocks, laid out like the game's sounds. A sound played once ends on a block flagged 1 and a silent block that
        /// loops on itself (7) after it. A looping sound's loop starts on a block flagged 6, the blocks after it are flagged 2 and the
        /// loop's last block 3, which is where the sound stops: the samples after the loop are never played and aren't kept. The loop's
        /// points (samples, -1 for none) are rounded down to whole blocks of 28 samples, a loop is at least one block long
        /// </summary>
        public static Byte[] Encode(ReadOnlySpan<Int16> samples, Int32 loopStart, Int32 loopEnd)
        {
            var blocks = (samples.Length + SamplesPerBlock - 1) / SamplesPerBlock;
            var loopStartBlock = -1;
            if (loopStart >= 0 && loopEnd > loopStart && loopStart / SamplesPerBlock < blocks)
            {
                loopStartBlock = loopStart / SamplesPerBlock;
                blocks = Math.Max(Math.Min(loopEnd, samples.Length) / SamplesPerBlock, loopStartBlock + 1);
            }

            var output = new Byte[(loopStartBlock == -1 ? Math.Max(blocks, 1) + 1 : blocks) * BlockSize];
            var state = new EncoderState();
            var block = new Double[SamplesPerBlock];
            for (var index = 0; index < Math.Max(blocks, 1); index++)
            {
                for (var i = 0; i < SamplesPerBlock; i++)
                {
                    var sample = index * SamplesPerBlock + i;
                    block[i] = sample < samples.Length ? samples[sample] : 0.0;
                }

                var last = index == Math.Max(blocks, 1) - 1;
                var flags = SampleLineFlags.None;
                if (loopStartBlock == -1)
                {
                    flags = last ? SampleLineFlags.LoopEnd : SampleLineFlags.None;
                }
                else if (index >= loopStartBlock)
                {
                    flags = SampleLineFlags.Loop;
                    if (index == loopStartBlock)
                    {
                        flags |= SampleLineFlags.LoopStart;
                    }

                    if (last)
                    {
                        flags |= SampleLineFlags.LoopEnd;
                    }
                }

                EncodeBlock(state, block, flags, output.AsSpan(index * BlockSize, BlockSize));
            }

            if (loopStartBlock == -1)
            {
                output[output.Length - BlockSize + 1] = (Byte)(SampleLineFlags.LoopStart | SampleLineFlags.Loop | SampleLineFlags.LoopEnd);
            }

            return output;
        }

        private static void EncodeBlock(EncoderState state, Double[] samples, SampleLineFlags flags, Span<Byte> output)
        {
            FindPredict(state, samples, out var predict, out var shift);
            var nibbles = Pack(state, samples, predict, shift);
            output[0] = (Byte)(shift | (predict << 4));
            output[1] = (Byte)flags;
            for (var i = 0; i < 14; ++i)
            {
                output[i + 2] = (Byte)(((nibbles[(i * 2) + 1] >> 8) & 0xF0) | ((nibbles[i * 2] >> 12) & 0xF));
            }
        }

        // The filter whose residue is the smallest, the residue replacing the samples, and the shift that fits it into 4 bits
        private static void FindPredict(EncoderState state, Double[] samples, out Int32 predict, out Int32 shift)
        {
            var buffer = new Double[SamplesPerBlock, 5];
            Double s1 = 0.0;
            Double s2 = 0.0;
            var min = 1e10;
            predict = 0;
            for (var i = 0; i < 5; ++i)
            {
                var max = 0.0;
                s1 = state.S1;
                s2 = state.S2;
                for (var j = 0; j < SamplesPerBlock; ++j)
                {
                    var s0 = Math.Clamp(samples[j], -30720.0, 30719.0);
                    var ds = s0 + s1 * F[i, 0] + s2 * F[i, 1];
                    buffer[j, i] = ds;
                    max = Math.Max(max, Math.Abs(ds));
                    s2 = s1;
                    s1 = s0;
                }

                if (max < min)
                {
                    min = max;
                    predict = i;
                }

                if (min <= 7)
                {
                    predict = 0;
                    break;
                }
            }

            state.S1 = s1;
            state.S2 = s2;
            for (var i = 0; i < SamplesPerBlock; ++i)
            {
                samples[i] = buffer[i, predict];
            }

            var min2 = (Int32)min;
            var shiftMask = 0x4000;
            shift = 0;
            while (shift < 12)
            {
                if ((shiftMask & (min2 + (shiftMask >> 3))) != 0)
                {
                    break;
                }

                shift++;
                shiftMask >>= 1;
            }
        }

        private static Int16[] Pack(EncoderState state, Double[] samples, Int32 predict, Int32 shift)
        {
            var s1 = state.Ps1;
            var s2 = state.Ps2;
            var nibbles = new Int16[SamplesPerBlock];
            for (var i = 0; i < SamplesPerBlock; ++i)
            {
                var s0 = samples[i] + s1 * F[predict, 0] + s2 * F[predict, 1];
                var ds = s0 * (1 << shift);
                var di = (Int32)(((Int32)ds + 0x800) & 0xFFFFF000);
                di = Math.Clamp(di, -32768, 32767);
                nibbles[i] = (Int16)di;
                di >>= shift;
                s2 = s1;
                s1 = di - s0;
            }

            state.Ps1 = s1;
            state.Ps2 = s2;
            return nibbles;
        }
    }
}
