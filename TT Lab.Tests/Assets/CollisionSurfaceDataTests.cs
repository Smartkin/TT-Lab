using Newtonsoft.Json;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Tests.Assets;

// A collision surface's sounds, particles and physics values by the contact kinds the game plays them by, and the files of
// projects made before they had those names
[Collection(ProjectCollection.Name)]
public sealed class CollisionSurfaceDataTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public CollisionSurfaceDataTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    [Fact]
    public void NamedValuesAreTheGamesFloats()
    {
        var surface = _project.Add(new CollisionSurface { Chunk = "default", LayoutID = ChunkLayouts.CollisionSurfaces }, "Wood");
        var data = new CollisionSurfaceData(surface) { Friction = 0.7f, ImpactSoundVolume = 0.8f, StepSoundVolume = 0.75f, HardImpactParticleSystemId = 129 };

        Assert.Equal(0.7f, data.PhysicsParameters[SurfacePhysics.Friction]);
        Assert.Equal(0.8f, data.PhysicsParameters[SurfacePhysics.ImpactSoundVolume]);
        Assert.Equal(0.75f, data.PhysicsParameters[SurfacePhysics.StepSoundVolume]);
        Assert.Equal(-1f, data.LandSoundVolume);
        Assert.Equal(1000000f, data.UnreadValue1);
        Assert.True(data.CollisionMask.HasFlag(Enums.SurfaceCollisionFlags.SolidToPlayer | Enums.SurfaceCollisionFlags.SolidToObjects));

        surface.SetData(data);
        var bytes = _assets.Export(surface);

        Assert.Equal(114, bytes.Length);
        Assert.Equal(0.7f, BitConverter.ToSingle(bytes, 26 + SurfacePhysics.Friction * 4));
        Assert.Equal(129, BitConverter.ToUInt16(bytes, 6 + 3 * 2));
    }
}
