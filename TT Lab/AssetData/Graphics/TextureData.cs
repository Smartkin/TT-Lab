using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Graphics;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics;

[Editable(Caption = "Texture Editor", EditorDescType = typeof(TextureEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
public class TextureData : AbstractAssetData
{
    public TextureData(IAsset asset) : base(asset)
    {
    }

    public TextureData(IAsset asset, ITwinTexture texture) : this(asset)
    {
        SetTwinItem(texture);
    }

    private Bitmap? _bitmap;
    // The exact pixels (ARGB) the bitmap shows. Avalonia's bitmaps lose the color of fully transparent pixels, which filtering blends into
    // the edges of what the texture draws
    private UInt32[]? _pixels;

    public Bitmap? Bitmap
    {
        get => _bitmap;
        set
        {
            _bitmap = value;
            _pixels = null;
        }
    }

    public Byte[] GetPngBytes()
    {
        return EncodePng(GetPixels(), Bitmap!.PixelSize.Width, Bitmap.PixelSize.Height);
    }

    public static TextureData FromPng(IAsset owner, Stream png)
    {
        var textureData = new TextureData(owner);
        var (pixels, width, height) = DecodePng(png);
        textureData.SetPixels(pixels, width, height);
        return textureData;
    }

    /// <summary>
    /// A texture of the pixels (ARGB, top row first), which it keeps
    /// </summary>
    public static TextureData FromPixels(IAsset owner, UInt32[] pixels, Int32 width, Int32 height)
    {
        var textureData = new TextureData(owner);
        textureData.SetPixels(pixels, width, height);
        return textureData;
    }

    /// <summary>
    /// The biggest size the game's textures come in
    /// </summary>
    public const Int32 MaxGameSize = 256;

    /// <summary>
    /// The texture at a size the game takes: a power of two of 16 to <see cref="MaxGameSize"/> on each side, the closest to the size it
    /// has. The texture itself when it already is one
    /// </summary>
    public TextureData ResizedForTheGame()
    {
        var size = Bitmap!.PixelSize;
        var width = GameSize(size.Width);
        var height = GameSize(size.Height);
        if (width == size.Width && height == size.Height)
        {
            return this;
        }

        var resized = new TextureData(Owner);
        resized.SetPixels(Resample(GetPixels(), size.Width, size.Height, width, height), width, height);
        return resized;
    }

    private static Int32 GameSize(Int32 size)
    {
        return Math.Clamp(1 << (Int32)Math.Round(Math.Log2(Math.Max(size, 1))), 16, MaxGameSize);
    }

    internal static UInt32[] Resample(UInt32[] pixels, Int32 width, Int32 height, Int32 newWidth, Int32 newHeight)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var result = new UInt32[newWidth * newHeight];
        var sourceHandle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        var resultHandle = GCHandle.Alloc(result, GCHandleType.Pinned);
        try
        {
            using var source = new SKBitmap();
            source.InstallPixels(info, sourceHandle.AddrOfPinnedObject(), info.RowBytes);
            var targetInfo = new SKImageInfo(newWidth, newHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var target = new SKBitmap();
            target.InstallPixels(targetInfo, resultHandle.AddrOfPinnedObject(), targetInfo.RowBytes);
            if (!source.ScalePixels(target, SKFilterQuality.High))
            {
                throw new InvalidOperationException($"Couldn't resize a {width}x{height} texture to {newWidth}x{newHeight}");
            }
        }
        finally
        {
            sourceHandle.Free();
            resultHandle.Free();
        }

        return result;
    }

    public static TextureData Copy(IAsset owner, TextureData source)
    {
        var textureData = new TextureData(owner);
        textureData.SetPixels((UInt32[])source.GetPixels().Clone(), source.Bitmap!.PixelSize.Width, source.Bitmap.PixelSize.Height);
        return textureData;
    }

    private void SetPixels(UInt32[] pixels, Int32 width, Int32 height)
    {
        Bitmap = BitmapOf(pixels, width, height);
        _pixels = pixels;
    }

    // ARGB, top row first
    internal UInt32[] GetPixels()
    {
        if (_pixels != null)
        {
            return _pixels;
        }

        var bitmap = Bitmap!;
        var pixels = new UInt32[bitmap.PixelSize.Width * bitmap.PixelSize.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), handle.AddrOfPinnedObject(), pixels.Length * 4, bitmap.PixelSize.Width * 4);
        }
        finally
        {
            handle.Free();
        }

        return pixels;
    }

    // PNGs go through Skia as they are, Avalonia premultiplies the alpha of the ones it loads and saves which darkens every partly transparent pixel
    internal static (UInt32[] Pixels, Int32 Width, Int32 Height) DecodePng(Stream stream)
    {
        using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException("Not an image");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pixels = new UInt32[info.Width * info.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var result = codec.GetPixels(info, handle.AddrOfPinnedObject());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                throw new InvalidDataException($"Couldn't decode the image: {result}");
            }
        }
        finally
        {
            handle.Free();
        }

        return (pixels, info.Width, info.Height);
    }

    internal static Byte[] EncodePng(UInt32[] pixels, Int32 width, Int32 height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var skBitmap = new SKBitmap();
            skBitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes);
            using var data = skBitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    public static TextureData CreateSolidColor(IAsset owner, Int32 size, UInt32 argb)
    {
        var textureData = new TextureData(owner);
        var bits = new UInt32[size * size];
        Array.Fill(bits, argb);
        textureData.SetPixels(bits, size, size);
        return textureData;
    }

    public override String GetStringified()
    {
        if (Bitmap == null && IsTwinItemValid())
        {
            Import(LabURI.Empty, null, null);
        }
        using var ms = new MemoryStream(EncodePng(GetPixels(), Bitmap!.PixelSize.Width, Bitmap.PixelSize.Height));
        using var br = new BinaryReader(ms);
        return new String(br.ReadChars((int)ms.Length));
    }

    protected override void Dispose(Boolean disposing)
    {
        // Disposed is already set by the time this runs, the bitmap's pixels live in native memory so they have to be freed explicitly
        Bitmap?.Dispose();
        Bitmap = null;
    }
        
    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        if (Bitmap != null && !Disposed)
        {
            File.WriteAllBytes(dataPath, EncodePng(GetPixels(), Bitmap.PixelSize.Width, Bitmap.PixelSize.Height));
        }
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        Bitmap?.Dispose();
        using var stream = new FileStream(dataPath, FileMode.Open, FileAccess.Read);
        var (pixels, width, height) = DecodePng(stream);
        SetPixels(pixels, width, height);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var texture = GetTwinItem<ITwinTexture>();
        // The Xbox version's textures are compressed or kept as they are
        if (texture.TextureFormat is not (ITwinTexture.TexturePixelFormat.PSMCT32 or ITwinTexture.TexturePixelFormat.PSMT8
            or ITwinTexture.TexturePixelFormat.DXT5 or ITwinTexture.TexturePixelFormat.Raw))
        {
            return;
        }
            
        var width = (Int32)Math.Pow(2, texture.ImageWidthPower);
        var height = (Int32)Math.Pow(2, texture.ImageHeightPower);
        texture.CalculateData();
        
        var bits = new UInt32[width * height];
        for (var i = 0; i < bits.Length; ++i)
        {
            bits[i] = texture.Colors[i].ToARGB();
        }

        Bitmap?.Dispose();
        SetPixels(bits, width, height);
    }

    // A bitmap of the pixels (ARGB, top row first) as they are, not premultiplied
    internal static Bitmap BitmapOf(UInt32[] pixels, Int32 width, Int32 height)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, handle.AddrOfPinnedObject(), new PixelSize(width, height), new Vector(96, 96), width * 4);
        }
        finally
        {
            handle.Free();
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        if (Bitmap == null)
        {
            return factory.GenerateTexture();
        }

        return Build(factory, GetPixels(), Bitmap.PixelSize.Width, (Texture)Owner);
    }

    // What building makes of the pixels with the texture's settings, which the texture's viewer shows level by level
    internal static ITwinTexture Build(ITwinItemFactory factory, UInt32[] pixels, Int32 width, Texture textureOwner)
    {
        var texture = factory.GenerateTexture();
        var fun = textureOwner.TextureFunction;
        var format = textureOwner.PixelFormat;
        var tex = pixels.Select(argb => new Twinsanity.TwinsanityInterchange.Common.Color((Byte)(argb >> 16), (Byte)(argb >> 8), (Byte)argb, (Byte)(argb >> 24))).ToList();
        texture.FromBitmap(tex, width, fun, format, textureOwner.GenerateMipmaps);
        if (!textureOwner.ReservesMemory && format is ITwinTexture.TexturePixelFormat.PSMT8 or ITwinTexture.TexturePixelFormat.PSMCT32)
        {
            texture.ReservedBlocks = new Byte[2];
        }

        if (textureOwner.Leftovers != null)
        {
            texture.Leftovers = textureOwner.Leftovers;
        }

        return texture;
    }
}