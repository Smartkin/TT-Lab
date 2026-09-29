using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Rendering.Objects;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.Instance;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Editor;

// A particle system's picture is a rectangle of one of the default chunk's texture pages, stored as its corners plus 2^19: the editor
// shows the pages and the game's pictures on them and takes pixels
[Collection(ProjectCollection.Name)]
public sealed class ParticleTextureEditorTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public ParticleTextureEditorTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    private static Vector2 At(float x, float y, float offset = ParticleTextureRect.GameOffset) => new() { X = x + offset, Y = y + offset };

    private static ParticleSystem System(string name, int page, Vector2 start, Vector2 end) => new() { Name = name, TexturePage = page, TextureStart = start, TextureEnd = end };

    [Fact]
    public void RectanglesAreThePagesPixels()
    {
        var rect = ParticleTextureRect.FromGame(At(7.44f, 70.3f), At(59.3f, 122.2f));
        Assert.Equal(new ParticleTextureRect(7, 70, 52, 52), rect);
        Assert.Equal(ParticleEmitter.TexturePixel(524295.44f), ParticleTextureRect.Pixel(524295.44f));
        // The tools wrote 2^18 into a few systems, the game adds 2^19 and keeps the low 10 bits
        Assert.Equal(new ParticleTextureRect(68, 104, 3, 15), ParticleTextureRect.FromGame(At(68.1f, 104.03f, 1 << 18), At(71.47f, 119.16f, 1 << 18)));

        // An end before the start mirrors the picture
        var mirrored = ParticleTextureRect.FromGame(At(53, 120), At(7, 72));
        Assert.Equal(new ParticleTextureRect(7, 72, 46, 48, true, true), mirrored);

        // Unchanged corners keep their values, fractions and offsets included; moved ones move by as many pixels
        var start = At(7.44f, 70.3f);
        var end = At(59.3f, 122.2f, 1 << 18);
        var (sameStart, sameEnd) = rect.ToGame(start, end);
        Assert.Equal((start.X, start.Y, end.X, end.Y), (sameStart.X, sameStart.Y, sameEnd.X, sameEnd.Y));
        var (movedStart, movedEnd) = (rect with { X = 10 }).ToGame(start, end);
        Assert.Equal(start.X + 3, movedStart.X);
        Assert.Equal(start.Y, movedStart.Y);
        Assert.Equal(new ParticleTextureRect(10, 70, 52, 52), ParticleTextureRect.FromGame(movedStart, movedEnd));
        Assert.Equal(end.X + 3, movedEnd.X);

        // A new system's zeros get the tools' offset
        var (newStart, newEnd) = new ParticleTextureRect(4, 8, 16, 24, MirrorX: true).ToGame(new Vector2(), new Vector2());
        Assert.Equal((20 + ParticleTextureRect.GameOffset, 8 + ParticleTextureRect.GameOffset), (newStart.X, newStart.Y));
        Assert.Equal((4 + ParticleTextureRect.GameOffset, 32 + ParticleTextureRect.GameOffset), (newEnd.X, newEnd.Y));
        Assert.Equal(new ParticleTextureRect(4, 8, 16, 24, true), ParticleTextureRect.FromGame(newStart, newEnd));

        Assert.Equal(new ParticleTextureRect(96, 0, 32, 1), new ParticleTextureRect(120, -5, 32, 0).Within(128, 128));
    }

    [Fact]
    public void TheBankHasThePicturesTheGamesSystemsUse()
    {
        var sprites = ParticleTextureBank.Build(
        [
            System("RAIN", 0, At(0, 0), At(31, 32)),
            System("PLANT", 0, At(0, 0), At(31, 32)),
            System("SPLASH", 0, At(1, 0), At(32, 31)),
            System("MIRRORED", 0, At(31, 32), At(0, 0)),
            System("CLOUD", 0, At(0, 64), At(64, 128)),
            System("RING", 1, At(0, 0), At(31, 32)),
            System("EMPTY", 1, At(5, 5), At(5, 9)),
        ]);

        Assert.Equal(3, sprites.Count);
        Assert.Equal((0, new ParticleTextureRect(0, 0, 31, 32)), (sprites[0].Page, sprites[0].Rect));
        Assert.Equal(["RAIN", "PLANT", "SPLASH", "MIRRORED"], sprites[0].UsedBy);
        Assert.Equal("Used by RAIN, PLANT, SPLASH and 1 more", sprites[0].Description);
        Assert.Equal((0, new ParticleTextureRect(0, 64, 64, 64)), (sprites[1].Page, sprites[1].Rect));
        Assert.Equal((1, "Used by RING"), (sprites[2].Page, sprites[2].Description));
    }

    private (DocumentViewModel Document, ParticleSystem System) OpenDefaults()
    {
        var pages = Enumerable.Range(0, 3).Select(i => _assets.AddTexture($"Page {i}", 0xFF000000u | (uint)(0x40 * (i + 1)))).ToList();
        var defaults = _project.Add(new DefaultParticles(), "Global Particles");
        var data = new DefaultParticleData(defaults)
        {
            TextureIDs = pages.Select(page => page.URI).ToList(),
            MaterialIDs = [],
            ParticleSystems = [System("FIRE", 0, At(2, 2), At(10, 12)), System("SMOKE", 0, At(0, 0), At(8, 8)), System("SPARK", 1, At(4, 4), At(12, 12))],
            ParticleInstances = [],
        };
        defaults.SetData(data);
        var document = new DocumentViewModel(defaults);
        document.Initialize();
        return (document, data.ParticleSystems[0]);
    }

    private static void Pump()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaFact]
    public void TheEditorShowsThePagesAndChangesThePictureAsOneStep()
    {
        var (document, system) = OpenDefaults();
        var node = document.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].TexturePage")!;
        var editor = Assert.IsType<ParticleTextureViewModel>(EditorDescRegistry.GetDesc(document, node).Construct());
        var window = new Window { Content = new ContentControl { Content = editor }, Width = 500, Height = 600 };
        window.Show();
        Pump();

        Assert.Equal(3, editor.Pages.Count);
        Assert.True(editor.Pages[0].IsSelected);
        Assert.NotNull(editor.Page);
        Assert.Equal(new ParticleTextureRect(2, 2, 8, 10), editor.Rect);
        // The page's pictures, the system's own among them
        Assert.Equal(2, editor.Sprites.Count);
        Assert.Equal("Used by FIRE", editor.Status);
        Assert.NotNull(editor.Preview);
        // The raw corners have no editors of their own
        Assert.False(EditorDescRegistry.GetDesc(document, document.PropertyGraph.Find("Root.AssetData.ParticleSystems[0].TextureStart")!).Construct().IsVisible);

        // Picking another picture changes both corners as one step
        editor.PickSprite(editor.Sprites.Single(sprite => sprite.UsedBy.Contains("SMOKE")));
        Assert.Equal(new ParticleTextureRect(0, 0, 8, 8), ParticleTextureRect.FromGame(system.TextureStart, system.TextureEnd));
        Assert.Equal(new ParticleTextureRect(0, 0, 8, 8), editor.Rect);
        document.Undo();
        Assert.Equal(new ParticleTextureRect(2, 2, 8, 10), ParticleTextureRect.FromGame(system.TextureStart, system.TextureEnd));
        Assert.Equal(new ParticleTextureRect(2, 2, 8, 10), editor.Rect);
        Assert.Equal("2", editor.X);

        // Typed pixels and the mirror toggles
        editor.Width = "6";
        Assert.Equal(new ParticleTextureRect(2, 2, 6, 10), editor.Rect);
        editor.MirrorX = true;
        Assert.True(system.TextureStart.X > system.TextureEnd.X);
        Assert.Equal(-1.0, editor.PreviewScaleX);

        // A drag is one step however many times it moves
        editor.BeginDrag();
        editor.DragRect(new ParticleTextureRect(3, 3, 6, 10, true));
        editor.DragRect(new ParticleTextureRect(5, 4, 6, 10, true));
        editor.EndDrag();
        Assert.Equal(new ParticleTextureRect(5, 4, 6, 10, true), editor.Rect);
        document.Undo();
        Assert.Equal(new ParticleTextureRect(2, 2, 6, 10, true), editor.Rect);

        // Another page, and back with undo
        editor.ChoosePage(1);
        Assert.Equal(1, system.TexturePage);
        Assert.True(editor.Pages[1].IsSelected);
        Assert.Equal("Used by SPARK", Assert.Single(editor.Sprites).Description);
        Assert.Equal("6×10 pixels at 2, 2", editor.Status);
        document.Undo();
        Assert.Equal(0, system.TexturePage);
        Assert.True(editor.Pages[0].IsSelected);
        window.Close();
    }

    [AvaloniaFact]
    public void DraggingOnThePageDrawsMovesAndPicks()
    {
        var page = new ParticleTexturePage { Rect = new ParticleTextureRect(8, 8, 8, 8), Zoom = 2.0, Sprites = [new ParticleSprite(0, new ParticleTextureRect(40, 40, 10, 10), ["RING"])] };
        var window = new Window { Content = page, Width = 400, Height = 400 };
        window.Show();
        Pump();
        var dragged = new List<ParticleTextureRect>();
        ParticleSprite? picked = null;
        var ended = 0;
        page.RectDragged += dragged.Add;
        page.SpritePicked += sprite => picked = sprite;
        page.DragEnded += () => ended++;
        Point OnPage(double x, double y) => page.TranslatePoint(new Point(x * 2, y * 2), window)!.Value;

        // Inside the rectangle moves it
        window.MouseDown(OnPage(10, 10), MouseButton.Left);
        window.MouseMove(OnPage(14, 12));
        window.MouseUp(OnPage(14, 12), MouseButton.Left);
        Assert.Equal(new ParticleTextureRect(12, 10, 8, 8), dragged[^1]);

        // Its bottom right corner resizes it from the top left one
        window.MouseDown(OnPage(16, 16), MouseButton.Left);
        window.MouseMove(OnPage(20, 30));
        window.MouseUp(OnPage(20, 30), MouseButton.Left);
        Assert.Equal(new ParticleTextureRect(8, 8, 12, 22), dragged[^1]);

        // Elsewhere draws a new one, whichever way the pointer goes
        window.MouseDown(OnPage(100, 100), MouseButton.Left);
        window.MouseMove(OnPage(90, 80));
        window.MouseUp(OnPage(90, 80), MouseButton.Left);
        Assert.Equal(new ParticleTextureRect(90, 80, 10, 20), dragged[^1]);
        Assert.Equal(3, ended);

        // One of the game's pictures is taken with a click
        window.MouseDown(OnPage(45, 45), MouseButton.Left);
        window.MouseUp(OnPage(45, 45), MouseButton.Left);
        Assert.Equal("RING", picked?.UsedBy[0]);
        Assert.Equal(3, ended);
        window.Close();
    }
}
