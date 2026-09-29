using GlmSharp;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Particles;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.Tests.Rendering;

public sealed class ParticleSimulationTests
{
    private static Vector2[] Curve(params (float Time, float Value)[] keys)
    {
        var curve = Enumerable.Range(0, ParticleCurves.MaxKeys).Select(_ => new Vector2()).ToArray();
        for (var i = 0; i < keys.Length; i++)
        {
            curve[i] = new Vector2 { X = keys[i].Time, Y = keys[i].Value };
        }

        return curve;
    }

    private static ParticleSystem System(Int16 genRate = 10, float lifeTime = 1.0f) => new()
    {
        GenRate = genRate,
        ParticleLifeTime = lifeTime,
        EmitterOverTime = 1,
        ColorGradients = Enumerable.Range(0, ParticleCurves.MaxKeys).Select(_ => new Vector4()).ToArray(),
        AlphaGradient = Curve((0, 128), (1, 0)),
        SizeWidth = Curve((0, 10000), (1, 10000)),
        SizeHeight = Curve((0, 10000), (1, 10000)),
        Rotation = Curve((0, 0), (1, 0)),
    };

    private static void Run(ParticleSimulation simulation, int frames)
    {
        for (var frame = 0; frame < frames; frame++)
        {
            simulation.Update(ParticleSimulation.FrameTime);
        }
    }

    // The game lines an emitter's cycle up with the frame counter when it makes it, an edited timing lines it up again with the
    // particles already out left as they are
    [Fact]
    public void EditingTheTimingLinesTheCycleUpAgain()
    {
        var system = System(genRate: 1, lifeTime: 100);
        system.EmitterOffTime = 9;
        var simulation = new ParticleSimulation { System = system };
        Run(simulation, 40);
        var made = simulation.Particles.Count;
        Assert.Equal(4, made);

        simulation.TimingOffset = 3;
        Run(simulation, 40);

        Assert.True(simulation.Particles.Count > made);
        var phases = simulation.Particles.Skip(made).Select(particle => (int)MathF.Round(particle.SpawnTime / ParticleSimulation.FrameTime) % 10).Distinct();
        Assert.Equal([3], phases);
    }

    // The game samples a curve between the first pair of keys enclosing the time, the keys after the first at 1 are leftovers
    [Fact]
    public void CurvesEndAtTheirFirstKeyAtTheEndOfTheLife()
    {
        // The game's CRATE_BREAK alpha: jumps to full right away and fades
        var alpha = Curve((0, 0), (0, 255), (1, 0), (1, 0), (1, 0));
        alpha[5] = new Vector2 { X = 0.5f, Y = 999 };

        Assert.Equal(3, ParticleCurves.CountKeys(alpha));
        Assert.Equal(0, ParticleCurves.Evaluate(alpha, 0.0f), 1);
        Assert.Equal(255, ParticleCurves.Evaluate(alpha, 0.0001f), 1);
        Assert.Equal(127.5f, ParticleCurves.Evaluate(alpha, 0.5f), 1);
        Assert.Equal(0, ParticleCurves.Evaluate(alpha, 1.0f), 1);
        // Nothing encloses a time before the first key
        Assert.Equal(0, ParticleCurves.Evaluate(Curve((0.5f, 100), (1, 100)), 0.25f), 1);
    }

    [Fact]
    public void ColorKeysHoldTheTimeFirst()
    {
        var colors = Enumerable.Range(0, ParticleCurves.MaxKeys).Select(_ => new Vector4()).ToArray();
        colors[0] = new Vector4(0, 255, 0, 128);
        colors[1] = new Vector4(1, 0, 255, 128);

        var color = ParticleCurves.EvaluateColor(colors, 0.25f);

        Assert.Equal(191.25f, color.x, 2);
        Assert.Equal(63.75f, color.y, 2);
        Assert.Equal(128f, color.z, 2);
    }

    // The rate is per frame, negative rates make one particle every that many frames, and the ring holds as many as the game's count
    [Fact]
    public void ParticlesAreMadeAtTheRatePerFrame()
    {
        var simulation = new ParticleSimulation { System = System(genRate: 3, lifeTime: 1.0f) };
        Run(simulation, 10);
        Assert.Equal(30, simulation.Particles.Count);
        Run(simulation, 60);
        Assert.Equal(180, simulation.Particles.Count);

        var sparse = new ParticleSimulation { System = System(genRate: -4, lifeTime: 1.0f) };
        Run(sparse, 60);
        Assert.Equal(15, sparse.Particles.Count);

        var none = new ParticleSimulation { System = System(genRate: 0, lifeTime: 1.0f) };
        Run(none, 60);
        Assert.Empty(none.Particles);
    }

    [Fact]
    public void ParticlesGoAtTheEndOfTheirLife()
    {
        var simulation = new ParticleSimulation { System = System(genRate: 1, lifeTime: 0.5f) };

        Run(simulation, 180);

        Assert.Equal(30, simulation.Particles.Count);
        Assert.All(simulation.Particles, particle => Assert.True(simulation.Time - particle.SpawnTime < 0.5f));
    }

    // The game's count: what one lifetime of frames makes with the on and off times, times the particle and its ghosts
    [Fact]
    public void TheMostParticlesIsWhatALifetimeMakes()
    {
        Assert.Equal(60, TwinParticleSystem.ComputeMaxParticleCount(1, 1.0f, 1, 0, 0, 0));
        Assert.Equal(30, TwinParticleSystem.ComputeMaxParticleCount(-2, 1.0f, 1, 0, 0, 0));
        Assert.Equal(600, TwinParticleSystem.ComputeMaxParticleCount(10, 10.0f, 6, 0, 54, 0));
        Assert.Equal(1, TwinParticleSystem.ComputeMaxParticleCount(0, 1.0f, 8, 0, 120, 0));
        Assert.Equal(6, TwinParticleSystem.ComputeMaxParticleCount(1, 0.05f, 1, 0, 0, 1));
    }

    [Fact]
    public void EmittersWithAnOffTimeTakeBreaks()
    {
        var system = System(genRate: 10, lifeTime: 10.0f);
        system.EmitterOverTime = 6;
        system.EmitterOffTime = 54;
        var simulation = new ParticleSimulation { System = system };

        Run(simulation, 60);

        // On for 6 of every 60 frames at 10 particles a frame
        Assert.Equal(60, simulation.Particles.Count);
        Run(simulation, 60);
        Assert.Equal(120, simulation.Particles.Count);
    }

    // The game's drips and splashes have negative gravity, fire and steam positive: a particle is at start + velocity t + gravity t²
    [Fact]
    public void ParticlesFlyTheEmittersWayAndFallWithGravitySquared()
    {
        var system = System(genRate: 1, lifeTime: 10.0f);
        system.Velocity = 2.0f;
        system.Gravity = -1.0f;
        // A quarter turn about Z tilts the up towards -X
        var simulation = new ParticleSimulation { System = system, EmitRotation = ParticleSimulation.Rotation(16384, 0) };

        Run(simulation, 60);

        var particle = simulation.Particles[0];
        var age = simulation.Time - particle.SpawnTime;
        var position = simulation.PositionAt(particle, simulation.Time);
        Assert.Equal(-2.0f * age, position.x, 2);
        Assert.Equal(-age * age, position.y, 2);
        Assert.Equal(0.0f, position.z, 3);
    }

    // The emitter's tilt turns about Z, then its yaw about Y and its roll about X
    [Fact]
    public void RotationsApplyTiltThenYawThenRoll()
    {
        var tiltOnly = ParticleSimulation.Rotation(16384, 0) * vec3.UnitY;
        Assert.Equal(-1.0f, tiltOnly.x, 4);
        Assert.Equal(0.0f, tiltOnly.y, 4);

        var tilted = ParticleSimulation.Rotation(16384, 16384) * vec3.UnitY;
        Assert.Equal(0.0f, tilted.x, 4);
        Assert.Equal(1.0f, tilted.z, 4);

        var rolled = ParticleSimulation.Rotation(0, 0, 16384) * vec3.UnitY;
        Assert.Equal(1.0f, rolled.z, 4);
    }

    // Rings of dust around Crash and charges gathering in: the random values are angles and how far out they start
    [Fact]
    public void RadialSystemsMakeParticlesOnARing()
    {
        var system = System(genRate: 50, lifeTime: 10.0f);
        system.GenSort = TwinParticleSystem.GenSortType.Radial;
        system.Velocity = -3.0f;
        // The game's IMPACT_RADIAL1A: tilted a quarter turn from the up, all the way around it
        system.RandomEmit = new Vector3(0, 32768, 0);
        system.RandomStart = new Vector3(2, 32768, -16384);
        var simulation = new ParticleSimulation { System = system };

        simulation.Update(ParticleSimulation.FrameTime);

        Assert.NotEmpty(simulation.Particles);
        Assert.All(simulation.Particles, particle =>
        {
            Assert.Equal(0.0f, particle.Velocity.y, 3);
            Assert.Equal(3.0f, particle.Velocity.Length, 3);
            // Flying in towards the emitter from 2 units away
            Assert.True(vec3.Dot(particle.Start, particle.Velocity) < 0.0f);
            Assert.Equal(2.0f, particle.Start.Length, 3);
        });

        // Without a tilt every particle starts on the up. The sphere sort spreads them evenly over the area, a quarter turn of
        // tilt either way covering the dome (the game's TELEPORT2)
        system.RandomStart = new Vector3(2, 0, 0);
        var column = new ParticleSimulation { System = system };
        column.Update(ParticleSimulation.FrameTime);
        Assert.All(column.Particles, particle => Assert.Equal(2.0f, particle.Start.y, 3));
        system.GenSort = TwinParticleSystem.GenSortType.Sphere;
        system.RandomEmit = new Vector3(0, 32768, 16384);
        var dome = new ParticleSimulation { System = system };
        dome.Update(ParticleSimulation.FrameTime);
        Assert.Contains(dome.Particles, particle => particle.Start.y < 1.0f);
        Assert.All(dome.Particles, particle =>
        {
            Assert.True(particle.Start.y >= -0.001f);
            Assert.Equal(2.0f, particle.Start.Length, 3);
        });
    }

    // Ghosts are copies of the particle spawning the separation later, with the same look
    [Fact]
    public void GhostsTrailTheirParticle()
    {
        var system = System(genRate: -60, lifeTime: 1.0f);
        system.ParticleGhostsNum = 2;
        system.GhostSeparation = 0.1f;
        var simulation = new ParticleSimulation { System = system };

        // One every 60 frames, on the frames the counter divides by
        Run(simulation, 60);
        Assert.Single(simulation.Particles);
        Run(simulation, 13);
        Assert.Equal(3, simulation.Particles.Count);
        var spawns = simulation.Particles.Select(particle => particle.SpawnTime).OrderBy(time => time).ToList();
        Assert.Equal(0.1f, spawns[1] - spawns[0], 3);
        Assert.Equal(0.1f, spawns[2] - spawns[1], 3);
    }

    // Bounce systems turn the particle around at the plane, keeping the bounce factor of its speed
    [Fact]
    public void BounceSystemsBounceOffThePlane()
    {
        var system = System(genRate: 1, lifeTime: 10.0f);
        system.GenSort = TwinParticleSystem.GenSortType.Bounce;
        system.Gravity = -1.0f;
        system.RandomStart = new Vector3(0, 0, 0);
        var simulation = new ParticleSimulation { System = system, PlaneOffset = -1.0f, BounceFactor = 0.5f };

        // The first particle falls from the emitter to the plane a unit below in a second, then comes back up at half the speed
        Run(simulation, 70);
        var particle = simulation.Particles.OrderBy(p => p.SpawnTime).First();
        Assert.True(simulation.PositionAt(particle, simulation.Time).y > -1.0f);
        Assert.Equal(3.0f, particle.Velocity.y, 3);
        Assert.Equal(1.0f, particle.Velocity.y + 2.0f * system.Gravity * 1.0f, 3);
        Assert.True(particle.BounceAt > simulation.Time);
    }

    // The emitter only runs while the camera is between its cut radii
    [Fact]
    public void EmittersOnlyRunWithinTheirCutRadii()
    {
        var system = System(genRate: 1, lifeTime: 10.0f);
        system.CutOnRadius = 5.0f;
        system.CutOffRadius = 50.0f;
        var simulation = new ParticleSimulation { System = system, CameraDistance = 100.0f };

        Run(simulation, 10);
        Assert.Empty(simulation.Particles);
        simulation.CameraDistance = 20.0f;
        Run(simulation, 70);
        Assert.NotEmpty(simulation.Particles);
        simulation.CameraDistance = 2.0f;
        Run(simulation, 2);
        Assert.Empty(simulation.Particles);
    }

    [Fact]
    public void LooksFollowTheCurvesInTheGamesUnits()
    {
        var system = System(lifeTime: 1.0f);
        system.ColorGradients[0] = new Vector4(0, 128, 64, 0);
        system.ColorGradients[1] = new Vector4(1, 128, 64, 0);
        system.SizeWidth = Curve((0, 5000), (1, 15000));
        system.Rotation = Curve((0, 0), (1, 16384));
        system.JibberXFreq = 1.0f;
        system.JibberXAmp = 10000.0f;
        var simulation = new ParticleSimulation { System = system };

        simulation.Evaluate(new ParticleSimulation.Particle { SpawnTime = 0.0f, LifeTime = 1.0f }, 0.5f, out var color, out var size, out var angle, out var jibber);

        Assert.Equal(new vec4(1.0f, 0.5f, 0.0f, 0.5f), color);
        Assert.Equal(1.0f, size.x, 3);
        Assert.Equal(MathF.PI / 4, angle, 3);
        // A full cycle over the life is back at 0 halfway
        Assert.Equal(0.0f, jibber.x, 3);
        simulation.Evaluate(new ParticleSimulation.Particle { SpawnTime = 0.0f, LifeTime = 1.0f }, 0.25f, out _, out _, out _, out jibber);
        Assert.Equal(1.0f, jibber.x, 3);
    }

    // The game adds 2^19 to the rectangle's values and keeps the low 10 bits of the pixel, so both offsets in its data work
    [Fact]
    public void TextureRectanglesKeepTheLowBitsOfThePixel()
    {
        Assert.Equal(66.0f, ParticleEmitter.TexturePixel(524288.0f + 66.0f));
        Assert.Equal(68.0f, ParticleEmitter.TexturePixel(262144.0f + 68.0f));
        Assert.Equal(12.0f, ParticleEmitter.TexturePixel(12.0f));
    }
}
