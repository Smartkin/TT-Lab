using System.Text;
using Twinsanity.Libraries;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;

namespace TT_Lab.Tests.TwinTech;

// PS2 sounds keep their loop in their ADPCM blocks' flags, laid out like the retail sounds: of the NTSC version's 812 sounds 717 play
// once, ending on a block flagged 1 followed by a silent one, and 95 loop from their second block (00 06 02.. 03) to their last
public class AdpcmTests
{
    private static short[] Sine(int count, double amplitude = 10000) =>
        Enumerable.Range(0, count).Select(i => (short)(Math.Sin(i * 2 * Math.PI * 440 / 22050) * amplitude)).ToArray();

    private static byte[] Flags(byte[] blocks) => Enumerable.Range(0, blocks.Length / 16).Select(i => blocks[i * 16 + 1]).ToArray();

    private static (int, int) Loop(byte[] blocks)
    {
        ADPCM.FindLoop(blocks, out var start, out var end);
        return (start, end);
    }

    [Fact]
    public void ASoundPlayedOnceEndsOnItsLastBlockAndASilentOne()
    {
        var blocks = ADPCM.Encode(Sine(100), -1, -1);

        Assert.Equal([0, 0, 0, 1, 7], Flags(blocks));
        Assert.All(blocks.Skip(4 * 16 + 2).Take(14), value => Assert.Equal(0, value));
        Assert.Equal((-1, -1), Loop(blocks));
        // The last block's samples play, the silent one doesn't
        Assert.Equal(112, ADPCM.Decode(blocks).Length);
    }

    [Fact]
    public void ALoopIsLaidOutLikeTheGamesOnes()
    {
        var blocks = ADPCM.Encode(Sine(280), 28, 280);

        Assert.Equal([0, 6, 2, 2, 2, 2, 2, 2, 2, 3], Flags(blocks));
        Assert.Equal((28, 280), Loop(blocks));
        Assert.Equal(280, ADPCM.Decode(blocks).Length);
    }

    [Fact]
    public void LoopPointsRoundDownToBlocksAndTheSamplesAfterTheLoopAreLeftOut()
    {
        var blocks = ADPCM.Encode(Sine(300), 30, 270);

        Assert.Equal(9, blocks.Length / 16);
        Assert.Equal((28, 252), Loop(blocks));
        // A loop is a block at least and ends within the sound, the last block when it isn't full
        Assert.Equal((56, 84), Loop(ADPCM.Encode(Sine(300), 60, 61)));
        Assert.Equal((0, 84), Loop(ADPCM.Encode(Sine(100), 0, 5000)));
        // A loop of one block is the silent block's flags
        Assert.Equal([0, 7], Flags(ADPCM.Encode(Sine(56), 28, 56)));
    }

    [Fact]
    public void TheBlockEndingTheSoundIsDecoded()
    {
        // The old decoder stopped before the block flagged 1 and lost the sound's last 28 samples, a loop's last block with them
        var blocks = ADPCM.Encode(Sine(84), -1, -1);
        Assert.Equal([0, 0, 1, 7], Flags(blocks));

        var samples = ADPCM.Decode(blocks);

        Assert.Equal(84, samples.Length);
        Assert.Contains(samples.Skip(56), sample => Math.Abs(sample) > 1000);
    }

    [Fact]
    public void TheCodecKeepsTheWaveform()
    {
        var samples = Sine(2800);

        var decoded = ADPCM.Decode(ADPCM.Encode(samples, 28, 2800));

        var error = Math.Sqrt(samples.Zip(decoded, (a, b) => (double)(a - b) * (a - b)).Average());
        Assert.True(error < 300, $"RMS error {error}");
    }

    [Fact]
    public void StereoSoundsLoopBothChannels()
    {
        var left = Sine(280);
        var right = Sine(280, 4000);
        var pcm = new byte[280 * 4];
        for (var i = 0; i < 280; i++)
        {
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 4), left[i]);
            BitConverter.TryWriteBytes(pcm.AsSpan(i * 4 + 2), right[i]);
        }

        var sound = new PS2AnySound { Header = 2 };
        sound.SetDataFromPCM(pcm, 28, 280);

        // The left channel's blocks, then the right one's
        Assert.Equal(2 * 10 * 16, sound.Sound.Length);
        Assert.Equal([0, 6, 2, 2, 2, 2, 2, 2, 2, 3], Flags(sound.Sound[160..]));
        Assert.Equal(28, sound.LoopStart);
        Assert.Equal(280, sound.LoopEnd);
        var back = sound.ToPCM();
        Assert.Equal(pcm.Length, back.Length);
        var peakLeft = Enumerable.Range(0, 280).Max(i => Math.Abs((int)BitConverter.ToInt16(back, i * 4)));
        var peakRight = Enumerable.Range(0, 280).Max(i => Math.Abs((int)BitConverter.ToInt16(back, i * 4 + 2)));
        Assert.InRange(peakLeft, 9000, 11000);
        Assert.InRange(peakRight, 3500, 4500);
    }

    [Fact]
    public void XboxSoundsKeepNoLoop()
    {
        var sound = new XboxAnySound();

        sound.SetDataFromPCM(new byte[56], 0, 28);

        Assert.Equal(-1, sound.LoopStart);
        Assert.Equal(-1, sound.LoopEnd);
        Assert.Equal(56, sound.Sound.Length);
    }

    private static void Chunk(BinaryWriter writer, string id, byte[] data)
    {
        writer.Write(Encoding.ASCII.GetBytes(id));
        writer.Write(data.Length);
        writer.Write(data);
        if ((data.Length & 1) != 0)
        {
            writer.Write((byte)0);
        }
    }

    [Fact]
    public void WavsAreReadByTheirChunksWithTheirSamplersLoop()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            // A format chunk with the extension size and an odd sized chunk before the data, like audio tools write
            var format = new byte[18];
            BitConverter.TryWriteBytes(format.AsSpan(0), (short)1);
            BitConverter.TryWriteBytes(format.AsSpan(2), (short)1);
            BitConverter.TryWriteBytes(format.AsSpan(4), 22050);
            BitConverter.TryWriteBytes(format.AsSpan(8), 44100);
            BitConverter.TryWriteBytes(format.AsSpan(12), (short)2);
            BitConverter.TryWriteBytes(format.AsSpan(14), (short)16);
            Chunk(writer, "fmt ", format);
            Chunk(writer, "LIST", Encoding.ASCII.GetBytes("INFOx"));
            Chunk(writer, "data", [1, 0, 2, 0, 3, 0, 4, 0]);
            // A sampler chunk with one loop, its end the loop's last sample
            var sampler = new byte[60];
            BitConverter.TryWriteBytes(sampler.AsSpan(28), 1);
            BitConverter.TryWriteBytes(sampler.AsSpan(44), 1);
            BitConverter.TryWriteBytes(sampler.AsSpan(48), 2);
            Chunk(writer, "smpl", sampler);
        }

        stream.Position = 0;
        using var reader = new BinaryReader(stream);
        var pcm = Array.Empty<byte>();
        short channels = 0;
        uint rate = 0;
        var file = Riff.LoadRiff(reader, ref pcm, ref channels, ref rate, out var loopStart, out var loopEnd);

        Assert.Equal(1, channels);
        Assert.Equal(22050u, rate);
        Assert.Equal([1, 0, 2, 0, 3, 0, 4, 0], pcm);
        Assert.Equal((1, 3), (loopStart, loopEnd));
        Assert.Equal(stream.Length, file.Length);

        // What TT Lab writes reads back, without a loop
        using var plain = new MemoryStream();
        using (var writer = new BinaryWriter(plain, Encoding.ASCII, true))
        {
            short mono = 1;
            uint hertz = 8000;
            Riff.SaveRiff(writer, [5, 0, 6, 0], ref mono, ref hertz);
        }

        plain.Position = 0;
        using var plainReader = new BinaryReader(plain);
        Riff.LoadRiff(plainReader, ref pcm, ref channels, ref rate, out loopStart, out loopEnd);
        Assert.Equal([5, 0, 6, 0], pcm);
        Assert.Equal(8000u, rate);
        Assert.Equal((-1, -1), (loopStart, loopEnd));
    }
}
