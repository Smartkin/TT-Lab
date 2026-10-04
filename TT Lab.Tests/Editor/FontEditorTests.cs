using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Global;
using TT_Lab.Views.Editors.Global;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Editor;

// A font's characters are boxes on its pages: the editor shows every page with its boxes, drags them, edits the table and replaces a
// page's picture, all through the document so undo follows
[Collection(ProjectCollection.Name)]
public sealed class FontEditorTests : IDisposable
{
    private const int PageSize = 16;

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static UInt32 PixelAt(int page, int column, int row) => 0xFF000000 | (UInt32)(column << 16) | (UInt32)(row << 8) | (UInt32)page;

    private static byte[] EncodePng(UInt32[] argb, int size)
    {
        var handle = GCHandle.Alloc(argb, GCHandleType.Pinned);
        try
        {
            using var bitmap = new SKBitmap();
            bitmap.InstallPixels(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul), handle.AddrOfPinnedObject());
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    private static byte[] PagePng(int page, int size = PageSize)
    {
        var pixels = new UInt32[size * size];
        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                pixels[row * size + column] = PixelAt(page, column, row);
            }
        }

        return EncodePng(pixels, size);
    }

    private (PTC Page, Texture Texture) AddPage(string name, int page)
    {
        var texture = _project.Add(new Texture(), $"{name} texture");
        texture.SetData(TextureData.FromPng(texture, new MemoryStream(PagePng(page))));
        var ptc = _project.Add(new PTC { GlobalPath = "Startup/Fonts" }, name);
        ptc.SetData(new PTCData(ptc) { TextureID = texture.URI });
        return (ptc, texture);
    }

    private static VectorCharacterData Character(float left, float bottom, float width, float height, byte page) => new()
    {
        PageUv = new Vector2 { X = left, Y = bottom },
        Size = new Vector2 { X = width, Y = height },
        FontPageSpecifier = page,
    };

    private (Font Font, FontData Data, Texture FirstPage) AddFont()
    {
        var characters = Enumerable.Range(0, 'Z' - ' ' + 1).Select(_ => new VectorCharacterData()).ToList();
        characters[0] = Character(0, 8, 3, 4, 0);
        characters['A' - ' '] = Character(2, 6, 4, 5, 1);
        characters['Z' - ' '] = Character(5, 3, 2, 2, 2);
        var (first, texture) = AddPage("Crash page 0", 0);
        var font = _project.Add(new Font { GlobalPath = "Startup/Fonts" }, "Crash");
        var data = new FontData(font) { FontPages = [first.URI, AddPage("Crash page 1", 1).Page.URI], CharacterData = characters, SpaceIdentifier = 32 };
        font.SetData(data);
        return (font, data, texture);
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    // Clicks and captured frames go by what the compositor got last, which only takes a frame once the render timer took the one before
    // it: a window another test showed can still hold one (the Windows runner captured the page's background where its picture was)
    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private (DocumentViewModel Document, FontEditorViewModel Editor, Window Window) Open(Font font)
    {
        var document = new DocumentViewModel(font);
        document.Initialize();
        var editor = Assert.IsType<FontEditorViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.CharacterData")!).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 1000, Height = 700 };
        window.Show();
        Pump();
        Render();
        return (document, editor, window);
    }

    [AvaloniaFact]
    public void PagesAndCharactersAreListedAndPickingACharacterShowsItsPage()
    {
        var (font, _, _) = AddFont();
        var (_, editor, _) = Open(font);

        Assert.Equal(2, editor.Pages.Count);
        Assert.All(editor.Pages, page => Assert.NotNull(page.Image));
        Assert.Equal('Z' - ' ' + 1, editor.Characters.Count);
        Assert.Equal("A (0x41)", editor.Characters['A' - ' '].Caption);
        // The first page's boxes are the ones on the game's page 0 and 1, the space and the A
        Assert.Equal([0, 'A' - ' '], editor.Boxes.Select(box => box.Index));

        editor.SelectedCharacter = editor.Characters['Z' - ' '];
        Pump();

        Assert.Equal(1, editor.SelectedPage!.Index);
        Assert.Equal([new FontBox('Z' - ' ', 5, 3, 2, 2)], editor.Boxes);
        Assert.Equal('Z' - ' ', editor.SelectedIndex);
    }

    [AvaloniaFact]
    public void DraggingABoxMovesItsCharacterInOneStep()
    {
        var (font, data, _) = AddFont();
        var (document, editor, window) = Open(font);
        var page = window.GetVisualDescendants().OfType<FontPageEditor>().Single();
        var zoom = page.Zoom;
        // The A's box is 4 by 5 pixels up from row 6, shown the right way up its top is at row 16 - 6: press in the middle of it and pull
        // it 2 right and 1 down, which is towards the page's bottom row
        var inside = page.TranslatePoint(new Point((2 + 2) * zoom, (PageSize - 6 + 2) * zoom), window)!.Value;

        window.MouseDown(inside, MouseButton.Left);
        window.MouseMove(inside + new Point(zoom, 0));
        window.MouseMove(inside + new Point(2 * zoom, zoom));
        window.MouseUp(inside + new Point(2 * zoom, zoom), MouseButton.Left);
        Pump();

        var a = data.CharacterData['A' - ' '];
        Assert.Equal((4.0f, 5.0f, 4.0f, 5.0f), (a.PageUv.X, a.PageUv.Y, a.Size.X, a.Size.Y));
        Assert.Equal('A' - ' ', editor.SelectedIndex);
        document.Undo();
        Assert.Equal((2.0f, 6.0f), (a.PageUv.X, a.PageUv.Y));
        Assert.False(document.CanUndo);
        document.Redo();
        Assert.Equal((4.0f, 5.0f), (a.PageUv.X, a.PageUv.Y));
    }

    [AvaloniaFact]
    public void TheCornerResizesAndTheTableEditsTheBoxes()
    {
        var (font, data, _) = AddFont();
        var (document, editor, window) = Open(font);
        var page = window.GetVisualDescendants().OfType<FontPageEditor>().Single();
        var zoom = page.Zoom;
        // The corner handle sits at the box's bottom right as shown, (6, 16 - 6 + 5) on the page for the A; pulling it keeps the box's
        // top where it is, so its bottom row on the page moves
        var corner = page.TranslatePoint(new Point(6 * zoom, (PageSize - 6 + 5) * zoom), window)!.Value;

        window.MouseDown(corner, MouseButton.Left);
        window.MouseMove(corner + new Point(zoom, 2 * zoom));
        window.MouseUp(corner + new Point(zoom, 2 * zoom), MouseButton.Left);
        Pump();
        var a = data.CharacterData['A' - ' '];
        Assert.Equal((2.0f, 6.0f, 5.0f, 7.0f), (a.PageUv.X, a.PageUv.Y, a.Size.X, a.Size.Y));

        editor.Characters['A' - ' '].Page = 2;
        editor.Characters['A' - ' '].Left = 1;
        Pump();
        Assert.Equal((2, 1.0f), (a.FontPageSpecifier, a.PageUv.X));
        Assert.Equal(1, editor.SelectedPage!.Index);
        Assert.Contains(editor.Boxes, box => box.Index == 'A' - ' ');

        document.Undo();
        document.Undo();
        Assert.Equal((1, 2.0f), (a.FontPageSpecifier, a.PageUv.X));
        document.Undo();
        Assert.Equal((2.0f, 6.0f, 4.0f, 5.0f), (a.PageUv.X, a.PageUv.Y, a.Size.X, a.Size.Y));
    }

    // The game keeps its pages upside down, a box going up from its bottom row: the page's bottom row is the one shown on top
    [AvaloniaFact]
    public void PagesAreShownTheRightWayUp()
    {
        var (font, _, _) = AddFont();
        var (_, editor, window) = Open(font);
        var page = window.GetVisualDescendants().OfType<FontPageEditor>().Single();
        var zoom = page.Zoom;
        var box = editor.Boxes.Single(box => box.Index == 'A' - ' ');
        Assert.Equal(new Rect(2, PageSize - 6, 4, 5), page.GetShownRect(box));

        var topLeft = page.TranslatePoint(new Point(zoom / 2, zoom / 2), window)!.Value;
        var bottomLeft = page.TranslatePoint(new Point(zoom / 2, PageSize * zoom - zoom / 2), window)!.Value;
        var expected = (Top: PixelAt(0, 0, PageSize - 1), Bottom: PixelAt(0, 0, 0));
        // Polled like any view, a frame of the page shows a render tick after the one before it
        var shown = (Top: 0u, Bottom: 0u);
        for (var tries = 0; tries < 10 && shown != expected; tries++)
        {
            Render();
            var frame = window.CaptureRenderedFrame()!;
            shown = (PixelOf(frame, topLeft), PixelOf(frame, bottomLeft));
        }

        Assert.Equal(expected, shown);
    }

    private static UInt32 PixelOf(WriteableBitmap frame, Point point)
    {
        using var buffer = frame.Lock();
        return (UInt32)Marshal.ReadInt32(buffer.Address, (int)point.Y * buffer.RowBytes + (int)point.X * 4);
    }

    [AvaloniaFact]
    public void ReplacingAPageChangesItsTexture()
    {
        var (font, _, texture) = AddFont();
        var (_, editor, _) = Open(font);
        editor.SelectedPage = editor.Pages[0];

        Assert.False(editor.ReplacePage(new MemoryStream(PagePng(7, 12))));
        Assert.True(editor.ReplacePage(new MemoryStream(PagePng(7, 32))));

        var pixels = ((IAsset)texture).GetData<TextureData>();
        Assert.Equal(32, pixels.Bitmap!.PixelSize.Width);
        Assert.Equal(PixelAt(7, 3, 2), pixels.GetPixels()[2 * 32 + 3]);
        Assert.Equal(32, editor.Pages[0].Image!.PixelSize.Width);
    }

    [AvaloniaFact]
    public void CharactersGetAddedAndRemovedAtTheEnd()
    {
        var (font, data, _) = AddFont();
        var (document, editor, _) = Open(font);
        var count = data.CharacterData.Count;

        editor.AddCharacterCommand.Execute().Subscribe();
        Pump();
        Assert.Equal(count + 1, data.CharacterData.Count);
        Assert.Equal("[ (0x5B)", editor.Characters[^1].Caption);
        Assert.Equal(editor.Characters[^1], editor.SelectedCharacter);

        editor.RemoveLastCharacterCommand.Execute().Subscribe();
        Pump();
        Assert.Equal(count, data.CharacterData.Count);

        document.Undo();
        Assert.Equal(count + 1, data.CharacterData.Count);
        document.Undo();
        Assert.Equal(count, data.CharacterData.Count);
    }
}
