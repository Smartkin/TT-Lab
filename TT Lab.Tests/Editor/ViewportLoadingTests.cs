using Avalonia;
using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Editor;

// A scene says what it's loading over the viewport in the game's font until it renders: the assets its document reads, then the
// resources the scene gets built from with how far along it is
[Collection(ProjectCollection.Name)]
public sealed class ViewportLoadingTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 250 && !condition(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }

    [AvaloniaFact]
    public async Task TheSceneShowsWhatItReadsAndThenWhatItBuilds()
    {
        var chunk = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach", package: _project.Project.Ps2Package);
        foreach (var name in new[] { "Start", "End" })
        {
            var position = _project.Add(new Position { Chunk = chunk.AdditionalPath!, AdditionalPath = chunk.AdditionalPath, LayoutID = 0 }, name);
            position.SetData(new PositionData(position) { Coords = new Vector3(1, 2, 3) });
            chunk.ChunkResources.Add(position.URI);
        }

        var viewport = new ViewportViewModel();
        // Tabs make their documents on the task pool
        await Task.Run(() => new DocumentViewModel(chunk, viewport));

        await WaitUntil(() => viewport.LoadingDetail.EndsWith("(2)"));
        Assert.Equal(ViewportViewModel.ReadingStage, viewport.LoadingStage);
        Assert.False(viewport.IsLoadingProgressKnown);

        await Task.Run(() => viewport.ReportLoading(ViewportViewModel.BuildingStage, "Scenery (3 of 10)", 2, 10));
        await WaitUntil(() => viewport.LoadingStage == ViewportViewModel.BuildingStage);
        Assert.Equal("Scenery (3 of 10)", viewport.LoadingDetail);
        Assert.True(viewport.IsLoadingProgressKnown);
        Assert.Equal(0.2, viewport.LoadingProgress, 6);
    }

    [AvaloniaFact]
    public void GameTextDrawsTheFontsGlyphsAsTallAsTheLine()
    {
        var glyphs = PsfGlyphs.FromGlyphs([new PsfGlyphs.Glyph('A', null, new Size(20, 40)), new PsfGlyphs.Glyph('[', null, new Size(30, 40)),
            new PsfGlyphs.Glyph('E', null, new Size(36, 40)), new PsfGlyphs.Glyph('e', null, new Size(16, 40))], 40);
        var text = new GameText { Glyphs = glyphs, LineHeight = 20, Text = "AZ[E" };

        var layout = text.Layout();

        Assert.Equal(('A', 10.0, true), layout[0]);
        // What the font lacks, and what it draws as a controller button, is the application's font
        Assert.False(layout[1].IsGlyph);
        Assert.True(layout[1].Width > 0);
        Assert.False(layout[2].IsGlyph);
        // A capital letter the font draws as a button is its small capital
        Assert.Equal(('E', 8.0, true), layout[3]);
        text.Measure(new Size(1000, 100));
        Assert.Equal(20, text.DesiredSize.Height);
        // Layout rounding takes it to whole pixels
        var width = layout.Sum(piece => piece.Width);
        Assert.InRange(text.DesiredSize.Width, width, width + 1);

        // A line too long gets smaller
        text.Measure(new Size(5, 100));
        Assert.Equal(5, text.DesiredSize.Width, 6);
        Assert.True(text.DesiredSize.Height < 20);
    }
}
