using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using Avalonia.Media.Imaging;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Util;

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
        
        texture = RegisterTexture(uri, textureData.Bitmap);
        if (textureAsset.GenerateMipmaps)
        {
            texture.GenerateMipmaps();
        }
        
        return texture;
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