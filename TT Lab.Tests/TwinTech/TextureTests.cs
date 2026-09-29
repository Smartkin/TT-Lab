using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
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
