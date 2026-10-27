using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.Tests.Assets;

// A link's object matrix is its chunk matrix's inverse: the inspector keeps them so, what's set outside it needn't be (the viewport's menu
// placed links with their chunk matrix as their object matrix), and the build writes the inverse in place of an object matrix that isn't one
[Collection(ProjectCollection.Name)]
public sealed class ChunkLinkMatrixTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void TheBuildWritesTheChunkMatricesInverse()
    {
        var chunk = mat4.Translate(120.5f, -3, 48) * mat4.RotateY(0.7f);
        var link = new ChunkLink { ChunkMatrix = chunk.ToTwin(), ObjectMatrix = mat4.Translate(1, 2, 3).ToTwin() };

        var written = link.WrittenObjectMatrix(out var isOwn).ToGlm();

        Assert.False(isOwn);
        Assert.True((written * chunk - mat4.Identity).Values1D.All(value => MathF.Abs(value) < 1e-4f));
        // The game's pairs are inverses but for rounding: theirs are written
        var rounded = chunk.Inverse;
        rounded.m30 += 2e-5f;
        link.ObjectMatrix = rounded.ToTwin();
        Assert.Same(link.ObjectMatrix, link.WrittenObjectMatrix(out isOwn));
        Assert.True(isOwn);
    }

    // What TT Lab 1.0.0 and 1.1.0 placed from the viewport's menu builds right without the project changing: the link's file gets the inverse
    [Fact]
    public void ALinkPlacedWithItsChunkMatrixAsItsObjectMatrixBuildsWithTheInverse()
    {
        var beach = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach", package: _project.Project.Ps2Package);
        var cave = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/cave" }, "cave", package: _project.Project.Ps2Package);
        var links = _project.Add(new ChunkLinks { Chunk = beach.AdditionalPath!, AdditionalPath = beach.AdditionalPath }, "Links", package: _project.Project.Ps2Package);
        var at = mat4.Translate(30, 2, -12);
        var data = new ChunkLinksData(links) { Links = [new ChunkLink { Path = cave.URI, ObjectMatrix = at.ToTwin(), ChunkMatrix = at.ToTwin() }] };
        links.SetData(data);

        var written = Assert.Single(((ITwinLink)data.Export(new PS2ItemFactory())).LinksList);

        Assert.True((written.ObjectMatrix.ToGlm() - mat4.Translate(-30, -2, 12)).Values1D.All(value => MathF.Abs(value) < 1e-5f));
        Assert.Equal(at, written.ChunkMatrix.ToGlm());
        // The asset keeps what it has
        Assert.Equal(at, data.Links[0].ObjectMatrix.ToGlm());
    }
}
