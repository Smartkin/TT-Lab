using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

namespace TT_Lab.Tests.Editor;

// A PSM's editor shows its parts put together the way the game shows them, the right way up, and replaces the whole picture or a part
[Collection(ProjectCollection.Name)]
public sealed class PsmEditorTests : IDisposable
{
    // Small stand-ins for the game's tiles of 128x256
    private const int TileWidth = 4;
    private const int TileHeight = 8;

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static uint Pixel(int part, int column, int row) => 0xFF000000 | (uint)(part << 16) | (uint)(column << 8) | (uint)row;

    // Every part's pixels the way the game keeps them: row 0 is the bottom of what's shown
    private (PSM Psm, List<Texture> Textures) AddPsm(string name, params (int Width, int Height)[] sizes)
    {
        var textures = new List<Texture>();
        var parts = new List<LabURI>();
        for (var i = 0; i < sizes.Length; i++)
        {
            var (width, height) = sizes[i];
            var part = i;
            var texture = _project.Add(new Texture(), $"{name} texture {i}");
            texture.SetData(TextureData.FromPixels(texture, Enumerable.Range(0, width * height).Select(pixel => Pixel(part, pixel % width, pixel / width)).ToArray(), width, height));
            var material = _project.Add(new Material(), $"{name} material {i}");
            material.SetData(new MaterialData(material) { Name = $"{name}_{i + 1:00}" });
            var ptc = _project.Add(new PTC { GlobalPath = "Language/Loading" }, $"{name} part {i}");
            ptc.SetData(new PTCData(ptc) { TextureID = texture.URI, MaterialID = material.URI });
            textures.Add(texture);
            parts.Add(ptc.URI);
        }

        var psm = _project.Add(new PSM { GlobalPath = "Language/Loading" }, name);
        psm.SetData(new PSMData(psm) { PTCs = parts });
        return (psm, textures);
    }

    private (PSM Psm, List<Texture> Textures) AddPicture(string name = "Loading1") => AddPsm(name, Enumerable.Repeat((TileWidth, TileHeight), 8).ToArray());

    private static PsmEditorViewModel Open(PSM psm, out DocumentViewModel document)
    {
        document = new DocumentViewModel(psm);
        document.Initialize();
        var editor = Assert.IsType<PsmEditorViewModel>(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.PTCs")!).Construct());
        editor.Activator.Activate();
        return editor;
    }

    private static uint[] PixelsOf(Texture texture) => ((IAsset)texture).GetData<TextureData>().GetPixels();

    private static MemoryStream Png(int width, int height, Func<int, int, uint> pixel) =>
        new(TextureData.EncodePng(Enumerable.Range(0, width * height).Select(index => pixel(index % width, index / width)).ToArray(), width, height));

    [AvaloniaFact]
    public void TheTilesMakeOnePictureTheRightWayUp()
    {
        var (psm, _) = AddPicture();

        var editor = Open(psm, out _);

        Assert.True(editor.IsPicture);
        Assert.Equal((16, 16), (editor.Layout.Width, editor.Layout.Height));
        Assert.Equal("6. Loading1_06 (4x8)", editor.Parts[5].Caption);
        var picture = TextureData.DecodePng(new MemoryStream(editor.GetPicturePng()));
        // Tile 5 is the second of the second row, the bottom of what it shows is its row 0
        Assert.Equal(Pixel(5, 1, 0), picture.Pixels[(TileHeight + TileHeight - 1) * 16 + TileWidth + 1]);
        Assert.Equal(Pixel(5, 1, TileHeight - 1), picture.Pixels[TileHeight * 16 + TileWidth + 1]);
    }

    [AvaloniaFact]
    public void ReplacingThePictureGivesEveryTileItsPart()
    {
        var (psm, textures) = AddPicture();
        var editor = Open(psm, out var document);

        Assert.True(editor.ReplacePicture(Png(16, 16, (column, row) => 0xFF000000 | (uint)(column << 8) | (uint)row)));

        // The picture's top left pixel is the last row of the first tile, the game keeps the tiles upside down
        Assert.Equal(0xFF000000u, PixelsOf(textures[0])[(TileHeight - 1) * TileWidth]);
        Assert.Equal(0xFF000000u | (uint)((TileWidth + 3) << 8) | (uint)(TileHeight + 0), PixelsOf(textures[5])[(TileHeight - 1) * TileWidth + 3]);
        Assert.Equal(0xFF000000u | (5 << 8) | 9u, TextureData.DecodePng(new MemoryStream(editor.GetPicturePng())).Pixels[9 * 16 + 5]);
        // The textures' data, not the document's: nothing to undo
        Assert.False(document.CanUndo);

        // An image of another size is resized to the picture
        Assert.True(editor.ReplacePicture(Png(40, 24, (_, _) => 0xFF336699)));
        Assert.All(textures, texture => Assert.All(PixelsOf(texture), pixel => Assert.Equal(0xFF336699u, pixel)));
    }

    [AvaloniaFact]
    public void APartIsReplacedOnItsOwn()
    {
        var (psm, textures) = AddPicture();
        var editor = Open(psm, out _);
        Assert.False(editor.ReplacePart(Png(TileWidth, TileHeight, (_, _) => 0xFFFFFFFF)));

        editor.SelectedIndex = 2;
        Assert.True(editor.ReplacePart(Png(TileWidth, TileHeight, (column, row) => 0xFF000000 | (uint)(column << 8) | (uint)row)));

        // Shown the right way up, kept upside down
        Assert.Equal(0xFF000000u | (TileHeight - 1), PixelsOf(textures[2])[0]);
        Assert.Equal(Pixel(3, 0, 0), PixelsOf(textures[3])[0]);
        var part = TextureData.DecodePng(new MemoryStream(editor.GetPartPng()!));
        Assert.Equal((TileWidth, TileHeight), (part.Width, part.Height));
        Assert.Equal(0xFF000000u | (1 << 8) | 2u, part.Pixels[2 * TileWidth + 1]);
    }

    [AvaloniaFact]
    public void TheIconsAreOnlyReplacedOneAtATime()
    {
        var (psm, textures) = AddPsm("Icons", (8, 8), (8, 8), (4, 8));
        var editor = Open(psm, out _);

        Assert.False(editor.IsPicture);
        Assert.False(editor.ReplacePicture(Png(32, 16, (_, _) => 0xFFFFFFFF)));
        Assert.Equal(Pixel(0, 0, 0), PixelsOf(textures[0])[0]);

        editor.SelectedIndex = 2;
        Assert.True(editor.ReplacePart(Png(8, 16, (_, _) => 0xFF123456)));
        Assert.Equal(4 * 8, PixelsOf(textures[2]).Length);
        Assert.All(PixelsOf(textures[2]), pixel => Assert.Equal(0xFF123456u, pixel));
    }

    [AvaloniaFact]
    public void TheEditorShowsThePictureInTheDocument()
    {
        var (psm, _) = AddPicture();
        var document = new DocumentViewModel(psm);
        document.Initialize();
        var window = new Window { Content = new DocumentScrollViewer { Content = document.Root }, Width = 900, Height = 900 };
        window.Show();
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var picture = window.GetVisualDescendants().OfType<PsmPicture>().Single();
        Assert.NotNull(picture.Picture);
        Assert.Equal(8, picture.Parts!.Count);
        Assert.Single(window.GetVisualDescendants().OfType<PsmEditorView>());
        Assert.Equal(5, picture.PartAt(new Avalonia.Point(TileWidth * 1.5 * picture.Zoom, TileHeight * 1.5 * picture.Zoom)));
        Assert.NotNull(window.CaptureRenderedFrame());
        window.Close();
    }
}
