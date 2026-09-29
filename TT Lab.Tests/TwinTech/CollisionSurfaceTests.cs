using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Tests.TwinTech;

// A collision surface's record as the PAL executable reads it: the flags, the ID, the sounds and particles of the contact kinds
// in the file's order, then the volumes and the friction among the 10 floats
public class CollisionSurfaceTests
{
    [Fact]
    public void SurfacesComeBackFromTheirBytes()
    {
        var surface = new PS2AnyCollisionSurface
        {
            CollisionMask = Enums.SurfaceCollisionFlags.SolidToObjects | Enums.SurfaceCollisionFlags.SolidToPlayer | (Enums.SurfaceCollisionFlags)0xFF000,
            SurfaceId = Enums.SurfaceType.SURF_NORMAL_WOOD, StepSoundId1 = 0x9E, StepSoundId2 = 0x9F, ImpactSoundId = 0x9D, LandSoundId = 0x9D,
            HardImpactSoundId = 0x12, ScrapeSoundId = 0xB5, ImpactParticleSystemId = 129, HardImpactParticleSystemId = 0xFFFF, StepParticleSystemId = 77,
            UnusedVector = new Vector4(0, 0, 0, 1), ContactMessage = [new Vector4(0, 0, 0, 0), new Vector4(0, 0, 0, 0)]
        };
        surface.PhysicsParameters[SurfacePhysics.ImpactSoundVolume] = 0.8f;
        surface.PhysicsParameters[SurfacePhysics.Friction] = 0.7f;
        surface.PhysicsParameters[SurfacePhysics.Unread1] = 1000000;

        var bytes = Write(surface);
        var read = new PS2AnyCollisionSurface();
        using (var reader = new BinaryReader(new MemoryStream(bytes)))
        {
            read.Read(reader, bytes.Length);
        }

        Assert.Equal(surface.GetLength(), bytes.Length);
        Assert.Equal(bytes, Write(read));
        // The order the game reads the IDs in: step 1, step 2, impact particles, hard impact particles, impact, hard impact, step
        // particles, land, scrape and one it skips
        Assert.Equal(new ushort[] { 0x9E, 0x9F, 129, 0xFFFF, 0x9D, 0x12, 77, 0x9D, 0xB5, 0xFFFF }, Enumerable.Range(0, 10).Select(i => BitConverter.ToUInt16(bytes, 6 + i * 2)));
        Assert.Equal(0.8f, BitConverter.ToSingle(bytes, 26 + SurfacePhysics.ImpactSoundVolume * 4));
        Assert.Equal(0.7f, BitConverter.ToSingle(bytes, 26 + SurfacePhysics.Friction * 4));
        Assert.Equal((0x9Du, 0x12u, 0xB5u, 0.7f), ((uint)read.ImpactSoundId, (uint)read.HardImpactSoundId, (uint)read.ScrapeSoundId, read.PhysicsParameters[SurfacePhysics.Friction]));
    }

    private static byte[] Write(PS2AnyCollisionSurface surface)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        surface.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }
}
