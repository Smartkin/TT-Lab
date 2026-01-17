using Caliburn.Micro;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.ViewModels.Editors.Graphics;

public class TextureViewModel : ResourceEditorViewModel
{
    private Bitmap? _texture;
    private ITwinTexture.TextureFunction _texFun;
    private ITwinTexture.TexturePixelFormat _pixelFormat;
    private Boolean _generateMipmaps;

    static TextureViewModel()
    {
        TextureFunctions = new ObservableCollection<object>(Enum.GetValues(typeof(ITwinTexture.TextureFunction)).Cast<object>());
        PixelFormats = new ObservableCollection<object>(Enum.GetValues(typeof(ITwinTexture.TexturePixelFormat)).Cast<object>());
    }

    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset<Assets.Graphics.Texture>(EditableResource);
        var data = (TextureData)asset.GetData();
        data.Bitmap = Texture;
        asset.PixelFormat = PixelStorageFormat;
        asset.TextureFunction = TextureFunction;
        asset.GenerateMipmaps = GenerateMipmaps;
        asset.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
            
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset<Assets.Graphics.Texture>(EditableResource);
        _pixelFormat = asset.PixelFormat;
        _texFun = asset.TextureFunction;
        _generateMipmaps = asset.GenerateMipmaps;
            
        var textureBitmap = ((TextureData)asset.GetData()).Bitmap;
        if (textureBitmap == null)
        {
            return;
        }
            
        _texture = textureBitmap.CloneBitmap();
    }

    protected override Task OnDeactivateAsync(Boolean close, CancellationToken cancellationToken)
    {
        if (close)
        {
            _texture?.Dispose();
            _texture = null;
        }

        return base.OnDeactivateAsync(close, cancellationToken);
    }

    public async Task ReplaceButton()
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Choose an image file...", "Image files", ["*.jpg","*.png","*.bmp"]);
        TextureViewerFileDrop(new Controls.FileDropEventArgs { File = file });
    }

    public void TextureViewerDrop(DragEventArgs e)
    {
        // if (e.Data.GetDataPresent(DataFormats.FileDrop))
        // {
        //     var file = (string[])e.Data.GetData(DataFormats.FileDrop);
        //     TextureViewerFileDrop(new Controls.FileDropEventArgs { File = file[0] });
        // }
        // else if (e.Data.GetDataPresent(typeof(Controls.DraggedData)))
        // {
        //     var data = (Controls.DraggedData)e.Data.GetData(typeof(Controls.DraggedData));
        //     TextureViewerFileDrop(new Controls.FileDropEventArgs { Data = data });
        // }
        // else
        // {
        //     Log.WriteLine("Format not compatible!");
        //     e.Effects = DragDropEffects.None;
        // }
    }

    private void TextureViewerFileDrop(Controls.FileDropEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.File))
        {
            Bitmap image = new(e.File);
            if (image.Size.Width > 256 || image.Size.Height > 256 || !MathExtension.IsPowerOfTwo((long)image.Size.Width)
                || !MathExtension.IsPowerOfTwo((long)image.Size.Height)
                || image.Size.Width < 8 || image.Size.Height < 8)
            {
                Log.WriteLine($@"Image is not compatible. Provided image dimensions: {image.Size.Width:N0}x{image.Size.Height:N0}
                * Width and height can't exceed 256 pixels
                * Width and height have to be a power of 2
                * Width and height can't be less than 8 pixels");
                image.Dispose();
                return;
            }
            Texture = image.CloneBitmap();
            NotifyOfPropertyChange(nameof(Texture));
        }
        else if (e.Data != null)
        {
            try
            {
                var texAsset = AssetManager.Get().GetAsset((LabURI)e.Data.Data);
                Texture = texAsset.GetData<TextureData>().Bitmap?.CloneBitmap();
                NotifyOfPropertyChange(nameof(Texture));
                Log.WriteLine($"Replacing with texture: {texAsset.Alias}");
            }
            catch (Exception)
            {
                Log.WriteLine($"Unsupported texture");
            }
        }
    }

    public static ObservableCollection<object> TextureFunctions { get; }

    public static ObservableCollection<object> PixelFormats { get; }

    [MarkDirty]
    public Bitmap? Texture
    {
        get => _texture;
        set => _texture = value?.CloneBitmap();
    }

    [MarkDirty]
    public ITwinTexture.TextureFunction TextureFunction
    {
        get => _texFun;
        set
        {
            if (value != _texFun)
            {
                _texFun = value;
                    
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public ITwinTexture.TexturePixelFormat PixelStorageFormat
    {
        get => _pixelFormat;
        set
        {
            if (value != _pixelFormat)
            {
                _pixelFormat = value;
                    
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public Boolean GenerateMipmaps
    {
        get => _generateMipmaps;

        set
        {
            if (value != _generateMipmaps)
            {
                _generateMipmaps = value;
                    
                NotifyOfPropertyChange();
            }
        }
    }
}