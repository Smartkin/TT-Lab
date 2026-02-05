using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics;

public class TextureData : AbstractAssetData
{
    public TextureData(IAsset asset) : base(asset)
    {
    }

    public TextureData(IAsset asset, ITwinTexture texture) : this(asset)
    {
        SetTwinItem(texture);
    }

    public Bitmap? Bitmap;

    public ITwinTexture.TexturePixelFormat TexturePixelFormat { get; set; }
    public ITwinTexture.TextureFunction TextureFunction { get; set; }
    public Boolean GenerateMipmaps { get; set; }

    public static TextureData LoadFromGltf(IAsset owner, SharpGLTF.Schema2.Texture gltfTexture)
    {
        var textureData = new TextureData(owner);

        var gltfImage = gltfTexture.PrimaryImage;
        using var imageDataStream = new MemoryStream(gltfImage.Content.Content.ToArray());
        textureData.Bitmap = new Bitmap(imageDataStream);
        var isHd = textureData.Bitmap.Size.Width >= 256 || textureData.Bitmap.Size.Height >= 256;
        textureData.TexturePixelFormat = isHd ? ITwinTexture.TexturePixelFormat.PSMCT32 : ITwinTexture.TexturePixelFormat.PSMT8;
        textureData.TextureFunction = ITwinTexture.TextureFunction.MODULATE;
        textureData.GenerateMipmaps = !isHd;
        
        return textureData;
    }

    public override String GetStringified()
    {
        using var ms = new MemoryStream();
        if (Bitmap == null && IsTwinItemValid())
        {
            Import(LabURI.Empty, null, null);
        }
        Bitmap!.Save(ms, 100);
        ms.Position = 0;
        using var br = new BinaryReader(ms);
        return new String(br.ReadChars((int)ms.Length));
    }

    protected override void Dispose(Boolean disposing)
    {
        if (Bitmap != null && !Disposed)
        {
            Bitmap.Dispose();
        }
    }
        
    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        if (Bitmap != null && !Disposed)
        {
            Bitmap.Save(dataPath, 100);
        }
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        Bitmap = new Bitmap(dataPath);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var texture = GetTwinItem<ITwinTexture>();
        if (texture.TextureFormat != ITwinTexture.TexturePixelFormat.PSMCT32 &&
            texture.TextureFormat != ITwinTexture.TexturePixelFormat.PSMT8)
        {
            return;
        }
            
        var width = (Int32)Math.Pow(2, texture.ImageWidthPower);
        var height = (Int32)Math.Pow(2, texture.ImageHeightPower);
        texture.CalculateData();
        
        var bits = new UInt32[width * height];
        var bitsHandle = GCHandle.Alloc(bits, GCHandleType.Pinned);
        
        for (var x = 0; x < width; ++x)
        {
            for (var y = 0; y < height; ++y)
            {
                var dstx = x;
                var dsty = y;
                bits[dstx + dsty * width] = texture.Colors[x + y * width].ToARGB();
            }
        }

        Bitmap = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, bitsHandle.AddrOfPinnedObject(),
            new PixelSize(width, height), new Vector(96, 96), width * 4);
        bitsHandle.Free();
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var texture = factory.GenerateTexture();
        if (Bitmap == null)
        {
            return texture;
        }
        
        var fun = TextureFunction;
        var format = TexturePixelFormat;
        var tex = new List<Twinsanity.TwinsanityInterchange.Common.Color>();
        var bits = new byte[Bitmap.PixelSize.Width * Bitmap.PixelSize.Height * 4];
        var bitsHandle = GCHandle.Alloc(bits, GCHandleType.Pinned);
        Bitmap.CopyPixels(new PixelRect(0, 0, Bitmap.PixelSize.Width, Bitmap.PixelSize.Height), bitsHandle.AddrOfPinnedObject(), bits.Length, Bitmap.PixelSize.Width * 4);
        unsafe
        {
            fixed(byte* source = &bits[0])
            {
                var sourceAddr = source;
                for (var i = 0; i < Bitmap.PixelSize.Height; i++)
                {
                    for (var j = 0; j < Bitmap.PixelSize.Width; j++)
                    {
                        var b = sourceAddr[0];
                        var g = sourceAddr[1];
                        var r = sourceAddr[2];
                        var a = sourceAddr[3];
                        tex.Add(new Twinsanity.TwinsanityInterchange.Common.Color(r, g, b, a));
                        sourceAddr += 4;
                    }
                }
            }
        }
        texture.FromBitmap(tex, Bitmap.PixelSize.Width, fun, format, GenerateMipmaps);
        bitsHandle.Free();

        return texture;
    }
}