using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
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

        renderContext.QueueRenderAction(() =>
        {
            var boatGuy = ManifestResourceLoader.GetPathInExe("Media/boat_guy.png");
            var bitmap = new Bitmap(boatGuy);
            RegisterTexture(boatGuy, bitmap);
            _textures.Add("boat_guy", _textures[boatGuy]);
        });
    }

    public TextureBuffer? GetTexture(string file)
    {
        if (_textures.TryGetValue(file, out var buffer))
        {
            return buffer;
        }

        if (!File.Exists(file))
        {
            return null;
        }

        var bitmap = new Bitmap(file);
        buffer = RegisterTexture(file, bitmap);
        buffer.GenerateMipmaps();
        return buffer;
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

        var assetManager = AssetManager.Get();
        var textureData = assetManager.GetAssetData<TextureData>(uri);
        if (textureData.Bitmap == null)
        {
            return null;
        }
        
        texture = RegisterTexture(uri, textureData.Bitmap);
        if (textureData.GenerateMipmaps)
        {
            texture.GenerateMipmaps();
        }
        
        return texture;
    }

    public TextureBuffer RegisterTexture(string textureName, Bitmap bitmap)
    {
        Debug.Assert(!_textures.ContainsKey(textureName), "Given texture name is already registered.");
        var buffer = new TextureBuffer(_renderContext, bitmap);
        _textures.Add(textureName, buffer);
        return buffer;
    }
}