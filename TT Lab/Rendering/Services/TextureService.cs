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
            RegisterTexture(LabURI.BoatGuy, bitmap);

            var labIcons = ManifestResourceLoader.GetFiledInExeDirectory("Media/LabIcons");
            foreach (var labIcon in labIcons)
            {
                var iconName = labIcon[(labIcon.LastIndexOf('\\') + 1)..^4];
                var iconBitmap = new Bitmap(labIcon);
                LabURI.RegisterLabIcon(iconName);
                RegisterTexture(LabURI.GetLabIcon(iconName), iconBitmap);
            }
        });
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

    private TextureBuffer RegisterTexture(string textureName, Bitmap bitmap)
    {
        Debug.Assert(!_textures.ContainsKey(textureName), "Given texture name is already registered.");
        var buffer = new TextureBuffer(_renderContext, bitmap);
        _textures.Add(textureName, buffer);
        return buffer;
    }
}