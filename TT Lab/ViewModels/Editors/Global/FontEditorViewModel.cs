using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Media.Imaging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Controls;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors.Global;

public sealed record FontPage(int Index, string Name, LabURI Texture, Bitmap? Image);

/// <summary>
/// A character of the font's table: its code, the page it's on and its box, edited through the document's nodes so the history
/// follows
/// </summary>
public sealed partial class FontCharacter : ReactiveObject
{
    private readonly FontEditorViewModel _editor;

    public FontCharacter(FontEditorViewModel editor, int index)
    {
        _editor = editor;
        Index = index;
    }

    public int Index { get; }

    public int Code => _editor.SpaceIdentifier + Index;

    public string Symbol => Code is > 31 and <= Char.MaxValue ? ((char)Code).ToString() : $"#{Code}";

    public string Caption => $"{Symbol} (0x{Code:X2})";

    public int Page
    {
        get => (int)_editor.Get<byte>(Index, "FontPageSpecifier");
        set => _editor.Set(Index, "FontPageSpecifier", (byte)Math.Clamp(value, 0, 255));
    }

    public int Left
    {
        get => (int)MathF.Round(_editor.Get<float>(Index, "PageUv.X"));
        set => _editor.Set(Index, "PageUv.X", (float)value);
    }

    public int Bottom
    {
        get => (int)MathF.Round(_editor.Get<float>(Index, "PageUv.Y"));
        set => _editor.Set(Index, "PageUv.Y", (float)value);
    }

    public int Width
    {
        get => (int)MathF.Round(_editor.Get<float>(Index, "Size.X"));
        set => _editor.Set(Index, "Size.X", (float)value);
    }

    public int Height
    {
        get => (int)MathF.Round(_editor.Get<float>(Index, "Size.Y"));
        set => _editor.Set(Index, "Size.Y", (float)value);
    }

    /// <summary>
    /// The page the box is on: the game's first page is 0 or 1, the second 2 and so on
    /// </summary>
    public int PageIndex => Math.Max(Page - 1, 0);

    public FontBox Box => new(Index, Left, Bottom, Width, Height);

    internal void Refresh()
    {
        this.RaisePropertyChanged(nameof(Page));
        this.RaisePropertyChanged(nameof(Left));
        this.RaisePropertyChanged(nameof(Bottom));
        this.RaisePropertyChanged(nameof(Width));
        this.RaisePropertyChanged(nameof(Height));
        this.RaisePropertyChanged(nameof(Symbol));
        this.RaisePropertyChanged(nameof(Caption));
    }
}

/// <summary>
/// A font's pages with their characters' boxes drawn over them and the character table: boxes get dragged on the page or typed in,
/// pages get their picture replaced
/// </summary>
public partial class FontEditorViewModel : DocumentDataViewModel<List<VectorCharacterData>>
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<FontPage> _pages = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<FontCharacter> _characters = [];

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<FontBox> _boxes = [];

    [Reactive]
    private FontPage? _selectedPage;

    [Reactive]
    private FontCharacter? _selectedCharacter;

    [Reactive]
    private int _selectedIndex = -1;

    private IDisposable? _drag;
    private bool _following;

    public FontEditorViewModel(DocumentViewModel document, PropertyNode node) : base(document, node)
    {
    }

    public int SpaceIdentifier => Property.Parent?.Find(nameof(FontData.SpaceIdentifier))?.GetValue() is int space ? space : 32;

    protected override void OnActivated(CompositeDisposable disposables)
    {
        base.OnActivated(disposables);
        Reload();
        Document.History.Changed += OnHistoryChanged;
        Disposable.Create(() => Document.History.Changed -= OnHistoryChanged).DisposeWith(disposables);
        this.WhenAnyValue(x => x.SelectedIndex).Subscribe(index =>
        {
            if (_following)
            {
                return;
            }

            _following = true;
            SelectedCharacter = index >= 0 && index < Characters.Count ? Characters[index] : null;
            _following = false;
        }).DisposeWith(disposables);
        this.WhenAnyValue(x => x.SelectedCharacter).Subscribe(character =>
        {
            if (character != null && character.PageIndex < Pages.Count)
            {
                SelectedPage = Pages[character.PageIndex];
            }

            if (_following)
            {
                return;
            }

            _following = true;
            SelectedIndex = character?.Index ?? -1;
            _following = false;
        }).DisposeWith(disposables);
        this.WhenAnyValue(x => x.SelectedPage).Subscribe(_ =>
        {
            UpdateBoxes();
            this.RaisePropertyChanged(nameof(SelectedImage));
        }).DisposeWith(disposables);
    }

    // The picture of the page shown, none while there's no page (a binding through the page logged an error then)
    public Bitmap? SelectedImage => SelectedPage?.Image;

    protected override void OnCurrentValueChanged()
    {
        base.OnCurrentValueChanged();
        Reload();
    }

    private void OnHistoryChanged()
    {
        foreach (var character in Characters)
        {
            character.Refresh();
        }

        // A character moved to another page takes the view along
        if (SelectedCharacter != null && SelectedCharacter.PageIndex < Pages.Count && SelectedPage?.Index != SelectedCharacter.PageIndex)
        {
            SelectedPage = Pages[SelectedCharacter.PageIndex];
        }

        UpdateBoxes();
    }

    private void Reload()
    {
        var count = CurrentValue?.Count ?? 0;
        if (Characters.Count != count)
        {
            Characters = Enumerable.Range(0, count).Select(index => new FontCharacter(this, index)).ToList();
        }

        Pages = LoadPages();
        SelectedPage = SelectedPage != null && SelectedPage.Index < Pages.Count ? Pages[SelectedPage.Index] : Pages.FirstOrDefault();
        UpdateBoxes();
    }

    private IReadOnlyList<FontPage> LoadPages()
    {
        var assetManager = AssetManager.Get();
        var pages = new List<FontPage>();
        if (Property.Parent?.Find(nameof(FontData.FontPages))?.GetValue() is not List<LabURI> uris)
        {
            return pages;
        }

        for (var i = 0; i < uris.Count; i++)
        {
            try
            {
                var page = assetManager.GetAssetData<PTCData>(uris[i]);
                var texture = assetManager.GetAssetData<TextureData>(page.TextureID);
                pages.Add(new FontPage(i, $"Page {i + 1}: {assetManager.GetAsset(uris[i]).Alias}", page.TextureID, texture.Bitmap));
            }
            catch (Exception ex)
            {
                Log.WriteLine($"Couldn't read font page {i}: {ex.Message}", Log.LogType.Warning);
                pages.Add(new FontPage(i, $"Page {i + 1}", LabURI.Empty, null));
            }
        }

        return pages;
    }

    private void UpdateBoxes()
    {
        var page = SelectedPage?.Index ?? -1;
        Boxes = Characters.Where(character => character.PageIndex == page && character.Width > 0 && character.Height > 0).Select(character => character.Box).ToList();
    }

    internal T Get<T>(int index, string path) => Node(index, path)?.GetValue() is T value ? value : default!;

    internal void Set(int index, string path, object value)
    {
        Node(index, path)?.SetValue(value);
        if (_drag == null)
        {
            OnHistoryChanged();
        }
    }

    private PropertyNode? Node(int index, string path) => Document.PropertyGraph.Find($"{Property.Path}[{index}].{path}");

    /// <summary>
    /// A drag on the page: one step of the history from the first move to the end
    /// </summary>
    public void BeginDrag(int index)
    {
        _drag?.Dispose();
        _drag = Document.History.BeginGroup();
        SelectedIndex = index;
    }

    public void DragBox(FontBox box)
    {
        if (box.Index < 0 || box.Index >= Characters.Count)
        {
            return;
        }

        var character = Characters[box.Index];
        character.Left = box.Left;
        character.Bottom = box.Bottom;
        character.Width = box.Width;
        character.Height = box.Height;
        character.Refresh();
        UpdateBoxes();
    }

    public void EndDrag()
    {
        _drag?.Dispose();
        _drag = null;
        OnHistoryChanged();
    }

    [ReactiveCommand]
    private void AddCharacter()
    {
        using (Document.History.BeginGroup())
        {
            Property.AddElement();
        }

        Reload();
        SelectedCharacter = Characters.LastOrDefault();
    }

    [ReactiveCommand]
    private void RemoveLastCharacter()
    {
        var last = Document.PropertyGraph.Find($"{Property.Path}[{Characters.Count - 1}]");
        if (last == null)
        {
            return;
        }

        using (Document.History.BeginGroup())
        {
            Property.RemoveElement(last);
        }

        Reload();
    }

    /// <summary>
    /// Puts a PNG in place of the selected page's picture: the page's texture asset gets the pixels and is saved, which no history
    /// can undo
    /// </summary>
    public bool ReplacePage(Stream png)
    {
        var page = SelectedPage;
        if (page == null || page.Texture == LabURI.Empty)
        {
            return false;
        }

        var texture = AssetManager.Get().GetAsset(page.Texture);
        TextureData data;
        try
        {
            data = TextureData.FromPng(texture, png);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            Log.WriteLine($"Couldn't read the image: {exception.Message}", Log.LogType.Error);
            return false;
        }

        var size = data.Bitmap!.PixelSize;
        if (size.Width is < 8 or > 256 || size.Height is < 8 or > 256 || !MathExtension.IsPowerOfTwo(size.Width) || !MathExtension.IsPowerOfTwo(size.Height))
        {
            Log.WriteLine($"A font page has to be 8 to 256 pixels wide and tall, a power of two each way, not {size.Width}x{size.Height}", Log.LogType.Error);
            return false;
        }

        texture.SetData(data);
        texture.Serialize(SerializationFlags.SaveData | SerializationFlags.PreserveData);
        Reload();
        return true;
    }
}
