using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using SkiaSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.Tests.Assets;

public class TextureDataTests
{
    private static SKImageInfo Info(int width, int height) => new(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);

    private static byte[] EncodePng(UInt32[] argb, int width)
    {
        var handle = GCHandle.Alloc(argb, GCHandleType.Pinned);
        try
        {
            using var bitmap = new SKBitmap();
            bitmap.InstallPixels(Info(width, argb.Length / width), handle.AddrOfPinnedObject());
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    private static UInt32[] DecodePng(byte[] png, int width, int height)
    {
        using var bitmap = SKBitmap.Decode(png, Info(width, height));
        var pixels = new int[width * height];
        Marshal.Copy(bitmap.GetPixels(), pixels, 0, pixels.Length);
        return pixels.Select(pixel => unchecked((UInt32)pixel)).ToArray();
    }

    // Icons' soft edges came back darkened after a project saved them, and transparent pixels lost the color filtering blends into edges
    [AvaloniaFact]
    public void TexturesKeepTransparentPixelsAsTheyAre()
    {
        UInt32[] argb = [0x08FEFEFE, 0x00123456, 0x80FF8000, 0xFF0080FF, 0x01010203, 0x7F7F7F7F, 0xFE000000, 0x40FFFFFF];
        var owner = new Texture { PixelFormat = ITwinTexture.TexturePixelFormat.PSMCT32, TextureFunction = ITwinTexture.TextureFunction.MODULATE };

        var data = TextureData.FromPng(owner, new MemoryStream(EncodePng(argb, 4)));
        var copy = TextureData.Copy(owner, data);

        Assert.Equal(argb, DecodePng(data.GetPngBytes(), 4, 2));
        Assert.Equal(argb, DecodePng(copy.GetPngBytes(), 4, 2));
    }

    // Crash's textures every chunk has leave the header's second copy of their size at 0
    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TexturesKeepWhetherTheyReserveMemory(bool reservesMemory)
    {
        var owner = new Texture
        {
            PixelFormat = ITwinTexture.TexturePixelFormat.PSMT8,
            TextureFunction = ITwinTexture.TextureFunction.MODULATE,
            GenerateMipmaps = true,
            ReservesMemory = reservesMemory
        };
        var data = TextureData.CreateSolidColor(owner, 64, 0xFF808080);

        var texture = (ITwinTexture)data.Export(new PS2ItemFactory());

        Assert.Equal(new byte[] { 0xE0, 0x20, 0, 0 }, texture.SizeWords);
        Assert.Equal(reservesMemory ? new byte[] { 0x20, 0 } : new byte[] { 0, 0 }, texture.ReservedBlocks);
    }

    [AvaloniaFact]
    public void TexturesKeepTheLeftoversOfTheirHeader()
    {
        var leftovers = new TwinTextureLeftovers { SignatureLeftover = 0xF600, ToolSlot = 0x979080 };
        var owner = new Texture { PixelFormat = ITwinTexture.TexturePixelFormat.PSMT8, TextureFunction = ITwinTexture.TextureFunction.MODULATE, Leftovers = leftovers };
        var data = TextureData.CreateSolidColor(owner, 32, 0xFF808080);

        var texture = (ITwinTexture)data.Export(new PS2ItemFactory());

        Assert.Equivalent(leftovers, texture.Leftovers);
    }

    // Powers of two of 16 to 256, the closest to the image's size, so Blender's images fit the game
    [AvaloniaFact]
    public void ResizingForTheGameGivesPowersOfTwo()
    {
        var owner = new Texture { Package = new TT_Lab.Assets.LabURI("res://Test"), InvariantName = "Big", Alias = "Big" };
        var pixels = Enumerable.Range(0, 300 * 100).Select(i => i % 300 < 150 ? 0xFFFF0000 : 0x800000FFu).ToArray();
        using var stream = new MemoryStream(EncodePng(pixels, 300));
        var big = TextureData.FromPng(owner, stream);

        var resized = big.ResizedForTheGame();

        Assert.Equal((256, 128), (resized.Bitmap!.PixelSize.Width, resized.Bitmap.PixelSize.Height));
        var resizedPixels = resized.GetPixels();
        Assert.Equal(0xFFFF0000, resizedPixels[0]);
        Assert.Equal(0x800000FFu, resizedPixels[255]);
        var small = TextureData.CreateSolidColor(owner, 64, 0xFF000000);
        Assert.Same(small, small.ResizedForTheGame());
    }
}
