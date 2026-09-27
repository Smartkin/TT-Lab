using System;
using System.Collections.Generic;
using GlmSharp;
using TT_Lab.AssetData.Instance.Particle;

namespace TT_Lab.Rendering.Particles;

/// <summary>
/// Runs a particle system the way the game roughly does, to show what an emitter looks like. Only what's known of the format is used
/// </summary>
/// <remarks>
/// Worked out from the game's systems: particles are made at the rate per second up to the most there can be, systems making none at a
/// rate make them all at once. Emitters with an off time are on and off for their times in frames. Gravity pulls along the emitter's up,
/// drips, splashes and sparks have negative gravity and fire and steam positive. Sizes and the jibber's amplitudes are in ten
/// thousandths of a unit, angles in 65536ths of a turn and colors and alpha are full at 128, the curves' values past that brighten.
/// Radial systems (<see cref="IsRadial"/>, rings of dust, impacts and charges gathering in) keep angles in their random values
/// </remarks>
public sealed class ParticleSimulation
{
    public const float FrameTime = 1.0f / 60.0f;
    public const float SizeUnit = 1.0f / 10000.0f;
    public const float ColorUnit = 1.0f / 128.0f;
    public const float AngleUnit = MathF.PI * 2.0f / 65536.0f;
    // A viewport that wasn't drawn for a while shouldn't make up for all of it at once
    private const float MaxDelta = 0.1f;

    public struct Particle
    {
        public vec3 Position;
        public vec3 Velocity;
        public float Age;
    }

    private readonly List<Particle> _particles = [];
    private readonly Random _random;
    private bool _isOn = true;
    private float _phaseLeft;
    private float _toMake;

    public ParticleSimulation(int seed = 0)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Changed from the UI thread while it runs on the render thread, the system is only ever read
    /// </summary>
    public ParticleSystem? System { get; set; }

    /// <summary>
    /// Turns the emitter's up, which particles fly along, and the plane radial systems spread in
    /// </summary>
    public quat EmitRotation { get; set; } = quat.Identity;

    public vec3 EmitDirection => EmitRotation * vec3.UnitY;

    public vec3 GravityDirection { get; set; } = vec3.UnitY;

    public IReadOnlyList<Particle> Particles => _particles;

    /// <summary>
    /// An emitter's emitting or gravity rotation, around X and then Y
    /// </summary>
    public static quat Rotation(Int16 rotX, Int16 rotY)
    {
        return quat.FromAxisAngle(rotY * AngleUnit, vec3.UnitY) * quat.FromAxisAngle(rotX * AngleUnit, vec3.UnitX);
    }

    /// <summary>
    /// Systems that make their particles on a ring or a sphere around the emitter and send them out or in: the random emit's Y and Z are
    /// how far the direction spreads around the emitter's up and away from its plane, the random start's X how far from the emitter they
    /// start
    /// </summary>
    public static bool IsRadial(ParticleSystem system) => system.GenSort is 6 or 7 or 11;

    public void Update(float delta)
    {
        var system = System;
        if (system == null)
        {
            _particles.Clear();
            return;
        }

        delta = Math.Clamp(delta, 0.0f, MaxDelta);
        var lifeTime = LifeTime(system);
        var gravity = GravityDirection * system.Gravity;
        for (var i = _particles.Count - 1; i >= 0; i--)
        {
            var particle = _particles[i];
            particle.Age += delta;
            if (particle.Age >= lifeTime)
            {
                _particles[i] = _particles[^1];
                _particles.RemoveAt(_particles.Count - 1);
                continue;
            }

            particle.Velocity += gravity * delta;
            particle.Position += particle.Velocity * delta;
            _particles[i] = particle;
        }

        Make(system, delta);
    }

    /// <summary>
    /// Color, size and angle of a particle at its age
    /// </summary>
    public void Evaluate(in Particle particle, out vec4 color, out vec2 size, out float angle, out vec2 jibber)
    {
        var system = System!;
        var time = Math.Clamp(particle.Age / LifeTime(system), 0.0f, 1.0f);
        var rgb = ParticleCurves.EvaluateColor(system.ColorGradients, time) * ColorUnit;
        var alpha = Math.Clamp(ParticleCurves.Evaluate(system.AlphaGradient, time) * ColorUnit, 0.0f, 1.0f);
        color = new vec4(rgb, alpha);
        size = new vec2(ParticleCurves.Evaluate(system.SizeWidth, time), ParticleCurves.Evaluate(system.SizeHeight, time)) * SizeUnit;
        angle = ParticleCurves.Evaluate(system.Rotation, time) * AngleUnit;
        jibber = new vec2(MathF.Sin(particle.Age * system.JibberXFreq * MathF.PI * 2.0f) * system.JibberXAmp,
            MathF.Sin(particle.Age * system.JibberYFreq * MathF.PI * 2.0f) * system.JibberYAmp) * SizeUnit;
    }

    private static float LifeTime(ParticleSystem system) => Math.Max(system.ParticleLifeTime, FrameTime);

    private void Make(ParticleSystem system, float delta)
    {
        if (system.EmitterOffTime > 0)
        {
            _phaseLeft -= delta;
            while (_phaseLeft <= 0.0f)
            {
                _isOn = !_isOn;
                var frames = _isOn
                    ? system.EmitterOverTime + _random.Next(system.EmitterOverTimeRandom + 1)
                    : system.EmitterOffTime + _random.Next(system.EmitterOffTimeRandom + 1);
                _phaseLeft += Math.Max(frames, 1) * FrameTime;
            }
        }
        else
        {
            _isOn = true;
        }

        if (!_isOn)
        {
            _toMake = 0.0f;
            return;
        }

        var most = Math.Max((int)system.MaxParticleCount, 1);
        // Made all at once, again once they're gone so the preview keeps showing them
        if (system.GenRate == 0)
        {
            if (_particles.Count == 0)
            {
                while (_particles.Count < most)
                {
                    MakeParticle(system);
                }
            }

            return;
        }

        _toMake = Math.Min(_toMake + system.GenRate * delta, most);
        while (_toMake >= 1.0f && _particles.Count < most)
        {
            MakeParticle(system);
            _toMake -= 1.0f;
        }
    }

    private void MakeParticle(ParticleSystem system)
    {
        if (IsRadial(system))
        {
            var yaw = Spread(system.RandomEmit.Y) * AngleUnit;
            var pitch = Spread(system.RandomEmit.Z) * AngleUnit;
            var direction = EmitRotation * new vec3(MathF.Cos(pitch) * MathF.Cos(yaw), MathF.Sin(pitch), MathF.Cos(pitch) * MathF.Sin(yaw));
            _particles.Add(new Particle { Position = direction * system.RandomStart.X, Velocity = direction * system.Velocity });
            return;
        }

        var start = new vec3(Spread(system.RandomStart.X), Spread(system.RandomStart.Y), Spread(system.RandomStart.Z));
        var velocity = EmitDirection * system.Velocity + new vec3(Spread(system.RandomEmit.X), Spread(system.RandomEmit.Y), Spread(system.RandomEmit.Z));
        _particles.Add(new Particle { Position = start, Velocity = velocity });
    }

    private float Spread(float range) => (_random.NextSingle() * 2.0f - 1.0f) * range;
}
