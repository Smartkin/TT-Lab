using GlmSharp;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Rendering.Particles;
using Twinsanity.TwinsanityInterchange.Common;

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

    private static ParticleSystem System(UInt16 genRate = 10, UInt16 most = 100, float lifeTime = 1.0f) => new()
    {
        GenRate = genRate,
        MaxParticleCount = most,
        ParticleLifeTime = lifeTime,
        EmitterOverTime = 1,
        ColorGradients = Enumerable.Range(0, ParticleCurves.MaxKeys).Select(_ => new Vector4()).ToArray(),
        AlphaGradient = Curve((0, 128), (1, 0)),
        SizeWidth = Curve((0, 10000), (1, 10000)),
        SizeHeight = Curve((0, 10000), (1, 10000)),
        Rotation = Curve((0, 0), (1, 0)),
    };

    private static void Run(ParticleSimulation simulation, float seconds)
    {
        for (var time = 0.0f; time < seconds; time += ParticleSimulation.FrameTime)
        {
            simulation.Update(ParticleSimulation.FrameTime);
        }
    }

    [Fact]
    public void CurvesEndAtTheirFirstKeyAtTheEndOfTheLife()
    {
        // The game's CRATE_BREAK alpha: jumps to full right away and fades, the keys after the first at 1 are leftovers
        var alpha = Curve((0, 0), (0, 255), (1, 0), (1, 0), (1, 0));
        alpha[5] = new Vector2 { X = 0.5f, Y = 999 };

        Assert.Equal(3, ParticleCurves.CountKeys(alpha));
        Assert.Equal(255, ParticleCurves.Evaluate(alpha, 0.0001f), 1);
        Assert.Equal(127.5f, ParticleCurves.Evaluate(alpha, 0.5f), 1);
        Assert.Equal(0, ParticleCurves.Evaluate(alpha, 1.0f), 1);
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

    [Fact]
    public void ParticlesAreMadeAtTheRateUpToTheMost()
    {
        var simulation = new ParticleSimulation { System = System(genRate: 30, most: 10, lifeTime: 5.0f) };

        Run(simulation, 0.1f);
        Assert.InRange(simulation.Particles.Count, 2, 4);

        Run(simulation, 2.0f);
        Assert.Equal(10, simulation.Particles.Count);
    }

    [Fact]
    public void ParticlesGoAtTheEndOfTheirLife()
    {
        var simulation = new ParticleSimulation { System = System(genRate: 60, most: 1000, lifeTime: 0.5f) };

        Run(simulation, 3.0f);

        // A second makes 60, half of them are around at once
        Assert.InRange(simulation.Particles.Count, 28, 32);
        Assert.All(simulation.Particles, particle => Assert.True(particle.Age < 0.5f));
    }

    [Fact]
    public void SystemsWithoutARateMakeThemAllAtOnce()
    {
        var simulation = new ParticleSimulation { System = System(genRate: 0, most: 5, lifeTime: 0.5f) };

        simulation.Update(ParticleSimulation.FrameTime);
        Assert.Equal(5, simulation.Particles.Count);

        Run(simulation, 0.3f);
        Assert.Equal(5, simulation.Particles.Count);
    }

    [Fact]
    public void EmittersWithAnOffTimeTakeBreaks()
    {
        var system = System(genRate: 600, most: 1000, lifeTime: 10.0f);
        system.EmitterOverTime = 6;
        system.EmitterOffTime = 54;
        var simulation = new ParticleSimulation { System = system };

        Run(simulation, 1.0f);

        // On for 6 of every 60 frames at 10 particles a frame
        Assert.InRange(simulation.Particles.Count, 50, 70);
    }

    // The game's drips and splashes have negative gravity, fire and steam positive
    [Fact]
    public void ParticlesFlyTheEmittersWayAndFallWithNegativeGravity()
    {
        var system = System(genRate: 60, most: 1, lifeTime: 10.0f);
        system.Velocity = 2.0f;
        system.Gravity = -1.0f;
        // A quarter turn around X points up to the front
        var simulation = new ParticleSimulation { System = system, EmitRotation = ParticleSimulation.Rotation(16384, 0) };

        Run(simulation, 1.0f);

        var particle = Assert.Single(simulation.Particles);
        Assert.Equal(2.0f * particle.Age, particle.Position.z, 1);
        Assert.Equal(-0.5f * particle.Age * particle.Age, particle.Position.y, 1);
    }

    // Rings of dust around Crash and charges gathering in: the random values are angles and how far out they start
    [Fact]
    public void RadialSystemsMakeParticlesOnARing()
    {
        var system = System(genRate: 600, most: 50, lifeTime: 10.0f);
        system.GenSort = 11;
        system.Velocity = -3.0f;
        system.RandomEmit = new Vector3(0, 32768, 0);
        system.RandomStart = new Vector3(2, 16384, 0);
        var simulation = new ParticleSimulation { System = system };

        simulation.Update(ParticleSimulation.FrameTime);

        Assert.NotEmpty(simulation.Particles);
        Assert.All(simulation.Particles, particle =>
        {
            Assert.Equal(0.0f, particle.Velocity.y, 3);
            Assert.Equal(3.0f, particle.Velocity.Length, 3);
            // Flying in towards the emitter from 2 units away
            Assert.True(vec3.Dot(particle.Position, particle.Velocity) < 0.0f);
            Assert.InRange(particle.Position.Length, 1.9f, 2.0f);
        });
    }

    [Fact]
    public void LooksFollowTheCurvesInTheGamesUnits()
    {
        var system = System(lifeTime: 1.0f);
        system.ColorGradients[0] = new Vector4(0, 128, 64, 0);
        system.ColorGradients[1] = new Vector4(1, 128, 64, 0);
        system.SizeWidth = Curve((0, 5000), (1, 15000));
        system.Rotation = Curve((0, 0), (1, 16384));
        var simulation = new ParticleSimulation { System = system };

        simulation.Evaluate(new ParticleSimulation.Particle { Age = 0.5f }, out var color, out var size, out var angle, out _);

        Assert.Equal(new vec4(1.0f, 0.5f, 0.0f, 0.5f), color);
        Assert.Equal(1.0f, size.x, 3);
        Assert.Equal(MathF.PI / 4, angle, 3);
    }
}
