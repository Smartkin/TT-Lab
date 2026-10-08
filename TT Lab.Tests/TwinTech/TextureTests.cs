using Twinsanity.Libraries;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces.Items
;

namespace TT_Lab.Tests.TwinTech;

public class TextureTests
{
    // The PS2 keeps every channel at half precision so their lowest bit is lost
    [Theory]
    [InlineData(16, 3)]
    [InlineData(32, 200)]
    [InlineData(64, 256)]
    public void PalettedTextureRoundTrips(int size, int colorCount)
    {
        var colors = Enumerable.Range(0, colorCount).Select(i => new Color((byte)(i * 37), (byte)(i * 11), (byte)(255 - i), 255)).ToList();
        var image = Enumerable.Range(0, size * size).Select(i => colors[(i * 7 + i / size) % colorCount]).ToList();
        var expected = image.Select(color => color.ToARGB() & 0xFEFEFEFE).ToList();
        var texture = new PS2AnyTexture();

        texture.FromBitmap(image, size, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8);
        texture.CalculateData();

        Assert.Equal(expected, texture.Colors.Select(color => color.ToARGB()));
    }

    // The layouts the game's own textures of each size have. The header's size is what the game reserves in the GS memory for the upload,
    // the palette of textures without mips goes right after their pixels
    [Theory]
    [InlineData(64, 64, true, 4, 20, 1, 64, 32, 0x20)]
    [InlineData(64, 64, false, 1, 16, 1, 64, 32, 0x20)]
    [InlineData(16, 16, true, 2, 8, 1, 64, 32, 0x20)]
    [InlineData(64, 16, true, 2, 8, 1, 64, 32, 0x20)]
    [InlineData(32, 32, false, 1, 8, 1, 64, 32, 0x20)]
    [InlineData(128, 64, true, 4, 44, 2, 64, 64, 0x40)]
    [InlineData(128, 128, true, 5, 84, 2, 64, 96, 0x60)]
    [InlineData(128, 128, false, 1, 64, 2, 64, 96, 0x60)]
    [InlineData(128, 256, false, 1, 128, 2, 64, 160, 0xA0)]
    public void PalettedTexturesGetTheGamesLayout(int width, int height, bool mipmaps, int mips, int clutPointer, int bufferWidth, int uploadWidth, int uploadHeight, int size)
    {
        var texture = new PS2AnyTexture();

        texture.FromBitmap(Enumerable.Range(0, width * height).Select(i => new Color((byte)i, 0, 0, 255)).ToList(), width, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8, mipmaps);

        Assert.Equal(mips, texture.MipLevels);
        Assert.Equal(clutPointer, texture.ClutBufferBasePointer);
        Assert.Equal(bufferWidth, texture.TextureBufferWidth);
        Assert.Equal((uploadWidth, uploadHeight), UploadRectangle(texture));
        Assert.Equal(new byte[] { 0xE0, (byte)size, (byte)(size >> 8), 0 }, texture.SizeWords);
        Assert.Equal(new byte[] { (byte)size, (byte)(size >> 8) }, texture.ReservedBlocks);
    }

    [Theory]
    [InlineData(256, 64, 0x100)]
    [InlineData(256, 128, 0x200)]
    [InlineData(256, 256, 0x400)]
    public void TrueColorTexturesUploadTheWholeImage(int width, int height, int size)
    {
        var image = Enumerable.Range(0, width * height).Select(i => new Color((byte)(i * 3), (byte)(i >> 8), (byte)i, 255)).ToList();
        var expected = image.Select(color => color.ToARGB() & 0xFEFEFEFE).ToList();
        var texture = new PS2AnyTexture();

        texture.FromBitmap(image, width, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMCT32);
        texture.CalculateData();

        Assert.Equal(4, texture.TextureBufferWidth);
        Assert.Equal((width, height), UploadRectangle(texture));
        Assert.Equal(new byte[] { 0xE0, (byte)size, (byte)(size >> 8), 0 }, texture.SizeWords);
        Assert.Equal(new byte[] { (byte)size, (byte)(size >> 8) }, texture.ReservedBlocks);
        Assert.Equal(expected, texture.Colors.Select(color => color.ToARGB()));
    }

    // A picture of more colors than a palette holds gets a palette made of its colors and is diffused into it: a smooth area next to a
    // few rows of every color stays close to itself, where the old median cut spent the palette on the few (a blurred error of 2.2)
    [Fact]
    public void PicturesOfMoreColorsThanAPaletteHoldStayClose()
    {
        const int size = 64;
        var random = new Random(7);
        var image = Enumerable.Range(0, size * size).Select(i => i / size < 58
            ? new Color((byte)(150 + i % size), (byte)(100 + i / size), (byte)(80 + (i % size + i / size) / 4), 255)
            : new Color((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255)).ToList();
        var texture = new PS2AnyTexture();

        texture.FromBitmap(image, size, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8);
        texture.CalculateData();

        Assert.Equal(4096, image.Select(color => color.ToARGB()).Distinct().Count());
        Assert.InRange(BlurredError(image, texture.Colors, size, size), 0.0, 1.6);
    }

    // Their mips are their pixels averaged and their palette has the averages too, the GS draws every level with it: dark and bright
    // columns are in between in the distance, every other index made them all dark
    [Fact]
    public void MipsOfQuantizedPicturesAverageTheirPixels()
    {
        const int size = 64;
        var image = Enumerable.Range(0, size * size).Select(i =>
        {
            var (x, y) = (i % size, i / size);
            var value = x % 2 == 0 ? 40 : 200;
            return new Color((byte)(value + y % 16), (byte)(value + x / 4), (byte)value, 255);
        }).ToList();
        var texture = new PS2AnyTexture();

        texture.FromBitmap(image, size, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8, true);

        var mip = DecodeMip(texture, 1, size / 2, size / 2);
        Assert.InRange(mip.Average(color => (double)color.B), 110, 130);
        Assert.True(mip.Count(color => Math.Abs(color.B - 120) <= 20) > mip.Count * 0.9);
    }

    // Every level the way the game draws it, the mips where the descriptor put them: what the texture viewer shows
    [Fact]
    public void TheLevelsAreThePictureAndItsMips()
    {
        const int size = 64;
        var image = Enumerable.Range(0, size * size).Select(i => new Color((byte)(i % 200), (byte)(i / 64 * 4), 7, 255)).ToList();
        var texture = new PS2AnyTexture();
        texture.FromBitmap(image, size, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8, true);

        var levels = texture.DecodeLevels();

        Assert.Equal(texture.MipLevels, levels.Count);
        texture.CalculateData();
        Assert.Equal(texture.Colors.Select(color => color.ToARGB()), levels[0].Select(color => color.ToARGB()));
        for (var level = 1; level < levels.Count; level++)
        {
            Assert.Equal(DecodeMip(texture, level, size >> level, size >> level).Select(color => color.ToARGB()), levels[level].Select(color => color.ToARGB()));
        }

        // More colors than a palette holds, quantized
        Assert.InRange(levels[0].Select(color => color.ToARGB()).Distinct().Count(), 2, 256);
    }

    // The game's tools only made palette textures of the descriptors' sizes: a picture of another size goes in with every color and no
    // mips, it failed to build as a palette texture
    [Theory]
    [InlineData(256, 256)]
    [InlineData(8, 8)]
    [InlineData(16, 8)]
    public void PicturesOfSizesWithoutAPaletteLayoutKeepEveryColor(int width, int height)
    {
        Color At(int i) => new((byte)(i * 7), (byte)(i / 3), (byte)i, 255);
        var expected = Enumerable.Range(0, width * height).Select(At).Select(color => color.ToARGB()).ToList();
        var texture = new PS2AnyTexture();

        texture.FromBitmap(Enumerable.Range(0, width * height).Select(At).ToList(), width, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8, true);

        Assert.Equal(ITwinTexture.TexturePixelFormat.PSMCT32, texture.TextureFormat);
        Assert.Equal(1, texture.MipLevels);
        // The PS2's colors go to 128, every channel loses its lowest bit like the game's 32 bit textures (TrueColorTexturesUploadTheWholeImage)
        Assert.Equal(expected.Select(argb => argb & 0xFEFEFEFE), Assert.Single(texture.DecodeLevels()).Select(color => color.ToARGB()));
    }

    // The Xbox version keeps only the largest level
    [Fact]
    public void XboxTexturesHaveOneLevel()
    {
        var texture = new XboxAnyTexture();
        texture.FromBitmap(Enumerable.Range(0, 64 * 64).Select(i => new Color((byte)i, 0, 0, 255)).ToList(), 64, ITwinTexture.TextureFunction.MODULATE,
            ITwinTexture.TexturePixelFormat.PSMT8, true);

        Assert.Equal(64 * 64, Assert.Single(texture.DecodeLevels()).Count);
    }

    // How far the picture is from what it was made of up close, where the eye takes dithering in as its average: a 3x3 blur of both
    private static double BlurredError(List<Color> expected, List<Color> actual, int width, int height)
    {
        double error = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var difference = new double[4];
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var at = Math.Clamp(y + dy, 0, height - 1) * width + Math.Clamp(x + dx, 0, width - 1);
                        difference[0] += expected[at].R - actual[at].R;
                        difference[1] += expected[at].G - actual[at].G;
                        difference[2] += expected[at].B - actual[at].B;
                        difference[3] += expected[at].A - actual[at].A;
                    }
                }

                error += difference.Sum(channel => channel / 9 * (channel / 9));
            }
        }

        return Math.Sqrt(error / (width * height * 4));
    }

    // A mip level's pixels, read the way CalculateData reads the first
    private static List<Color> DecodeMip(PS2AnyTexture texture, int level, int width, int height)
    {
        var gif = VIFInterpreter.InterpretCode(texture.TextureData).GetGifMem();
        var transfer = gif[0].Data[1].Output;
        var raw = EzSwizzle.writeTexPSMCT32(0, 1, 0, 0, (int)(transfer & 0xFFFFFFFF), (int)(transfer >> 32), EzSwizzle.TagToBytes(gif[1]));
        var indexes = EzSwizzle.readTexPSMT8(texture.MipLevelsTBP[level - 1], texture.MipLevelsTBW[level - 1], 0, 0, width, height, raw, false);
        var palette = EzSwizzle.BytesToColors(EzSwizzle.readTexPSMCT32(texture.ClutBufferBasePointer, 1, 0, 0, 16, 16, raw, false));
        // Entries 8-15 and 16-23 of every 32 are stored swapped
        for (var i = 0; i < 8; i++)
        {
            for (var j = 8; j < 16; j++)
            {
                (palette[j + i * 32], palette[j + i * 32 + 8]) = (palette[j + i * 32 + 8], palette[j + i * 32]);
            }
        }

        foreach (var color in palette)
        {
            color.ScaleAlphaUp();
        }

        return indexes.Select(index => palette[index]).ToList();
    }

    // The icons of PSM files have zeros where the textures of chunks have leftovers of the tools
    [Fact]
    public void TexturesKeepTheLeftoversOfTheirHeader()
    {
        var leftovers = new TwinTextureLeftovers { SignatureLeftover = 0x0178, ToolSlot = 0x461, Reserved1 = 0xF, Reserved2 = 2, ToolMemory = [0x1F, 1, 2, 3, 0x20571F40, 5, 6, 7] };
        var texture = new PS2AnyTexture();
        texture.FromBitmap(Enumerable.Repeat(new Color(10, 20, 30, 255), 16 * 16).ToList(), 16, ITwinTexture.TextureFunction.MODULATE, ITwinTexture.TexturePixelFormat.PSMT8);
        texture.Leftovers = leftovers;
        var bytes = Write(texture);

        var read = new PS2AnyTexture();
        using (var reader = new BinaryReader(new MemoryStream(bytes)))
        {
            read.Read(reader, bytes.Length);
        }

        Assert.Equivalent(leftovers, read.Leftovers);
        Assert.Equal(0xBBCC0178U, read.HeaderSignature);
        Assert.Equal(bytes, Write(read));
    }

    private static byte[] Write(PS2AnyTexture texture)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        texture.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }

    private static (int, int) UploadRectangle(PS2AnyTexture texture)
    {
        // The second register the header's GIF tag sets is TRXREG, the size of the transfer
        var transfer = VIFInterpreter.InterpretCode(texture.TextureData).GetGifMem()[0].Data[1].Output;
        return ((int)(transfer & 0xFFFFFFFF), (int)(transfer >> 32));
    }
}
