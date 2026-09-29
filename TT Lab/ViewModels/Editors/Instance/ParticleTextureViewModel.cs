using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors.Instance;

public sealed partial class ParticlePageChoice(int index, Bitmap? picture) : ReactiveObject
{
    [Reactive]
    private bool _isSelected;

    public int Index { get; } = index;
    public Bitmap? Picture { get; } = picture;
    public string Caption => $"Page {Index}";
}

// A particle system's picture: the page of the default chunk's particle textures it's on (the system's TexturePage, the node this
// edits) and the rectangle it takes from it (TextureStart and TextureEnd next to it, which have no editors of their own)
public sealed partial class ParticleTextureViewModel(DocumentViewModel document, PropertyNode data, params DocumentNodeViewModel[] dependencies)
    : DocumentDataViewModel<Int32>(document, data, dependencies)
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<ParticlePageChoice> _pages = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private Bitmap? _page;

    [Reactive(SetModifier = AccessModifier.Private)]
    private ParticleTextureRect _rect;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<ParticleSprite> _sprites = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private CroppedBitmap? _preview;

    [Reactive(SetModifier = AccessModifier.Private)]
    private string _status = string.Empty;

    [Reactive]
    private string _x = string.Empty;

    [Reactive]
    private string _y = string.Empty;

    [Reactive]
    private string _width = string.Empty;

    [Reactive]
    private string _height = string.Empty;

    [Reactive]
    private bool _mirrorX;

    [Reactive]
    private bool _mirrorY;

    private List<ParticleSprite> _allSprites = [];
    private bool _isShowing;
    private IDisposable? _drag;
    private ParticleSprite? _hovered;

    public double PreviewScaleX => Rect.MirrorX ? -1.0 : 1.0;
    public double PreviewScaleY => Rect.MirrorY ? -1.0 : 1.0;

    private PropertyNode? StartNode => Property.Parent?.Find(nameof(ParticleSystem.TextureStart));
    private PropertyNode? EndNode => Property.Parent?.Find(nameof(ParticleSystem.TextureEnd));

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        foreach (var node in new[] { StartNode, EndNode })
        {
            if (node == null)
            {
                continue;
            }

            node.Changed += Refresh;
            Disposable.Create(() => node.Changed -= Refresh).DisposeWith(disposables);
        }

        LoadPages();
        this.WhenAnyValue(x => x.X, x => x.Y, x => x.Width, x => x.Height).Skip(1).Where(_ => !_isShowing).Subscribe(_ => WriteTyped()).DisposeWith(disposables);
        this.WhenAnyValue(x => x.MirrorX, x => x.MirrorY).Skip(1).Where(_ => !_isShowing).Subscribe(_ => Write(Rect with { MirrorX = MirrorX, MirrorY = MirrorY })).DisposeWith(disposables);
        Refresh();
    }

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        Refresh();
    }

    private ParticleData? GetParticles()
    {
        for (var node = Property.Parent; node != null; node = node.Parent)
        {
            if (node.GetValue() is ParticleData particles)
            {
                return particles;
            }
        }

        return null;
    }

    private void LoadPages()
    {
        var particles = GetParticles();
        var pages = particles?.GetTexturePages() ?? [];
        var assetManager = AssetManager.Get();
        Pages = pages.Select((uri, index) => new ParticlePageChoice(index, assetManager.DoesAssetExist(uri) ? GetPicture(uri) : null)).ToList();
        _allSprites = particles == null ? [] : ParticleTextureBank.Build(particles.GetUsableSystems().Select(usable => usable.System));
    }

    private static Bitmap? GetPicture(LabURI uri)
    {
        try
        {
            return AssetManager.Get().GetAssetData<TextureData>(uri).Bitmap;
        }
        catch (Exception ex)
        {
            Log.WriteLine($"The particle texture page {uri} couldn't be shown: {ex.Message}", Log.LogType.Warning);
            return null;
        }
    }

    private void Refresh()
    {
        var start = StartNode?.GetValue() as Vector2 ?? new Vector2();
        var end = EndNode?.GetValue() as Vector2 ?? new Vector2();
        var pageIndex = CurrentValue;
        var choice = Pages.FirstOrDefault(page => page.Index == pageIndex);
        foreach (var page in Pages)
        {
            page.IsSelected = page == choice;
        }

        Page = choice?.Picture;
        Sprites = _allSprites.Where(sprite => sprite.Page == pageIndex).ToList();
        Rect = ParticleTextureRect.FromGame(start, end);
        _isShowing = true;
        X = Rect.X.ToString(CultureInfo.InvariantCulture);
        Y = Rect.Y.ToString(CultureInfo.InvariantCulture);
        Width = Rect.Width.ToString(CultureInfo.InvariantCulture);
        Height = Rect.Height.ToString(CultureInfo.InvariantCulture);
        MirrorX = Rect.MirrorX;
        MirrorY = Rect.MirrorY;
        _isShowing = false;
        this.RaisePropertyChanged(nameof(PreviewScaleX));
        this.RaisePropertyChanged(nameof(PreviewScaleY));
        Preview = MakePreview();
        UpdateStatus();
    }

    private CroppedBitmap? MakePreview()
    {
        if (Page == null)
        {
            return null;
        }

        var size = Page.PixelSize;
        var rect = Rect.Within(size.Width, size.Height);
        return new CroppedBitmap(Page, new PixelRect(rect.X, rect.Y, rect.Width, rect.Height));
    }

    private void UpdateStatus()
    {
        if (Pages.Count == 0)
        {
            Status = "No texture pages: the default chunk's particles have none";
            return;
        }

        if (Page == null)
        {
            Status = CurrentValue < 0 || CurrentValue >= Pages.Count ? $"Page {CurrentValue} doesn't exist, pick one of the {Pages.Count}" : $"Page {CurrentValue} couldn't be shown";
            return;
        }

        var sprite = _hovered ?? Sprites.FirstOrDefault(sprite => sprite.Rect == Rect with { MirrorX = false, MirrorY = false });
        Status = sprite != null ? $"{sprite.Description}" : $"{Rect.Width}×{Rect.Height} pixels at {Rect.X}, {Rect.Y}";
    }

    public void ChoosePage(int index)
    {
        if (index == CurrentValue)
        {
            return;
        }

        SetCurrentValue(index);
    }

    public void PickSprite(ParticleSprite sprite)
    {
        Write(sprite.Rect with { MirrorX = Rect.MirrorX, MirrorY = Rect.MirrorY });
    }

    public void HoverSprite(ParticleSprite? sprite)
    {
        _hovered = sprite;
        UpdateStatus();
    }

    public void BeginDrag()
    {
        _drag?.Dispose();
        _drag = Document.History.BeginGroup("Moved the particle picture");
    }

    public void DragRect(ParticleTextureRect rect)
    {
        Write(rect);
    }

    public void EndDrag()
    {
        _drag?.Dispose();
        _drag = null;
    }

    private void WriteTyped()
    {
        if (!int.TryParse(X, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) || !int.TryParse(Y, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            || !int.TryParse(Width, NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) || !int.TryParse(Height, NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
        {
            return;
        }

        Write(Rect with { X = x, Y = y, Width = width, Height = height });
    }

    // Both corners change as one step, they're one picture
    public void Write(ParticleTextureRect rect)
    {
        var (startNode, endNode) = (StartNode, EndNode);
        if (startNode == null || endNode == null)
        {
            return;
        }

        var size = Page?.PixelSize ?? new PixelSize(ParticleTextureRect.MaxPixel, ParticleTextureRect.MaxPixel);
        rect = rect.Within(size.Width, size.Height);
        var start = startNode.GetValue() as Vector2 ?? new Vector2();
        var end = endNode.GetValue() as Vector2 ?? new Vector2();
        if (rect == ParticleTextureRect.FromGame(start, end))
        {
            Refresh();
            return;
        }

        var (newStart, newEnd) = rect.ToGame(start, end);
        using (_drag == null ? Document.History.BeginGroup("Changed the particle picture") : null)
        {
            startNode.SetValue(newStart);
            endNode.SetValue(newEnd);
        }

        Refresh();
    }
}
