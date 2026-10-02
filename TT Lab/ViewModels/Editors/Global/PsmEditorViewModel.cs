using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;

namespace TT_Lab.ViewModels.Editors.Global;

/// <summary>
/// A part of a PSM: its texture, the name its material has in the game and where the picture shows it
/// </summary>
public sealed record PsmPart(int Index, string Name, LabURI Texture, PsmPartPlace Place)
{
    public string Caption => $"{Index + 1}. {Name} ({Place.Width}x{Place.Height})";
}

/// <summary>
/// A PSM's picture, its parts put together the way the game shows them (<see cref="PsmLayout"/>). A picture of tiles gets replaced whole
/// or a tile at a time, Icons.psm's icons one at a time. Replacing puts the pixels into the parts' texture assets and saves them, which
/// no history can undo, like a font's pages
/// </summary>
public partial class PsmEditorViewModel : DocumentDataViewModel<List<LabURI>>
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<PsmPart> _parts = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private Bitmap? _picture;

    [Reactive(SetModifier = AccessModifier.Private)]
    private PsmLayout _layout = PsmLayout.Of(string.Empty, []);

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _summary = string.Empty;

    [Reactive]
    private int _selectedIndex = -1;

    private UInt32[] _picturePixels = [];

    public PsmEditorViewModel(DocumentViewModel document, PropertyNode node) : base(document, node)
    {
    }

    /// <summary>
    /// The parts are tiles of one picture, which gets replaced whole as well
    /// </summary>
    public bool IsPicture => Layout.IsPicture;

    public IReadOnlyList<PsmPartPlace> Places => Layout.Parts;

    /// <summary>
    /// The PSM's own name (the game's icons are Icons), what the picture gets saved as
    /// </summary>
    public string Name => PsmName;

    public PsmPart? SelectedPart => SelectedIndex >= 0 && SelectedIndex < Parts.Count ? Parts[SelectedIndex] : null;

    public bool HasSelection => SelectedPart != null;

    public string SelectionText => SelectedPart is { } part ? $"Part {part.Caption}" : IsPicture ? "Click a tile to pick it" : "Click a picture to pick it";

    // The PSM's own name tells the game's icons apart
    private string PsmName
    {
        get
        {
            for (var node = Property.Parent; node != null; node = node.Parent)
            {
                // Without the variation the disc's path makes part of the name
                if (node.GetValue() is IAsset asset)
                {
                    return asset.InvariantName;
                }
            }

            return string.Empty;
        }
    }

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        Reload();
        this.WhenAnyValue(x => x.SelectedIndex).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(SelectedPart));
            this.RaisePropertyChanged(nameof(HasSelection));
            this.RaisePropertyChanged(nameof(SelectionText));
        }).DisposeWith(disposables);
    }

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        Reload();
    }

    private void Reload()
    {
        var assetManager = AssetManager.Get();
        var uris = CurrentValue ?? [];
        var names = new List<string>();
        var textures = new List<LabURI>();
        var pixels = new List<UInt32[]?>();
        var sizes = new List<(int Width, int Height)>();
        for (var i = 0; i < uris.Count; i++)
        {
            try
            {
                var part = assetManager.GetAssetData<PTCData>(uris[i]);
                var texture = assetManager.GetAssetData<TextureData>(part.TextureID);
                var size = texture.Bitmap?.PixelSize ?? new PixelSize(0, 0);
                names.Add(part.MaterialID != LabURI.Empty && assetManager.DoesAssetExist(part.MaterialID) ? assetManager.GetAssetData<MaterialData>(part.MaterialID).Name : string.Empty);
                textures.Add(part.TextureID);
                pixels.Add(texture.Bitmap == null ? null : texture.GetPixels());
                sizes.Add((size.Width, size.Height));
            }
            catch (Exception exception)
            {
                Log.WriteLine($"Couldn't read part {i + 1} of the PSM: {exception.Message}", Log.LogType.Warning);
                names.Add(string.Empty);
                textures.Add(LabURI.Empty);
                pixels.Add(null);
                sizes.Add((0, 0));
            }
        }

        Layout = PsmLayout.Of(PsmName, sizes);
        Parts = Layout.Parts.Select(place => new PsmPart(place.Index, string.IsNullOrEmpty(names[place.Index]) ? $"Part {place.Index + 1}" : names[place.Index], textures[place.Index], place)).ToList();
        _picturePixels = Layout.Compose(pixels);
        Picture = ToBitmap(_picturePixels, Layout.Width, Layout.Height);
        Summary = Describe();
        if (SelectedIndex >= Parts.Count)
        {
            SelectedIndex = -1;
        }

        this.RaisePropertyChanged(nameof(IsPicture));
        this.RaisePropertyChanged(nameof(Places));
        this.RaisePropertyChanged(nameof(SelectedPart));
        this.RaisePropertyChanged(nameof(HasSelection));
        this.RaisePropertyChanged(nameof(SelectionText));
    }

    private string Describe()
    {
        if (Parts.Count == 0)
        {
            return "The PSM has no parts";
        }

        if (!Layout.IsPicture)
        {
            return $"{Parts.Count} pictures of their own, the game draws each on its own (the HUD's icons), so they're replaced one at a time. " +
                   "The game keeps them upside down, they're shown the right way up";
        }

        var first = Layout.Parts[0];
        return Parts.Count == 1
            ? $"A {Layout.Width}x{Layout.Height} picture, kept upside down by the game and shown the right way up"
            : $"A {Layout.Width}x{Layout.Height} picture of {Parts.Count} tiles of {first.Width}x{first.Height}, {Layout.Width / first.Width} across, " +
              "each kept upside down by the game and shown the right way up";
    }

    private static Bitmap? ToBitmap(UInt32[] pixels, int width, int height)
    {
        if (width == 0 || height == 0)
        {
            return null;
        }

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

    /// <summary>
    /// Puts an image in place of the whole picture, resized to it when it's another size, every tile's texture asset gets its part
    /// </summary>
    public bool ReplacePicture(Stream png)
    {
        if (!Layout.IsPicture)
        {
            Log.WriteLine("The PSM's parts are pictures of their own, they're replaced one at a time", Log.LogType.Warning);
            return false;
        }

        if (!TryDecode(png, Layout.Width, Layout.Height, "picture", out var pixels))
        {
            return false;
        }

        for (var i = 0; i < Parts.Count; i++)
        {
            SetPart(Parts[i], Layout.Cut(pixels, i));
        }

        Reload();
        return true;
    }

    /// <summary>
    /// Puts an image in place of the picked part, resized to it when it's another size
    /// </summary>
    public bool ReplacePart(Stream png)
    {
        if (SelectedPart is not { } part)
        {
            return false;
        }

        if (!TryDecode(png, part.Place.Width, part.Place.Height, "part", out var pixels))
        {
            return false;
        }

        SetPart(part, PsmLayout.Flip(pixels, part.Place.Width, part.Place.Height));
        Reload();
        return true;
    }

    /// <summary>
    /// The picture shown as a PNG, what <see cref="ReplacePicture"/> takes back
    /// </summary>
    public byte[] GetPicturePng() => TextureData.EncodePng(_picturePixels, Layout.Width, Layout.Height);

    /// <summary>
    /// The picked part the right way up as a PNG, what <see cref="ReplacePart"/> takes back
    /// </summary>
    public byte[]? GetPartPng()
    {
        if (SelectedPart is not { } part)
        {
            return null;
        }

        return TextureData.EncodePng(PsmLayout.Flip(Layout.Cut(_picturePixels, part.Index), part.Place.Width, part.Place.Height), part.Place.Width, part.Place.Height);
    }

    private static bool TryDecode(Stream png, int width, int height, string what, out UInt32[] pixels)
    {
        pixels = [];
        try
        {
            var (decoded, decodedWidth, decodedHeight) = TextureData.DecodePng(png);
            if (decodedWidth != width || decodedHeight != height)
            {
                decoded = TextureData.Resample(decoded, decodedWidth, decodedHeight, width, height);
                Log.WriteLine($"The {decodedWidth}x{decodedHeight} image was resized to the {what}'s {width}x{height}");
            }

            pixels = decoded;
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        {
            Log.WriteLine($"Couldn't read the image: {exception.Message}", Log.LogType.Error);
            return false;
        }
    }

    private static void SetPart(PsmPart part, UInt32[] pixels)
    {
        if (part.Texture == LabURI.Empty)
        {
            return;
        }

        var texture = AssetManager.Get().GetAsset(part.Texture);
        texture.SetData(TextureData.FromPixels(texture, pixels, part.Place.Width, part.Place.Height));
        texture.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
    }
}
