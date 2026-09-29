using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using SkiaSharp;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Global;
using TT_Lab.Views.Editors.Global;
using Twinsanity.TwinsanityInterchange.Common;
using Vector2 = Twinsanity.TwinsanityInterchange.Common.Vector2;

namespace TT_Lab.Tests.Editor;

// The game's texts are shown with its fonts, which draw some characters as controller buttons
[Collection(ProjectCollection.Name)]
public sealed class TextFileEditorTests : IDisposable
{
    private const int PageSize = 8;

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // Every pixel tells where it is: its page in the blue channel, its column and row in the red and green ones
    private static UInt32 PixelAt(int page, int column, int row) => 0xFF000000 | (UInt32)(column << 16) | (UInt32)(row << 8) | (UInt32)page;

    private PTC AddPage(string name, int page)
    {
        var pixels = new UInt32[PageSize * PageSize];
        for (var row = 0; row < PageSize; row++)
        {
            for (var column = 0; column < PageSize; column++)
            {
                pixels[row * PageSize + column] = PixelAt(page, column, row);
            }
        }

        var texture = _project.Add(new Texture(), $"{name} texture");
        texture.SetData(TextureData.FromPng(texture, new MemoryStream(EncodePng(pixels))));
        var ptc = _project.Add(new PTC { GlobalPath = "Startup/Fonts" }, name);
        ptc.SetData(new PTCData(ptc) { TextureID = texture.URI });
        return ptc;
    }

    private static byte[] EncodePng(UInt32[] argb)
    {
        var handle = GCHandle.Alloc(argb, GCHandleType.Pinned);
        try
        {
            using var bitmap = new SKBitmap();
            bitmap.InstallPixels(new SKImageInfo(PageSize, PageSize, SKColorType.Bgra8888, SKAlphaType.Unpremul), handle.AddrOfPinnedObject());
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }

    private static VectorCharacterData Character(float left, float bottom, float width, float height, byte page) => new()
    {
        PageUv = new Vector2 { X = left, Y = bottom },
        Size = new Vector2 { X = width, Y = height },
        FontPageSpecifier = page,
    };

    // Starts at the space, 'A' is on the first page and '[' on the second one like the game's buttons
    private Font AddFont(string name, bool withButtons, int extraCharacters = 0, string symbols = "")
    {
        var characters = Enumerable.Range(0, '¦' - ' ' + 1).Select(_ => new VectorCharacterData()).ToList();
        characters[0] = Character(0, 8, 3, 4, 0);
        characters['A' - ' '] = Character(1, 5, 2, 3, 1);
        characters['Z' - ' '] = Character(5, 3, 2, 2, 0);
        if (withButtons)
        {
            characters['[' - ' '] = Character(4, 8, 3, 2, 2);
        }

        for (var i = 0; i < extraCharacters; i++)
        {
            characters['F' - ' ' + i] = Character(0, 2, 1, 1, 1);
        }

        foreach (var symbol in symbols)
        {
            characters[symbol - ' '] = Character(2, 2, 1, 1, 1);
        }

        var font = _project.Add(new Font { GlobalPath = "Startup/Fonts" }, name);
        font.SetData(new FontData(font) { FontPages = [AddPage($"{name} page 0", 0).URI, AddPage($"{name} page 1", 1).URI], CharacterData = characters, SpaceIdentifier = 32 });
        return font;
    }

    private static UInt32[] Pixels(Bitmap bitmap)
    {
        var pixels = new UInt32[bitmap.PixelSize.Width * bitmap.PixelSize.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), handle.AddrOfPinnedObject(), pixels.Length * 4, bitmap.PixelSize.Width * 4);
        }
        finally
        {
            handle.Free();
        }

        return pixels;
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    // A glyph's box goes up from its bottom left corner, the page has it upside down
    [AvaloniaFact]
    public void GlyphsAreTheirBoxesTurnedUpright()
    {
        var font = AddFont("Crash", withButtons: true);

        var glyphs = PsfGlyphs.FromFont((FontData)font.GetData());

        Assert.True(glyphs.TryGetGlyph('A', out var a));
        Assert.Equal(new Size(2, 3), a.Size);
        Assert.Equal([PixelAt(0, 1, 4), PixelAt(0, 2, 4), PixelAt(0, 1, 3), PixelAt(0, 2, 3), PixelAt(0, 1, 2), PixelAt(0, 2, 2)], Pixels(a.Image!));
        Assert.True(glyphs.TryGetGlyph('[', out var button));
        Assert.Equal([PixelAt(1, 4, 7), PixelAt(1, 5, 7), PixelAt(1, 6, 7), PixelAt(1, 4, 6), PixelAt(1, 5, 6), PixelAt(1, 6, 6)], Pixels(button.Image!));
        // The Xbox's fonts give the first page's characters 0 as well
        Assert.True(glyphs.TryGetGlyph('Z', out var z));
        Assert.Equal(PixelAt(0, 5, 2), Pixels(z.Image!)[0]);
    }

    [AvaloniaFact]
    public void TheSpaceTakesRoomAndCharactersWithoutASizeHaveNoGlyph()
    {
        var glyphs = PsfGlyphs.FromFont((FontData)AddFont("Crash", withButtons: true).GetData());

        Assert.True(glyphs.TryGetGlyph(' ', out var space));
        Assert.Equal(new Size(3, 4), space.Size);
        Assert.False(glyphs.TryGetGlyph('B', out _));
        Assert.False(glyphs.TryGetGlyph('~', out _));
    }

    private (TextFileEditorViewModel Editor, TextFileEditorView View, TextFileData Data) Open(string text) => Open(text, out _);

    private (TextFileEditorViewModel Editor, TextFileEditorView View, TextFileData Data) Open(string text, out DocumentViewModel document)
    {
        var file = _project.Add(new TextFile { GlobalPath = "Language/Code" }, "English");
        var data = new TextFileData(file, text);
        file.SetData(data);
        document = new DocumentViewModel(file);
        document.Initialize();
        var editor = Assert.IsType<TextFileEditorViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.Text")!).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 900, Height = 800 };
        window.Show();
        Pump();
        return (editor, window.GetVisualDescendants().OfType<TextFileEditorView>().Single(), data);
    }

    // The fonts draw symbols a keyboard has no key for, every one of them is on the toolbar after the controller buttons
    [AvaloniaFact]
    public void ToolbarHasEveryButtonAndSymbolGlyph()
    {
        AddFont("Crash", withButtons: true, symbols: "%!¦");
        var (editor, _, _) = Open("go");

        Assert.Equal(['[', '¦', '!', '%'], editor.Buttons.Select(button => button.Character));
    }

    // The document's history is the editor's undo, typing at one place is one step and the text stays where it is
    [AvaloniaFact]
    public void UndoAndRedoGoThroughTheDocumentsHistory()
    {
        AddFont("Crash", withButtons: true);
        var (_, view, data) = Open("go", out var document);
        view.Editor.CaretOffset = 2;
        view.Editor.TextArea.PerformTextInput("o");
        view.Editor.TextArea.PerformTextInput("d");
        Pump();
        Assert.Equal("good", data.Text);
        view.Editor.CaretOffset = 0;
        Pump();
        view.Editor.TextArea.PerformTextInput("so ");
        Pump();
        Assert.Equal("so good", data.Text);

        document.Undo();
        Pump();
        Assert.Equal("good", data.Text);
        Assert.Equal("good", view.Editor.Text);
        document.Undo();
        Pump();
        Assert.Equal("go", view.Editor.Text);
        Assert.Equal(2, view.Editor.CaretOffset);

        document.Redo();
        Pump();
        Assert.Equal("good", view.Editor.Text);
        Assert.Equal("good", data.Text);
    }

    [AvaloniaFact]
    public void TextsAreShownWithTheFontWithTheMostCharacters()
    {
        AddFont("Arial", withButtons: false);
        AddFont("Crash", withButtons: true, extraCharacters: 3);

        var (editor, view, _) = Open("press [ to spin~go");

        Assert.Equal(["Plain text", "Arial", "Crash"], editor.Fonts.Select(font => font.Name));
        Assert.Equal("Crash", editor.SelectedFont!.Name);
        Assert.Contains(view.Editor.TextArea.TextView.ElementGenerators, generator => generator is PsfGlyphGenerator);
        Assert.Equal(['['], editor.Buttons.Select(button => button.Character));

        editor.SelectedFont = editor.Fonts[0];
        Pump();

        Assert.DoesNotContain(view.Editor.TextArea.TextView.ElementGenerators, generator => generator is PsfGlyphGenerator);
        Assert.Empty(editor.Buttons);
    }

    [AvaloniaFact]
    public void ToolbarButtonsInsertTheirCharacterAtTheCaret()
    {
        AddFont("Crash", withButtons: true);
        var (_, view, data) = Open("press  to spin");
        view.Editor.CaretOffset = "press ".Length;

        var button = view.GetVisualDescendants().OfType<Button>().Single(button => button.Tag is '[');
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        view.GetVisualDescendants().OfType<Button>().Single(button => "~".Equals(button.Tag)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump();

        Assert.Equal("press [~ to spin", view.Editor.Text);
        Assert.Equal("press [~ to spin", data.Text);
    }

    // The game's text files are Latin-1
    [AvaloniaFact]
    public void CharactersTheFilesCantHoldCantBeTyped()
    {
        AddFont("Crash", withButtons: true);
        var (_, view, _) = Open("go");
        view.Editor.CaretOffset = 2;

        view.Editor.TextArea.PerformTextInput("€");
        view.Editor.TextArea.PerformTextInput("¦");
        Pump();

        Assert.Equal("go¦", view.Editor.Text);
    }
}
