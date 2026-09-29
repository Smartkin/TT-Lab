using System;

namespace Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code
{
    public interface ITwinSound : ITwinItem
    {
        /// <summary>
        /// Bit 0 clear makes the sound stereo (its size counts sample pairs), 3 on nearly every retail sound. The PAL executable
        /// reads it and drops it (FUN_001e5580)
        /// </summary>
        UInt32 Header { get; set; }
        /// <summary>
        /// The SPU2 pitch the PS2 version plays the sound at: the sample rate times 4096 over 48000, truncated (8000 Hz is 0x2AA, 22050 Hz
        /// 0x759, 32000 Hz 0xAAA). FUN_001dfa38 hands it to the sound driver, scaled by the play command's pitch factor. The Xbox
        /// version keeps the sample rate itself, its pitch is worked out from it
        /// </summary>
        UInt16 Pitch { get; set; }
        /// <summary>
        /// Handed to the sound driver with every play (FUN_001e2078), 32 on every retail sound
        /// </summary>
        UInt16 Param1 { get; set; }
        /// <summary>
        /// Read with the sound and never used by the PAL executable, 16 on every retail sound
        /// </summary>
        UInt16 Param2 { get; set; }
        /// <summary>
        /// Read with the sound and never used by the PAL executable, 8192 on every retail sound
        /// </summary>
        UInt16 Param3 { get; set; }
        /// <summary>
        /// Read with the sound and never used by the PAL executable, 8192 on every retail sound
        /// </summary>
        UInt16 Param4 { get; set; }
        Byte[] Sound { get; set; }
        /// <summary>
        /// Sets the sample rate the sound plays at
        /// </summary>
        void SetFreq(UInt16 freq);
        /// <summary>
        /// The sample rate the sound plays at
        /// </summary>
        UInt16 GetFreq();
        /// <summary>
        /// The sample the sound plays again from once it played the loop's last one, -1 when it plays once. The PS2 version keeps the
        /// loop in its ADPCM blocks' flags, so in whole blocks of 28 samples (the retail loops start on the second block); the Xbox
        /// version's sounds keep no loop
        /// </summary>
        Int32 LoopStart { get; }
        /// <summary>
        /// The sample after the loop's last one, -1 when the sound plays once. The PS2 stops a looping sound's data there, the retail
        /// loops end where the sound does
        /// </summary>
        Int32 LoopEnd { get; }
        /// <summary>
        /// The 16 bit samples, the channels interleaved
        /// </summary>
        Byte[] ToPCM();
        /// <summary>
        /// Sets the 16 bit samples (the channels interleaved) and the loop, in samples (-1 for none): the PS2 version rounds the loop
        /// down to whole blocks of 28 samples and drops the samples after it, which it never plays
        /// </summary>
        void SetDataFromPCM(Byte[] data, Int32 loopStart = -1, Int32 loopEnd = -1);

        /// <summary>
        /// The SPU2 pitch of a sample rate, the way the game's tools worked it out
        /// </summary>
        public static UInt16 PitchOf(UInt32 sampleRate)
        {
            return (UInt16)(sampleRate * 4096 / 48000);
        }

        /// <summary>
        /// The sample rate a pitch plays at: the rate the tools converted from when one gives the pitch, so retail sounds read as
        /// 8000 Hz rather than 7992, otherwise the lowest rate whose pitch it is
        /// </summary>
        public static UInt16 SampleRateOf(UInt16 pitch)
        {
            foreach (var rate in KnownSampleRates)
            {
                if (PitchOf(rate) == pitch)
                {
                    return rate;
                }
            }

            return (UInt16)Math.Ceiling(pitch * 48000.0 / 4096.0);
        }

        private static readonly UInt16[] KnownSampleRates = { 8000, 10000, 11025, 12000, 16000, 18000, 22050, 24000, 32000, 44100, 48000 };
    }
}
