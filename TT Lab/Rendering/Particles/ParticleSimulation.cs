using System;
using System.Collections.Generic;
using GlmSharp;
using TT_Lab.AssetData.Instance.Particle;
using Twinsanity.TwinsanityInterchange.Common.Particles;

namespace TT_Lab.Rendering.Particles;

/// <summary>
/// Runs a particle system the way the game does, to show what an emitter looks like
/// </summary>
/// <remarks>
/// Worked out from the PAL executable. Everything runs in frames of 1/60 s: an emitter that's on makes <see cref="ParticleSystem.GenRate"/>
/// particles a frame (one every that many frames when negative) while the camera is between its cut radii, for its on time and then
/// waits its off time, the cycle shifted by the timing offsets against the frame counter. A particle is a start, a velocity and a
/// spawn time, and sits at start + velocity × t + (0, gravity, 0) × t² in the emitter's gravity space (the game's VU1 works that out
/// every frame, its bounce code on the CPU solves the same formula), which <see cref="GravityRotation"/> turns into the world. The
/// emit rotation turns the start and the velocity within that space. Every system's particles live in a ring of as many slots as the
/// game works out from the rate, timing, lifetime and ghosts, new ones writing over the oldest, at most 1024 (384 hexagons). Ghosts
/// are copies of a particle spawning <see cref="ParticleSystem.GhostSeparation"/> apart. Sizes and the jibber's amplitudes are in ten
/// thousandths of a unit, angles in 65536ths of a turn, the jibber's frequencies cycles per life, colors bytes with 128 leaving the
/// texture as it is, and the curves are sampled at 64 steps of the life
/// </remarks>
public sealed class ParticleSimulation
{
    public const float FrameTime = 1.0f / 60.0f;
    public const float SizeUnit = 1.0f / 10000.0f;
    public const float ColorUnit = 1.0f / 128.0f;
    public const float AngleUnit = MathF.PI * 2.0f / 65536.0f;
    public const int LifeSteps = 64;
    public const int MaxParticles = 0x400;
    public const int MaxHexagonParticles = 0x180;
    // A viewport that wasn't drawn for a while shouldn't make up for all of it at once
    private const float MaxDelta = 0.1f;
    private const float Turn = 65536.0f;

    public struct Particle
    {
        /// <summary>
        /// Where it started from the emitter, in the gravity space
        /// </summary>
        public vec3 Start;
        public vec3 Velocity;
        public float SpawnTime;
        public float LifeTime;
        /// <summary>
        /// When it bounces next, infinity for never
        /// </summary>
        public float BounceAt;
    }

    private readonly Random _random;
    private readonly List<Particle> _alive = [];
    private Particle[] _slots = [];
    private int _next;
    private ParticleSystem? _system;
    private float _time;
    private float _accumulated;
    private int _frame;
    private int _onLeft;
    private int _nextActiveFrame;
    private bool _enabled = true;
    private int _rotorYaw;
    private int _rotorTilt;
    private float _startTime;
    private Timing _timing;

    // What lines the emitter's cycle up with the frame counter when it starts
    private readonly record struct Timing(int Offset, int On, int OnRandom, int Off, int OffRandom, bool Rotor, float RotorYaw, float RotorTilt);

    public ParticleSimulation(int seed = 0)
    {
        _random = new Random(seed);
    }

    /// <summary>
    /// Changed from the UI thread while it runs on the render thread, the system is only ever read. Setting another one starts over
    /// </summary>
    public ParticleSystem? System
    {
        get => _system;
        set
        {
            if (_system == value)
            {
                return;
            }

            _system = value;
            Reset();
        }
    }

    /// <summary>
    /// Turns the start and velocity of the particles within the gravity space
    /// </summary>
    public quat EmitRotation { get; set; } = quat.Identity;

    /// <summary>
    /// Turns the space the particles move and fall in into the world
    /// </summary>
    public quat GravityRotation { get; set; } = quat.Identity;

    /// <summary>
    /// How far the camera is from the emitter, which the system's cut radii compare against. Null runs the emitter whatever the distance
    /// </summary>
    public float? CameraDistance { get; set; }

    /// <summary>
    /// The emitter's frames added to the system's timing offset
    /// </summary>
    public int TimingOffset { get; set; }

    public float PlaneOffset { get; set; }

    public float BounceFactor { get; set; } = 0.9f;

    public short BouncePlaneAngle { get; set; }

    public float Time => _time;

    public int Frame => _frame;

    /// <summary>
    /// Whether the emitter runs at the camera's distance
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// The particles alive after the last update, ghosts still to spawn left out
    /// </summary>
    public IReadOnlyList<Particle> Particles => _alive;

    /// <summary>
    /// An emitter's rotation: the tilt about Z (the up leaning towards -X), then the yaw about Y, then the roll about X
    /// </summary>
    public static quat Rotation(Int16 tilt, Int16 yaw, Int16 roll = 0)
    {
        return quat.FromAxisAngle(roll * AngleUnit, vec3.UnitX) * quat.FromAxisAngle(yaw * AngleUnit, vec3.UnitY) * quat.FromAxisAngle(tilt * AngleUnit, vec3.UnitZ);
    }

    /// <summary>
    /// The direction of the radial sorts: the up tilted by the angle about Z then turned about Y, angles in 65536ths of a turn
    /// </summary>
    public static vec3 RadialDirection(float tilt, float yaw)
    {
        return RotateY(RotateZ(vec3.UnitY, tilt * AngleUnit), yaw * AngleUnit);
    }

    public static bool IsRadial(ParticleSystem system) => system.GenSort is TwinParticleSystem.GenSortType.Radial or TwinParticleSystem.GenSortType.RadialRotor
        or TwinParticleSystem.GenSortType.Sphere or TwinParticleSystem.GenSortType.Star;

    /// <summary>
    /// Where a particle is at a time, in the gravity space
    /// </summary>
    public vec3 PositionAt(in Particle particle, float time)
    {
        var age = time - particle.SpawnTime;
        return particle.Start + particle.Velocity * age + new vec3(0.0f, (_system?.Gravity ?? 0.0f) * age * age, 0.0f);
    }

    public void Update(float delta)
    {
        var system = _system;
        if (system == null)
        {
            _alive.Clear();
            return;
        }

        // Editing the timing lines the cycle up again, the particles already out stay
        if (TimingOf(system) != _timing)
        {
            Rephase(system);
        }

        _accumulated += Math.Clamp(delta, 0.0f, MaxDelta);
        while (_accumulated >= FrameTime)
        {
            _accumulated -= FrameTime;
            Step(system);
        }

        CollectAlive();
    }

    /// <summary>
    /// Color, size, angle and jibber of a particle at the time, sampled at the game's 64 steps of the life
    /// </summary>
    public void Evaluate(in Particle particle, float time, out vec4 color, out vec2 size, out float angle, out vec2 jibber)
    {
        var system = _system!;
        var step = Math.Clamp((int)((time - particle.SpawnTime) / particle.LifeTime * LifeSteps), 0, LifeSteps - 1);
        var life = step / (float)LifeSteps;
        var rgb = ParticleCurves.EvaluateColor(system.ColorGradients, life);
        var alpha = ParticleCurves.Evaluate(system.AlphaGradient, life);
        color = new vec4(Byte(rgb.x), Byte(rgb.y), Byte(rgb.z), Byte(alpha)) * ColorUnit;
        size = new vec2(ParticleCurves.Evaluate(system.SizeWidth, life), ParticleCurves.Evaluate(system.SizeHeight, life)) * SizeUnit;
        angle = ParticleCurves.Evaluate(system.Rotation, life) * AngleUnit;
        jibber = new vec2(MathF.Sin(system.JibberXFreq * life * MathF.PI * 2.0f) * system.JibberXAmp,
            MathF.Sin(system.JibberYFreq * life * MathF.PI * 2.0f) * system.JibberYAmp) * SizeUnit;
    }

    private static float Byte(float value) => Math.Clamp(value, 0.0f, 255.0f);

    private void Reset()
    {
        _alive.Clear();
        _slots = [];
        _next = 0;
        _startTime = _time;
        var system = _system;
        if (system == null)
        {
            return;
        }

        Rephase(system);
    }

    private Timing TimingOf(ParticleSystem system)
    {
        return new Timing(system.TimingOffset + TimingOffset, system.EmitterOverTime, system.EmitterOverTimeRandom, system.EmitterOffTime, system.EmitterOffTimeRandom,
            system.GenSort == TwinParticleSystem.GenSortType.RadialRotor, system.RandomEmit.Y, system.RandomEmit.Z);
    }

    private void Rephase(ParticleSystem system)
    {
        _timing = TimingOf(system);
        // The emitter's cycle is shifted against the frame counter, unless its times are random
        var offset = system.TimingOffset + TimingOffset;
        _onLeft = system.EmitterOverTime;
        _nextActiveFrame = _frame;
        _rotorYaw = 0;
        _rotorTilt = 0;
        if (system.EmitterOverTimeRandom == 0 && system.EmitterOffTimeRandom == 0)
        {
            if (system.GenSort == TwinParticleSystem.GenSortType.RadialRotor)
            {
                _rotorYaw = (short)(int)(offset * system.RandomEmit.Y);
                _rotorTilt = (short)(int)(offset * system.RandomEmit.Z);
            }

            var period = system.EmitterOverTime + system.EmitterOffTime;
            if (period > 0)
            {
                var into = _frame % period - offset;
                _nextActiveFrame = _frame + (into == 0 ? 0 : period - into);
            }
        }
    }

    private void Step(ParticleSystem system)
    {
        _frame++;
        _time += FrameTime;
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].BounceAt <= _time)
            {
                Bounce(system, ref _slots[i]);
            }
        }

        var enabled = CameraDistance is not { } distance || (system.CutOnRadius <= distance && (system.CutOffRadius <= 0.0f || distance <= system.CutOffRadius));
        if (enabled != _enabled)
        {
            _enabled = enabled;
            // The game lets go of the blocks of an emitter that's out of range
            if (!enabled)
            {
                _slots = [];
                _next = 0;
            }
        }

        if (_frame < _nextActiveFrame)
        {
            return;
        }

        if (_enabled)
        {
            Generate(system);
        }

        // Emitters out of range get looked at again in 50 to 57 frames
        _onLeft--;
        var wait = _enabled ? 1 : 50 + _random.Next(8);
        if (_onLeft <= 0)
        {
            wait += system.EmitterOffTime + (system.EmitterOffTimeRandom != 0 ? 1 + _random.Next(system.EmitterOffTimeRandom) : 0);
            _onLeft = system.EmitterOverTime + (system.EmitterOverTimeRandom != 0 ? 1 + _random.Next(system.EmitterOverTimeRandom) : 0);
        }

        _nextActiveFrame = _frame + wait;
    }

    private void Generate(ParticleSystem system)
    {
        var most = Math.Min(system.ComputeMaxParticleCount(), system.BlendMode == 7 ? MaxHexagonParticles : MaxParticles);
        if (_slots.Length != most)
        {
            var slots = new Particle[most];
            for (var i = 0; i < most; i++)
            {
                slots[i] = i < _slots.Length ? _slots[i] : Dead;
            }

            _slots = slots;
            _next = Math.Min(_next, most - 1);
        }

        if (system.GenRate > 0)
        {
            for (var i = 0; i < system.GenRate; i++)
            {
                Emit(system);
            }
        }
        else if (system.GenRate < 0 && _frame % -system.GenRate == 0)
        {
            Emit(system);
        }
    }

    private static Particle Dead => new() { SpawnTime = float.NegativeInfinity, LifeTime = 0.0f, BounceAt = float.PositiveInfinity };

    private void CollectAlive()
    {
        _alive.Clear();
        foreach (var particle in _slots)
        {
            var age = _time - particle.SpawnTime;
            if (age >= 0.0f && age < particle.LifeTime)
            {
                _alive.Add(particle);
            }
        }
    }

    private void Emit(ParticleSystem system)
    {
        if (_slots.Length == 0)
        {
            return;
        }

        var particle = Make(system, _slots[_next]);
        Place(particle);
        for (var ghost = 1; ghost <= system.ParticleGhostsNum; ghost++)
        {
            var copy = particle;
            copy.SpawnTime += ghost * system.GhostSeparation;
            copy.BounceAt = particle.BounceAt == float.PositiveInfinity ? particle.BounceAt : particle.BounceAt + ghost * system.GhostSeparation;
            Place(copy);
        }
    }

    private void Place(in Particle particle)
    {
        _slots[_next] = particle;
        _next = (_next + 1) % _slots.Length;
    }

    private Particle Make(ParticleSystem system, in Particle previous)
    {
        var particle = new Particle { SpawnTime = _time, LifeTime = Math.Max(system.ParticleLifeTime, FrameTime), BounceAt = float.PositiveInfinity };
        var emit = EmitRotation;
        var start = system.RandomStart;
        var spread = system.RandomEmit;
        switch (system.GenSort)
        {
            case TwinParticleSystem.GenSortType.Ranges:
            case TwinParticleSystem.GenSortType.RangesRandomLife:
                particle.Start = new vec3(Ranged(system.StartRandomScale.X, system.StartBase.X), Ranged(system.StartRandomScale.Y, system.StartBase.Y), Ranged(system.StartRandomScale.Z, system.StartBase.Z));
                particle.Velocity = new vec3(Ranged(system.VelocityRandomScale.X, system.VelocityBase.X), Ranged(system.VelocityRandomScale.Y, system.VelocityBase.Y), Ranged(system.VelocityRandomScale.Z, system.VelocityBase.Z));
                if (system.GenSort == TwinParticleSystem.GenSortType.RangesRandomLife)
                {
                    particle.LifeTime = RandomLife(particle.LifeTime);
                }

                break;
            case TwinParticleSystem.GenSortType.Line:
            {
                var along = RandomInt() * 2.7939678e-10f - 0.3f;
                var x = along * MathF.Sin(14000 * AngleUnit);
                var z = along * MathF.Cos(14000 * AngleUnit);
                var speed = RandomInt() * 6.9849193e-10f + 1.0f;
                particle.Start = new vec3(x, Ranged(system.StartRandomScale.Y, system.StartBase.Y), z);
                particle.Velocity = new vec3(x * 1.5f + speed * MathF.Sin(30000 * AngleUnit), Ranged(system.VelocityRandomScale.Y, system.VelocityBase.Y), z * 1.5f + speed * MathF.Cos(30000 * AngleUnit));
                break;
            }
            case TwinParticleSystem.GenSortType.Reuse:
                particle.Start = new vec3(previous.Start.x, Ranged(system.StartRandomScale.Y, system.StartBase.Y), previous.Start.z);
                particle.Velocity = new vec3(previous.Start.x * 2.0f, Ranged(system.VelocityRandomScale.Y, system.VelocityBase.Y), previous.Start.z * 2.0f);
                break;
            case TwinParticleSystem.GenSortType.Radial:
            case TwinParticleSystem.GenSortType.Star:
            {
                var yaw = (int)(Spread(spread.Y) + start.Y);
                var tilt = (int)(Spread(spread.Z) + start.Z);
                var radius = start.X * Ramp(system);
                if (system.GenSort == TwinParticleSystem.GenSortType.Star && system.StarRadialPoints > 0)
                {
                    // The radius dips to the ratio between the points
                    var period = 65536 / system.StarRadialPoints;
                    var phase = (yaw + 65536) % period / (float)period * 32768.0f;
                    var dip = MathF.Sin(phase * AngleUnit);
                    radius *= system.StarRadiusRatio + (1.0f - system.StarRadiusRatio) * (1.0f - dip);
                }

                var direction = RadialDirection(tilt, yaw);
                particle.Start = emit * (direction * radius);
                particle.Velocity = emit * (direction * (system.Velocity + Spread(spread.X)));
                break;
            }
            case TwinParticleSystem.GenSortType.RadialRotor:
            {
                var direction = RadialDirection(_rotorTilt + start.Z, _rotorYaw + start.Y);
                particle.Start = emit * (direction * start.X);
                particle.Velocity = emit * (direction * (system.Velocity + Spread(spread.X)));
                _rotorYaw = spread.Y == 0.0f ? 0 : (short)(_rotorYaw + (int)spread.Y);
                _rotorTilt = spread.Z == 0.0f ? 0 : (short)(_rotorTilt + (int)spread.Z);
                break;
            }
            case TwinParticleSystem.GenSortType.Sphere:
            {
                // Even over the sphere's area: the tilt from a uniform sine between the tilts
                var sineFrom = MathF.Sin((start.Z - spread.Z) * AngleUnit);
                var sineTo = MathF.Sin((start.Z + spread.Z) * AngleUnit);
                var tilt = MathF.Asin(Math.Clamp(sineFrom + Random01() * (sineTo - sineFrom), -1.0f, 1.0f)) / AngleUnit;
                var yaw = (Random01() - 0.5f) * 2.0f * spread.Y + start.Y;
                var direction = RadialDirection(tilt, yaw);
                particle.Start = emit * (direction * start.X);
                particle.Velocity = emit * (direction * (system.Velocity + Spread(spread.X)));
                break;
            }
            case TwinParticleSystem.GenSortType.Spheroid:
            {
                var point = RotateY(RotateZ(new vec3(MathF.Sqrt(Random01()), 0.0f, 0.0f), MathF.Asin(Random01() * 2.0f - 1.0f)), Random01() * MathF.PI * 2.0f);
                particle.Start = emit * new vec3(point.x * start.X, point.y * start.Y, point.z * start.Z);
                particle.Velocity = emit * new vec3(Spread(spread.X), system.Velocity + Spread(spread.Y), Spread(spread.Z));
                break;
            }
            default:
                particle.Start = emit * new vec3(Spread(start.X), Spread(start.Y), Spread(start.Z));
                particle.Velocity = emit * new vec3(Spread(spread.X), system.Velocity + Spread(spread.Y), Spread(spread.Z));
                break;
        }

        ApplyGenCode(system, ref particle);
        switch (system.GenSort)
        {
            case TwinParticleSystem.GenSortType.Bounce:
                particle.BounceAt = NextBounce(system, particle, 0.0f) is { } at ? particle.SpawnTime + at : float.PositiveInfinity;
                break;
            case TwinParticleSystem.GenSortType.BounceXZ:
                particle.BounceAt = WallHit(particle) is { } hit ? particle.SpawnTime + hit : float.PositiveInfinity;
                break;
        }

        return particle;
    }

    // Velocity from the start position, a few fixed rules
    private void ApplyGenCode(ParticleSystem system, ref Particle particle)
    {
        var start = particle.Start;
        switch (system.GenCode)
        {
            case 1:
                particle.Velocity.x = start.x * 2.0f;
                particle.Velocity.z = start.z * 2.0f;
                break;
            case 2:
                particle.Velocity.x = -start.x;
                particle.Velocity.z = -start.z;
                break;
            case 3:
                particle.Velocity.x = start.x * 4.0f;
                particle.Velocity.z = start.z * 4.0f;
                break;
            case 4:
                particle.Velocity.x = start.x * 16.0f;
                particle.Velocity.z = start.z * 16.0f;
                break;
            case 5:
                particle.Velocity.x += start.x * -0.6f;
                particle.Velocity.z += start.z * -0.6f;
                particle.Velocity.y -= MathF.Sqrt(start.x * start.x + start.z * start.z) * 0.4f;
                particle.LifeTime = RandomLife(particle.LifeTime);
                break;
            case 6:
                particle.Velocity = start * 5.4f;
                break;
        }
    }

    // When the particle reaches the plane after the time along its way, the later of the two times it crosses it
    private float? NextBounce(ParticleSystem system, in Particle particle, float after)
    {
        var height = particle.Start.y - PlaneOffset;
        var speed = particle.Velocity.y;
        var gravity = system.Gravity;
        float hit;
        if (gravity == 0.0f)
        {
            if (speed == 0.0f)
            {
                return null;
            }

            hit = -height / speed;
        }
        else
        {
            var discriminant = speed * speed - 4.0f * gravity * height;
            if (discriminant < 0.0f)
            {
                return null;
            }

            var root = MathF.Sqrt(discriminant);
            hit = Math.Max((-speed - root) / (2.0f * gravity), (-speed + root) / (2.0f * gravity));
        }

        return hit > after && hit < particle.LifeTime ? hit : null;
    }

    private void Bounce(ParticleSystem system, ref Particle particle)
    {
        var at = particle.BounceAt - particle.SpawnTime;
        particle.BounceAt = float.PositiveInfinity;
        if (system.GenSort == TwinParticleSystem.GenSortType.BounceXZ)
        {
            // Reflected off the vertical plane, the start moved so the formula keeps the position
            var normal = WallNormal();
            var push = (particle.Velocity.x * normal.x + particle.Velocity.z * normal.z) * -(BounceFactor + 1.0f);
            var velocity = particle.Velocity + new vec3(push * normal.x, 0.0f, push * normal.z);
            particle.Start -= (velocity - particle.Velocity) * at;
            particle.Velocity = velocity;
            return;
        }

        // The speed at the plane, turned around and damped, and the start moved so the formula keeps the position
        var falling = 2.0f * system.Gravity * at;
        var speed = -BounceFactor * (particle.Velocity.y + falling) - falling;
        particle.Start.y -= (speed - particle.Velocity.y) * at;
        particle.Velocity.y = speed;
        var next = NextBounce(system, particle, at);
        if (next is { } time)
        {
            if (time - at >= FrameTime)
            {
                particle.BounceAt = particle.SpawnTime + time;
            }
            else
            {
                // Landing within the frame comes to rest
                particle.Velocity.y = 0.0f;
                particle.Start.y = PlaneOffset;
            }
        }
    }

    private vec3 WallNormal() => RotateY(vec3.UnitX, BouncePlaneAngle * AngleUnit);

    // When the particle's straight path crosses the vertical plane within its life
    private float? WallHit(in Particle particle)
    {
        var normal = WallNormal();
        var from = normal.x * particle.Start.x + normal.z * particle.Start.z - PlaneOffset;
        var end = particle.Start + particle.Velocity * particle.LifeTime;
        var to = normal.x * end.x + normal.z * end.z - PlaneOffset;
        if (from == 0.0f || to == 0.0f || from > 0.0f == to > 0.0f)
        {
            return null;
        }

        return particle.LifeTime * (-from / (to - from));
    }

    private float Ramp(ParticleSystem system)
    {
        if (system.RampTime <= 0.0f)
        {
            return 1.0f;
        }

        var age = _time - _startTime;
        return age < system.RampTime ? age / system.RampTime : 1.0f;
    }

    private float RandomLife(float lifeTime) => lifeTime + lifeTime * RandomInt() * 6.652304e-10f;

    private float Random01() => _random.NextSingle();

    private float RandomInt() => _random.Next();

    private float Spread(float range) => (Random01() * 2.0f - 1.0f) * range;

    private float Ranged(float scale, float offset) => RandomInt() * scale + offset;

    private static vec3 RotateZ(vec3 v, float angle)
    {
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new vec3(v.x * cos - v.y * sin, v.x * sin + v.y * cos, v.z);
    }

    private static vec3 RotateY(vec3 v, float angle)
    {
        var sin = MathF.Sin(angle);
        var cos = MathF.Cos(angle);
        return new vec3(v.x * cos + v.z * sin, v.y, v.z * cos - v.x * sin);
    }
}
