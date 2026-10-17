using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Avalonia.Media.Imaging;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;

namespace TT_Lab.Rendering.Services;

public class TextureService
{
    private readonly RenderContext _renderContext;
    private readonly Dictionary<string, TextureBuffer> _textures = [];

    public TextureService(RenderContext renderContext)
    {
        _renderContext = renderContext;

        RegisterTexture(LabURI.BoatGuy, ManifestResourceLoader.LoadBitmap(MiscUtils.BoatGuyPath));

        foreach (var labIcon in ManifestResourceLoader.GetFilesIn("Media/LabIcons"))
        {
            var iconName = Path.GetFileNameWithoutExtension(labIcon);
            LabURI.RegisterLabIcon(iconName);
            RegisterTexture(LabURI.GetLabIcon(iconName), ManifestResourceLoader.LoadBitmap(labIcon));
        }
    }

    public TextureBuffer? GetTexture(LabURI uri)
    {
        if (_textures.TryGetValue(uri, out var texture))
        {
            return texture;
        }

        if (uri == LabURI.Empty)
        {
            return null;
        }

        _renderContext.ReadAssets.TryAdd(uri, true);
        var assetManager = AssetManager.Get();
        var textureAsset = assetManager.GetAsset<Texture>(uri);
        var textureData = assetManager.GetAssetData<TextureData>(uri);
        if (textureData.Bitmap == null)
        {
            return null;
        }

        // The pixels as they are: the GS blends by straight alpha, the bitmap's are premultiplied, which darkened every partly
        // transparent pixel and the edges filtering blends into the see-through ones
        var width = textureData.Bitmap.PixelSize.Width;
        var height = textureData.Bitmap.PixelSize.Height;
        var pixels = textureData.GetPixels();
        texture = new TextureBuffer(_renderContext, MemoryMarshal.AsBytes(pixels.AsSpan()), (uint)width, (uint)height);
        _textures.Add(uri, texture);
        // As many levels as building gives the PS2's texture, which the GS picks by the distance
        var levels = PS2AnyTexture.LevelsOf(width, height, textureAsset.GenerateMipmaps);
        if (levels > 1)
        {
            texture.GenerateMipmaps(levels);
        }
        
        return texture;
    }

    /// <summary>
    /// The texture asset's picture replaced (painted in Blender): what draws it draws the new one. On the render thread
    /// </summary>
    public void Refresh(LabURI uri)
    {
        var assetManager = AssetManager.Get();
        if (!_textures.TryGetValue(uri, out var texture) || !assetManager.DoesAssetExist(uri))
        {
            return;
        }

        var textureAsset = assetManager.GetAsset<Texture>(uri);
        var textureData = assetManager.GetAssetData<TextureData>(uri);
        if (textureData.Bitmap == null)
        {
            return;
        }

        var width = textureData.Bitmap.PixelSize.Width;
        var height = textureData.Bitmap.PixelSize.Height;
        texture.Replace(MemoryMarshal.AsBytes(textureData.GetPixels().AsSpan()), (uint)width, (uint)height, PS2AnyTexture.LevelsOf(width, height, textureAsset.GenerateMipmaps));
    }

    /// <summary>
    /// A picture that's no texture asset (a save icon's), drawn by materials naming the URI it's given; giving the URI again replaces it
    /// </summary>
    public TextureBuffer SetTexture(LabURI uri, byte[] bgra, uint width, uint height)
    {
        if (_textures.Remove(uri, out var previous))
        {
            previous.Dispose();
        }

        var texture = new TextureBuffer(_renderContext, bgra, width, height);
        _textures.Add(uri, texture);
        return texture;
    }

    private TextureBuffer RegisterTexture(string textureName, Bitmap bitmap)
    {
        Debug.Assert(!_textures.ContainsKey(textureName), "Given texture name is already registered.");
        var buffer = new TextureBuffer(_renderContext, bitmap);
        _textures.Add(textureName, buffer);
        return buffer;
    }
}