using System;
using System.Collections.Generic;
using System.IO;

namespace Twinsanity.Libraries
{
    [Flags]
    public enum SampleLineFlags : byte
    {
        None = 0,
        LoopEnd = 1,
        Loop = 2,
        LoopStart = 4
    }
    public class ADPCM
    {
        // PCM to ADPCM conversion code is based on PS2's PS2SDK repo
        // https://github.com/ps2dev/ps2sdk/blob/master/tools/ps2adpcm/README
        //
        // ADPCM to PCM conversion is based on code by bITmASTER and nextvolume
        // https://github.com/simias/psxsdk/blob/master/tools/vag2wav.c

        private class PcmBuffer
        {
            public int GetPcm(ref double[] output, int len)
            {
                int i;
                for (i = 0; i < len; i++)
                {
                    if (Position + i >= SampleCount)
                    {
                        break;
                    }
                    
                    output[i] = Sample[((Position + i) * ChannelCount) + Channel];
                }

                Position += i;

                return i;
            }
            
            public int Position;
            public int Channel;
            public int ChannelCount;
            public List<short> Sample = new();
            public int SampleCount;
        }

        private class AdpcmSetup
        {
            public AdpcmSetup(BinaryWriter writer, PcmBuffer buffer, int loopStart)
            {
                _adpcmWriter = writer;
                
                PcmBuffer = buffer;
                if (loopStart < 0)
                {
                    LoopStart = -1;
                }
                else
                {
                    LoopStart = loopStart;
                }
            }

            public void WriteAdpcm(AdpcmBlock block)
            {
                _adpcmWriter.Write((byte)(block.Shift | (block.Predict << 4)));
                _adpcmWriter.Write(block.Flags);
                _adpcmWriter.Write(block.Sample);
            }

            public PcmBuffer PcmBuffer;
            public double S1 = 0.0;
            public double S2 = 0.0;
            public double Ps1 = 0.0;
            public double Ps2 = 0.0;
            public int CurBlock = 0;
            public int LoopStart;
            public bool IsPadding = false;

            private BinaryWriter _adpcmWriter;
        }

        private class AdpcmBlock
        {
            public const byte ADPCM_LOOP_END = 1;
            public const byte ADPCM_LOOP = 2;
            public const byte ADPCM_LOOP_START = 4;
            
            public byte Shift; // 4 bits long
            public byte Predict; // 4 bits long
            public byte Flags;
            public byte[] Sample = new byte[14]; // 4 bits each
        }

        private static void PutAdpcm(BinaryWriter writer, ReadOnlySpan<byte> data, int len)
        {
            writer.Write(data[..len]);
        }

        private static readonly int CHUNK_SIZE = 8192;
        private static readonly int BUFFER_SIZE = CHUNK_SIZE * 28;
        private static readonly List<KeyValuePair<float, float>> F = new List<KeyValuePair<float, float>>
        {
            new(0.0f, 0.0f),
            new(-0.9375f, 0.0f),
            new(-1.796875f, 0.8125f),
            new(-1.53125f, 0.859375f),
            new(-1.90625f, 0.9375f),
        };
        
        private static readonly List<KeyValuePair<float, float>> RevF = new List<KeyValuePair<float, float>>
        {
            new(0.0f, 0.0f),
            new(0.9375f, 0.0f),
            new(1.796875f, -0.8125f),
            new(1.53125f, -0.859375f),
            new(1.90625f, -0.9375f),
        };

        private int AdpcmEncode(AdpcmSetup setup, int blocks)
        {
            var adpcm = new AdpcmBlock();
            var samples = new double[28];
            var procBlocks = 0;

            for (procBlocks = 0; procBlocks < blocks; ++procBlocks)
            {
                adpcm.Flags = 0;

                for (var j = 0; j < 28; ++j)
                {
                    samples[j] = 0.0;
                }

                var ret = setup.PcmBuffer.GetPcm(ref samples, 28);
                if (ret < 0)
                {
                    return -1;
                }

                if (ret < 28)
                {
                    adpcm.Flags = AdpcmBlock.ADPCM_LOOP_END;
                }

                if (setup.LoopStart >= 0)
                {
                    adpcm.Flags |= AdpcmBlock.ADPCM_LOOP;
                    if (setup.CurBlock == setup.LoopStart)
                    {
                        adpcm.Flags |= AdpcmBlock.ADPCM_LOOP_START;
                    }
                }
                
                FindPredict(ref setup, ref adpcm, ref samples);
                Pack(ref setup, ref adpcm, samples);
                
                setup.WriteAdpcm(adpcm);
                setup.CurBlock++;
                if (ret < 28)
                {
                    break;
                }
            }

            if (setup.LoopStart < 0 && procBlocks < blocks)
            {
                adpcm.Predict = 0;
                adpcm.Shift = 0;
                adpcm.Flags = AdpcmBlock.ADPCM_LOOP_START | AdpcmBlock.ADPCM_LOOP | AdpcmBlock.ADPCM_LOOP_END;
                for (var i = 0; i < 14; ++i)
                {
                    adpcm.Sample[i] = 0;
                }
                
                setup.WriteAdpcm(adpcm);
                setup.CurBlock++;
                procBlocks++;
            }

            if (procBlocks < blocks && setup.IsPadding)
            {
                var padBlocks = blocks - (setup.CurBlock % blocks);
                
                adpcm.Predict = 0;
                adpcm.Shift = 0;
                adpcm.Flags = AdpcmBlock.ADPCM_LOOP_START | AdpcmBlock.ADPCM_LOOP | AdpcmBlock.ADPCM_LOOP_END;
                for (var i = 0; i < 14; ++i)
                {
                    adpcm.Sample[i] = 0;
                }

                for (var i = 0; i < padBlocks; ++i)
                {
                    setup.WriteAdpcm(adpcm);
                    setup.CurBlock++;
                }
            }

            return procBlocks;
        }

        private void FindPredict(ref AdpcmSetup setup, ref AdpcmBlock adpcm, ref double[] samples)
        {
            var max = new double[5];
            var buffer = new double[28][];
            for (var i = 0; i < 28; ++i)
                buffer[i] = new double[5];
            double s1 = 0.0, s2 = 0.0, min = 1e10;

            for (var i = 0; i < 5; ++i)
            {
                max[i] = 0.0;
                s1 = setup.S1;
                s2 = setup.S2;

                for (var j = 0; j < 28; ++j)
                {
                    var s0 = Math.Clamp(samples[j], -30720.0, 30719.0);
                    var ds = s0 + s1 * F[i].Key + s2 * F[i].Value;
                    buffer[j][i] = ds;
                    if (Math.Abs(ds) > max[i])
                    {
                        max[i] = Math.Abs(ds);
                    }

                    s2 = s1;
                    s1 = s0;
                }

                if (max[i] < min)
                {
                    min = max[i];
                    adpcm.Predict = (byte)i;
                }

                if (min <= 7)
                {
                    adpcm.Predict = 0;
                    break;
                }
            }

            setup.S1 = s1;
            setup.S2 = s2;

            for (var i = 0; i < 28; ++i)
            {
                samples[i] = buffer[i][adpcm.Predict];
            }

            var min2 = (int)min;
            var shiftMask = 0x4000;
            adpcm.Shift = 0;

            while (adpcm.Shift < 12)
            {
                if ((shiftMask & (min2 + (shiftMask >> 3))) != 0)
                {
                    break;
                }
                
                adpcm.Shift++;
                shiftMask >>= 1;
            }
        }

        private void Pack(ref AdpcmSetup setup, ref AdpcmBlock adpcm, double[] samples)
        {
            var s1 = setup.Ps1;
            var s2 = setup.Ps2;
            var fourBit = new short[28];

            for (var i = 0; i < 28; ++i)
            {
                var s0 = samples[i] + s1 * F[adpcm.Predict].Key + s2 * F[adpcm.Predict].Value;
                var ds = s0 * (1 << adpcm.Shift);
                var di = (int)(((int)ds + 0x800) & 0xFFFFF000);
                di = Math.Clamp(di, -32768, 32767);
                
                fourBit[i] = (short)di;
                
                di >>= adpcm.Shift;
                s2 = s1;
                s1 = di - s0;
            }

            for (var i = 0; i < 14; ++i)
            {
                adpcm.Sample[i] = (byte)(((fourBit[(i * 2) + 1] >> 8) & 0xF0) | ((fourBit[i * 2] >> 12) & 0xF));
            }
            
            setup.Ps1 = s1;
            setup.Ps2 = s2;
        }

        private short SampleToPCM(int sample, int factor, int predict, ref float s0, ref float s1)
        {
            sample <<= 12;
            sample = (short)sample;
            sample >>= factor;
            float value = sample;
            value += s0 * RevF[predict].Key;
            value += s1 * RevF[predict].Value;
            s1 = s0;
            s0 = value;
            return (short)Math.Round(value);
        }
        
        private SampleLineFlags LineToPCM(BinaryReader reader, BinaryWriter writer, ref float s0, ref float s1)
        {
            Byte startByte = reader.ReadByte();
            SampleLineFlags flags = (SampleLineFlags)reader.ReadByte();
            int factor = startByte & 0xF;
            int predict = (startByte >> 4) & 0xF;
            if ((flags & SampleLineFlags.LoopEnd) == 0)
            {
                for (int i = 0; i < 14; i++)
                {
                    Byte src = reader.ReadByte();
                    int low = src & 0xF;
                    int high = (src & 0xF0) >> 4;
                    short l = SampleToPCM(low, factor, predict, ref s0, ref s1);
                    short h = SampleToPCM(high, factor, predict, ref s0, ref s1);
                    writer.Write(l);
                    writer.Write(h);
                }
            }
            return flags;
        }
        
        public void ToADPCMMono(BinaryReader reader, BinaryWriter writer)
        {
            var pcmBuffer = new PcmBuffer
            {
                ChannelCount = 1,
                Channel = 0,
                Position = 0
            };
            var setup = new AdpcmSetup(writer, pcmBuffer, -1);
            
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                pcmBuffer.Sample.Clear();
                for (var i = 0; i < BUFFER_SIZE; ++i)
                {
                    pcmBuffer.Sample.Add(reader.ReadInt16());
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    {
                        break;
                    }
                }
                pcmBuffer.SampleCount = pcmBuffer.Sample.Count;
                
                AdpcmEncode(setup, CHUNK_SIZE);
            }
        }

        public void ToADPCMStereo(BinaryReader reader, BinaryWriter writer)
        {
            var pcmBuffer = new PcmBuffer
            {
                ChannelCount = 2
            };
            var setups = new AdpcmSetup[2];
            setups[0] = new AdpcmSetup(writer, pcmBuffer, -1)
            {
                IsPadding = true
            };
            setups[1] = new AdpcmSetup(writer, pcmBuffer, -1)
            {
                IsPadding = true
            };

            var bufferSize = (int)reader.BaseStream.Length / 4;
            var chunkSize = (bufferSize / 28) + 1;
            
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                pcmBuffer.Sample.Clear();
                for (var i = 0; i < bufferSize; ++i)
                {
                    pcmBuffer.Sample.Add(reader.ReadInt16());
                    pcmBuffer.Sample.Add(reader.ReadInt16());
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    {
                        break;
                    }
                }
                
                pcmBuffer.SampleCount = pcmBuffer.Sample.Count / 2;
                
                for (var i = 0; i < 2; ++i)
                {
                    pcmBuffer.Position = 0;
                    pcmBuffer.Channel = i;
                    AdpcmEncode(setups[i], chunkSize);
                }
            }
        }

        public void ToPCMMono(BinaryReader reader, BinaryWriter writer)
        {
            float s0 = 0.0f;
            float s1 = 0.0f;
            SampleLineFlags flag = 0;
            while ((flag & SampleLineFlags.LoopEnd) == 0)
            {
                flag = LineToPCM(reader, writer, ref s0, ref s1);
            }
        }
        
        private static short SampleToPCM2(int sample, int factor, int predict, ref double s0, ref double s1)
        {
            sample <<= 12;
            sample = (short)sample; //sign extend
            sample >>= factor;
            double value = sample;
            value += s0 * RevF[predict].Key;
            value += s1 * RevF[predict].Value;
            s1 = s0;
            s0 = value;
            return (short)Math.Round(value);
        }
        
        private static byte[] LineToPCM2(byte[] input, ref double s0, ref double s1)
        {
            if (input.Length != 16)
                throw new ArgumentException("input");
            byte[] o = new byte[28 * 2];
            int factor = input[0] & 0xF;
            int predict = (input[0] >> 4) & 0xF;
            for (int i = 0; i < 14; i++)
            {
                int adl = input[i+2] & 0xF;
                int adh = (input[i+2] & 0xF0) >> 4;
                short l = SampleToPCM2(adl, factor, predict, ref s0, ref s1);
                short h = SampleToPCM2(adh, factor, predict, ref s0, ref s1);
                BitConv.ToInt16(o, i * 4 + 0, l);
                BitConv.ToInt16(o, i * 4 + 2, h);
            }
            return o;
        }

        public void ToPCMStereo(BinaryReader reader, BinaryWriter writer, int interleave)
        {
            if ((reader.BaseStream.Length % 32) != 0)
                throw new ArgumentException("Stereo sample size is not a multiple of 32.");
            if ((interleave % 16) != 0)
                throw new ArgumentException("Stereo interleave is not a multiple of 16.");
            if (interleave <= 0)
                throw new ArgumentOutOfRangeException("interleave");
            var size = reader.BaseStream.Length / 32;
            var data = reader.ReadBytes((int)reader.BaseStream.Length);
            interleave /= 16;
            double s0_l = 0, s1_l = 0;
            double s0_r = 0, s1_r = 0;
            List<byte> pcm_data = new List<byte>();
            int interleave_adv = 0;
            for (int i = 0; i < size; ++i)
            {
                if ((i % interleave) == 0)
                    ++interleave_adv;
                byte[] line_l = new byte[16];
                byte[] line_r = new byte[16];
                Array.Copy(data, (i + interleave * (interleave_adv-1)) * 16, line_l, 0, 16);
                Array.Copy(data, (i + interleave * interleave_adv) * 16, line_r, 0, 16);
                if (line_l[1] == 7 || line_r[1] == 7)
                    break;
                var l = LineToPCM2(line_l, ref s0_l, ref s1_l);
                var r = LineToPCM2(line_r, ref s0_r, ref s1_r);
                for (int j = 0; j < 28; ++j)
                {
                    pcm_data.Add(l[0 + j * 2]);
                    pcm_data.Add(l[1 + j * 2]);
                    pcm_data.Add(r[0 + j * 2]);
                    pcm_data.Add(r[1 + j * 2]);
                }
                if (line_l[1] == 1 || line_r[1] == 1)
                    break;
            }
            
            writer.Write(pcm_data.ToArray().AsSpan());
        }
    }
}

