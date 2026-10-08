using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ReactiveUI;
using Splat;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Project;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.ViewModels.Editors.Graphics;

/// <summary>
/// The texture's picture and what building makes of it, level by level: a palette texture of a picture with more colors than its palette
/// holds is quantized, the PS2's palette textures have smaller versions for the distance (the mips), the Xbox's are DXT5 compressed
/// </summary>
public class TextureViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
    : DocumentDataViewModel<TextureData>(document, data, dependencies)
{
    private List<Bitmap> _levels = [];
    private bool _showsOriginal;
    private int _mipLevel;
    private string _buildText = string.Empty;
    // What the levels were made of, they're made again when it changes
    private (TextureData Data, ITwinTexture.TextureFunction Function, bool Mipmaps, ITwinTexture.TexturePixelFormat Format)? _builtFor;
    private int _building;

    public Bitmap? Texture => ShowsOriginal || _levels.Count == 0 ? CurrentValue?.Bitmap : _levels[Math.Min(MipLevel, _levels.Count - 1)];

    /// <summary>
    /// The picture as it's stored instead of what the game gets
    /// </summary>
    public bool ShowsOriginal
    {
        get => _showsOriginal;
        set
        {
            this.RaiseAndSetIfChanged(ref _showsOriginal, value);
            RaiseShown();
        }
    }

    public int MipLevel
    {
        get => _mipLevel;
        set
        {
            this.RaiseAndSetIfChanged(ref _mipLevel, Math.Clamp(value, 0, MaxMipLevel));
            RaiseShown();
        }
    }

    public int MaxMipLevel => Math.Max(0, _levels.Count - 1);

    public bool CanPickLevel => !ShowsOriginal && _levels.Count > 1;

    public string LevelText
    {
        get
        {
            if (ShowsOriginal || _levels.Count == 0)
            {
                return CurrentValue?.Bitmap is { } picture ? $"The picture as it's stored, {picture.PixelSize.Width}x{picture.PixelSize.Height}" : string.Empty;
            }

            var level = _levels[Math.Min(MipLevel, _levels.Count - 1)];
            var size = $"{level.PixelSize.Width}x{level.PixelSize.Height}";
            return MipLevel == 0 ? $"What the game gets, {size}" : $"Mip {MipLevel} of {MaxMipLevel}, {size}";
        }
    }

    /// <summary>
    /// How building makes the texture: quantized, its colors kept, compressed
    /// </summary>
    public string BuildText
    {
        get => _buildText;
        private set => this.RaiseAndSetIfChanged(ref _buildText, value);
    }

    private void RaiseShown()
    {
        this.RaisePropertyChanged(nameof(Texture));
        this.RaisePropertyChanged(nameof(LevelText));
        this.RaisePropertyChanged(nameof(CanPickLevel));
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);

        // The texture's settings change what building makes of the picture
        foreach (var setting in new[] { nameof(Assets.Graphics.Texture.TextureFunction), nameof(Assets.Graphics.Texture.GenerateMipmaps) })
        {
            if (Document.PropertyGraph.Find($"Root.{setting}") is { } node)
            {
                node.Changed += MakeLevels;
                Disposable.Create(() => node.Changed -= MakeLevels).DisposeWith(disposables);
            }
        }

        MakeLevels();
    }

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        MakeLevels();
        RaiseShown();
    }

    private void MakeLevels()
    {
        if (CurrentValue is not { Bitmap: { } picture } texture || Document.DocumentModel is not Texture owner)
        {
            _builtFor = null;
            ShowLevels([], string.Empty);
            return;
        }

        var key = (texture, owner.TextureFunction, owner.GenerateMipmaps, owner.PixelFormat);
        if (_builtFor == key)
        {
            return;
        }

        _builtFor = key;
        var building = ++_building;
        var pixels = texture.GetPixels();
        var width = picture.PixelSize.Width;
        var xbox = (Locator.Current.GetService<ProjectManager>()?.OpenedProject as TT_Lab.Project.Project)?.GetPlatform(owner.Package) == GamePlatform.Xbox;
        BuildText = "Making what the game gets...";
        Task.Run(() => Levels.Of(pixels, width, owner, xbox)).ContinueWith(made => Dispatcher.UIThread.Post(() =>
        {
            if (building != _building)
            {
                return;
            }

            if (made.Exception is { } failure)
            {
                ShowLevels([], $"Couldn't make what the game gets: {failure.InnerException?.Message ?? failure.Message}");
                return;
            }

            var levels = made.Result;
            ShowLevels(levels.Pixels.Select(level => TextureData.BitmapOf(level.Pixels, level.Width, level.Height)).ToList(), levels.Description);
        }));
    }

    private void ShowLevels(List<Bitmap> levels, string description)
    {
        _levels = levels;
        BuildText = description;
        this.RaisePropertyChanged(nameof(MaxMipLevel));
        MipLevel = Math.Min(MipLevel, MaxMipLevel);
        RaiseShown();
    }

    /// <summary>
    /// What building makes of a picture with the texture's settings, decoded level by level
    /// </summary>
    internal static class Levels
    {
        public record Made(List<(UInt32[] Pixels, Int32 Width, Int32 Height)> Pixels, string Description);

        public static Made Of(UInt32[] pixels, Int32 width, Texture owner, bool xbox)
        {
            ITwinItemFactory factory = xbox ? new XboxItemFactory() : new PS2ItemFactory();
            var built = TextureData.Build(factory, pixels, width, owner);
            var levels = built.DecodeLevels();
            var height = pixels.Length / width;
            var made = new List<(UInt32[] Pixels, Int32 Width, Int32 Height)>();
            for (var level = 0; level < levels.Count; level++)
            {
                made.Add((levels[level].Select(color => color.ToARGB()).ToArray(), Math.Max(1, width >> level), Math.Max(1, height >> level)));
            }

            return new Made(made, Describe(built, pixels, owner, xbox));
        }

        private static string Describe(ITwinTexture built, UInt32[] pixels, Texture owner, bool xbox)
        {
            if (xbox)
            {
                return built.TextureFormat == ITwinTexture.TexturePixelFormat.DXT5
                    ? "The Xbox version gets it DXT5 compressed, without mips"
                    : "The Xbox version gets it with every color, without mips";
            }

            var colors = pixels.Distinct().Count();
            var mips = built.MipLevels > 1 ? $", with {built.MipLevels - 1} mips for the distance" : ", without mips";
            if (built.TextureFormat == ITwinTexture.TexturePixelFormat.PSMT8)
            {
                return colors > 256
                    ? $"{colors} colors, more than a palette holds: quantized into a palette of 256{mips}"
                    : $"{colors} colors, the palette keeps every one{mips}";
            }

            return owner.PixelFormat == ITwinTexture.TexturePixelFormat.PSMT8
                ? $"{colors} colors, every one kept (32 bit): the game's tools made no palette textures of this size{mips}"
                : $"{colors} colors, every one kept (32 bit){mips}";
        }
    }

    public async Task ReplaceButton()
    {
        var file = await MiscUtils.GetFileFromDialogueAsync("Choose an image file...", "Image files", ["*.jpg","*.png","*.bmp"]);
        ReplaceWith(file);
    }

    internal void ReplaceWith(string? file)
    {
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
            TextureData data;
            try
            {
                using var stream = new FileStream(e.File, FileMode.Open, FileAccess.Read);
                data = TextureData.FromPng((IAsset)Document.DocumentModel, stream);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException)
            {
                Log.WriteLine($"Couldn't read the image: {exception.Message}");
                return;
            }

            var image = data.Bitmap!;
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

            SetValueCommand.Execute(data);
            this.RaisePropertyChanged(nameof(Texture));
        }
        else if (e.Data != null)
        {
            try
            {
                var texAsset = AssetManager.Get().GetAsset((LabURI)e.Data.Data);
                var source = texAsset.GetData<TextureData>();
                SetValueCommand.Execute(source.Bitmap == null ? new TextureData((IAsset)Document.DocumentModel) : TextureData.Copy((IAsset)Document.DocumentModel, source));
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
