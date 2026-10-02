using TT_Lab.AssetData.Global;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;

namespace TT_Lab.Tests.TwinTech;

// The PS2 memory card icon the game's saves get (ps2iconsys' layout, which accounts for every byte of the game's Crash.ico): its corners
// with a position for every shape, the animation and the 128x128 texture, run length encoded or not
public class SaveIconFormatTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IconsComeBackByteForByte(bool compressed)
    {
        var icon = TestAssets.MakeSaveIcon(compressed);

        var bytes = SaveIconTlm.ToBytes(icon);
        var read = SaveIconTlm.FromBytes(bytes);

        Assert.Equal(icon.GetLength(), bytes.Length);
        Assert.Equal(bytes, SaveIconTlm.ToBytes(read));
        Assert.Equal((2, 12, 2), (read.ShapeCount, read.Vertexes.Count, read.Frames.Count));
        Assert.Equal(icon.Texture, read.Texture);
        Assert.Equal(compressed, read.CompressedTexture != null);
        // 20 bytes of header, 32 a corner of two shapes, 20 of animation header, the frames and the texture
        Assert.Equal(20 + 12 * 32 + 20 + 2 * (8 + 3 * 8), bytes.Length - (compressed ? 4 + read.CompressedTexture!.Length : 0x8000));
    }

    [Fact]
    public void TexturesRunLengthEncodeTheWayTheConsoleReadsThem()
    {
        var texels = new ushort[PS2SaveIcon.TexelCount];
        for (var i = 0; i < texels.Length; i++)
        {
            // A long run, then a thousand texels that all differ, then runs of two
            texels[i] = i < 5000 ? (ushort)0x8001 : i < 6000 ? (ushort)(0x8000 | i) : (ushort)(0x8000 | i / 2);
        }

        var encoded = PS2SaveIcon.CompressTexture(texels);

        Assert.Equal(texels, PS2SaveIcon.DecompressTexture(encoded));
        Assert.Equal(5000, BitConverter.ToUInt16(encoded, 0));
        // Texels as they are go 256 at most to a count
        Assert.Equal(0x10000 - 256, BitConverter.ToUInt16(encoded, 4));
        Assert.Equal(4 + 4 * 2 + 1000 * 2 + (PS2SaveIcon.TexelCount - 6000) / 2 * 4, encoded.Length);
    }

    [Fact]
    public void AnotherEncodingIsKeptWhileItDecodesToTheTexels()
    {
        var icon = TestAssets.MakeSaveIcon(true);
        // Every texel as it is, the way another tool might have written it
        var other = new MemoryStream();
        var writer = new BinaryWriter(other);
        for (var i = 0; i < PS2SaveIcon.TexelCount; i += 256)
        {
            writer.Write((ushort)(0x10000 - 256));
            for (var j = 0; j < 256; j++)
            {
                writer.Write(icon.Texture[i + j]);
            }
        }

        icon.CompressedTexture = other.ToArray();

        var kept = SaveIconTlm.FromBytes(SaveIconTlm.ToBytes(icon));
        Assert.Equal(icon.CompressedTexture, kept.CompressedTexture);

        icon.Texture[100] ^= 0x1F;
        var encodedAgain = SaveIconTlm.FromBytes(SaveIconTlm.ToBytes(icon));
        Assert.NotEqual(icon.CompressedTexture, encodedAgain.CompressedTexture);
        Assert.Equal(icon.Texture, encodedAgain.Texture);
    }

    [Fact]
    public void TexelsKeepTheirBitsThroughRgba()
    {
        for (var texel = 0; texel <= ushort.MaxValue; texel++)
        {
            var (r, g, b, a) = PS2SaveIcon.ToRgba((ushort)texel);
            Assert.Equal(texel, PS2SaveIcon.FromRgba(r, g, b, a));
        }

        Assert.Equal(((byte)255, (byte)255, (byte)255, (byte)255), PS2SaveIcon.ToRgba(0xFFFF));
        Assert.Equal(((byte)0, (byte)0, (byte)0, (byte)0), PS2SaveIcon.ToRgba(0));
    }

    [Fact]
    public void BrokenIconsAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => PS2SaveIcon.DecompressTexture([0x05, 0x00]));
        Assert.Throws<InvalidDataException>(() => PS2SaveIcon.DecompressTexture([0x01, 0x40, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00]));
        var bytes = SaveIconTlm.ToBytes(TestAssets.MakeSaveIcon());
        BitConverter.GetBytes(int.MaxValue).CopyTo(bytes, 16);
        Assert.Throws<InvalidDataException>(() => SaveIconTlm.FromBytes(bytes));
    }
}
