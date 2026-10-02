using GlmSharp;
using Newtonsoft.Json;
using TT_Lab.AssetData.Instance;
using TT_Lab.Extensions;
using LoadWallCorners = TT_Lab.Rendering.Objects.LoadWallCorners;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Assets;

// A new chunk link's load wall is a 10 unit square standing on the link, so it shows and can be dragged; links read back without a wall
// (the game's links without one, copies, prefabs) keep none, the build writes none for them
public class ChunkLinkWallTests
{
    [Fact]
    public void NewLinksGetATenUnitWall()
    {
        var wall = new ChunkLink().LoadingWall;

        Assert.True(LoadWallCorners.IsUsable(wall));
        // Going around from the bottom left, facing +Z
        Assert.Equal(new mat4(new vec4(-5, 0, 0, 1), new vec4(5, 0, 0, 1), new vec4(5, 10, 0, 1), new vec4(-5, 10, 0, 1)), wall.ToGlm());
        var transform = new LoadWallCorners().ToTransform(wall);
        Assert.Equal((new vec3(0, 5, 0), 5.0f, 5.0f, new vec3(0, 0, 1)), (transform.Column3.xyz, transform.Column0.xyz.Length, transform.Column1.xyz.Length, transform.Column2.xyz));
        Assert.Equal(new vec4(1, 3, 3, 1), ChunkLink.WallAt(new vec3(6, 3, 3)).ToGlm().Column0);
    }

    [Fact]
    public void LinksReadBackWithoutAWallKeepNone()
    {
        var without = JsonConvert.SerializeObject(new ChunkLink { LoadingWall = new Matrix4() });
        var with = new ChunkLink { LoadingWall = ChunkLink.WallAt(new vec3(1, 2, 3)) };

        Assert.False(LoadWallCorners.IsUsable(JsonConvert.DeserializeObject<ChunkLink>(without)!.LoadingWall));
        Assert.False(LoadWallCorners.IsUsable(CloneUtils.DeepClone(new ChunkLink { LoadingWall = new Matrix4() }).LoadingWall));
        Assert.Equal(with.LoadingWall.ToGlm(), JsonConvert.DeserializeObject<ChunkLink>(JsonConvert.SerializeObject(with))!.LoadingWall.ToGlm());
    }
}
