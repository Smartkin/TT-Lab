using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Graphics;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Tests.Projects;

// A project made on one system or in one folder builds the same opened on the other or in another
[Collection(ProjectCollection.Name)]
public sealed class MovedProjectTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    // A chunk's path has the separators of the system the project was made on: split by this system's alone, Play built a chunk of the
    // other's into Levels itself, where the game never looks for it
    [Theory]
    [InlineData("levels\\earth\\hub\\beach")]
    [InlineData("levels/earth/hub/beach")]
    public void ChunksOfEitherSystemAreBuiltIntoTheirLevelsFolders(string chunkPath)
    {
        Assert.Equal(["earth", "hub"], TT_Lab.Project.Project.LevelFoldersOf(chunkPath));
    }

    // Two parts of the same picture, the way a PSM has them
    private static List<ITwinPTC> ExportParts(TestProject project)
    {
        var parts = new List<PTC>();
        for (var i = 0; i < 2; i++)
        {
            var texture = project.Add(new Texture(), $"Icon texture {i}");
            texture.SetData(TextureData.FromPixels(texture, Enumerable.Repeat(0xFF336699u, 16 * 16).ToArray(), 16, 16));
            var material = project.Add(new Material(), $"Icon material {i}");
            material.SetData(new MaterialData(material) { Name = "Icon" });
            var part = project.Add(new PTC { GlobalPath = "Startup/PSM" }, $"Icon part {i}");
            part.SetData(new PTCData(part) { TextureID = texture.URI, MaterialID = material.URI });
            parts.Add(part);
        }

        return parts.Select(part => (ITwinPTC)part.Export(new PS2ItemFactory())).ToList();
    }

    // The parts' textures and materials get IDs of their own (the HUD's icons crashed the game with the same ones), which were salted
    // with their files' paths: built in another folder, or on the other system, every PSM, PTC and font had other IDs
    [AvaloniaFact]
    public void PicturesPartsHaveTheSameIdsWhereverTheProjectIs()
    {
        var here = ExportParts(_project);
        List<ITwinPTC> elsewhere;
        string otherFolder;
        using (var other = new TestProject())
        {
            otherFolder = other.Project.ProjectPath;
            elsewhere = ExportParts(other);
        }

        Assert.NotEqual(_project.Project.ProjectPath, otherFolder);
        Assert.Equal(here.Select(part => (part.TexID, part.MatID)), elsewhere.Select(part => (part.TexID, part.MatID)));
        Assert.NotEqual(here[0].TexID, here[1].TexID);
        Assert.NotEqual(here[0].MatID, here[1].MatID);
    }
}
