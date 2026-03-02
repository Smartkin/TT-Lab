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
using ReactiveUI;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;

namespace TT_Lab.ViewModels.Editors.Graphics;

public class TextureViewModel(DocumentViewModel document, TextureData data)
    : DocumentDataViewModel<TextureData>(document, data)
{
    public Bitmap? Texture => Data.Bitmap;

    public async Task ReplaceButton()
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Choose an image file...", "Image files", ["*.jpg","*.png","*.bmp"]);
        TextureViewerFileDrop(new Controls.FileDropEventArgs { File = file });
    }

    public override void Save()
    {
        Data.GetOwner().SetData(Data);
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

            Data = new TextureData((IAsset)Document.DocumentModel);
            Data.Bitmap = image.CloneBitmap();
            this.RaisePropertyChanged(nameof(Texture));
        }
        else if (e.Data != null)
        {
            try
            {
                var texAsset = AssetManager.Get().GetAsset((LabURI)e.Data.Data);
                Data = new TextureData((IAsset)Document.DocumentModel);
                Data.Bitmap = texAsset.GetData<TextureData>().Bitmap?.CloneBitmap();
                this.RaisePropertyChanged(nameof(Texture));
                Log.WriteLine($"Replacing with texture: {texAsset.Alias}");
            }
            catch (Exception)
            {
                Log.WriteLine($"Unsupported texture");
            }
        }
    }
}