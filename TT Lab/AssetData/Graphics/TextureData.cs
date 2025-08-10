using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.AssetData.Graphics
{
    public class TextureData : AbstractAssetData
    {
        public TextureData()
        {
        }

        public TextureData(ITwinTexture texture) : this()
        {
            SetTwinItem(texture);
        }

        public Bitmap? Bitmap;

        public ITwinTexture.TexturePixelFormat TexturePixelFormat { get; set; }
        public ITwinTexture.TextureFunction TextureFunction { get; set; }
        public Boolean GenerateMipmaps { get; set; }

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
                Bitmap.Save(dataPath, ImageFormat.Png);
            }
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            Bitmap = new Bitmap(Image.FromFile(dataPath));
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
            var tmpBmp = new Bitmap(width, height, width * 4, PixelFormat.Format32bppArgb, bitsHandle.AddrOfPinnedObject());

            for (var x = 0; x < width; ++x)
            {
                for (var y = 0; y < height; ++y)
                {
                    var dstx = x;
                    var dsty = y;
                    bits[dstx + dsty * width] = texture.Colors[x + y * width].ToARGB();
                }
            }

            Bitmap = new Bitmap(tmpBmp);
            tmpBmp.Dispose();
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
            var bits = Bitmap.LockBits(new Rectangle(0, 0, Bitmap.Width, Bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            unsafe
            {
                var source = (byte*)bits.Scan0;
                for (var i = 0; i < bits.Height; i++)
                {
                    for (var j = 0; j < bits.Width; j++)
                    {
                        var b = source[0];
                        var g = source[1];
                        var r = source[2];
                        var a = source[3];
                        tex.Add(new Twinsanity.TwinsanityInterchange.Common.Color(r, g, b, a));
                        source += 4;
                    }
                }
            }
            texture.FromBitmap(tex, Bitmap.Width, fun, format, GenerateMipmaps);
            Bitmap.UnlockBits(bits);

            return texture;
        }
    }
}
