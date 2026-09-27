using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Instance.Particle;
using TT_Lab.Assets;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Particles;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// Particles of an emitter placed in a chunk, run and drawn every frame on the render thread
/// </summary>
public sealed class ParticleEmitter : Renderable
{
    // Has to match ParticleData in Particle.vert
    private const int FloatsPerParticle = 16;
    private const uint Binding = 4;
    // The texture's rectangles are stored plus 2^19, which made the tools' floats round them to whole pixels
    private const float TextureRectOffset = 524288.0f;

    // Every emitter of a viewport fills the same buffer right before drawing, they go away with the context
    private static readonly ConditionalWeakTable<RenderContext, SharedResources> Shared = new();

    private sealed class SharedResources(RenderContext context)
    {
        public readonly StreamStorageBuffer Particles = new(context, Binding, FloatsPerParticle);
        public readonly uint EmptyVao = context.Gl.GenVertexArray();
    }

    private readonly ParticleSimulation _simulation;
    private readonly LabURI[] _pages;

    public ParticleEmitter(RenderContext context, string name, IEnumerable<LabURI> pages, int seed) : base(context, name)
    {
        _simulation = new ParticleSimulation(seed);
        _pages = pages.ToArray();
    }

    /// <summary>
    /// Can be called from the UI thread while the emitter runs
    /// </summary>
    public void Configure(ParticleSystem? system, ParticleSystemInstance emitter)
    {
        _simulation.EmitRotation = ParticleSimulation.Rotation(emitter.EmitRotX, emitter.EmitRotY);
        _simulation.GravityDirection = ParticleSimulation.Rotation(emitter.GravityRotX, emitter.GravityRotY) * vec3.UnitY;
        _simulation.System = system;
    }

    public override (String, Int32)[] GetPriorityPasses()
    {
        return [(PassService.ParticlesPass, 0)];
    }

    protected override void RenderSelf(float delta)
    {
        _simulation.Update(delta);
        var system = _simulation.System;
        var particles = _simulation.Particles;
        if (system == null || particles.Count == 0 || system.TexturePage < 0 || system.TexturePage >= _pages.Length)
        {
            return;
        }

        var page = Context.TextureService.GetTexture(_pages[system.TexturePage]);
        if (page == null)
        {
            return;
        }

        var shared = Shared.GetValue(Context, context => new SharedResources(context));
        var buffer = shared.Particles;
        var origin = RenderTransform.Column3.xyz;
        var textureRect = new vec4(system.TextureStart.X, system.TextureStart.Y, system.TextureEnd.X, system.TextureEnd.Y) - TextureRectOffset;
        buffer.Clear();
        // Ghosts trail each particle where it was a moment before, fading out
        var ghosts = Math.Max((int)system.ParticleGhostsNum, 0);
        foreach (var particle in particles)
        {
            _simulation.Evaluate(particle, out var color, out var size, out var angle, out var jibber);
            var position = origin + particle.Position + new vec3(jibber, 0.0f);
            for (var ghost = 0; ghost <= ghosts; ghost++)
            {
                var data = buffer.Allocate(out _);
                var fade = 1.0f - ghost / (ghosts + 1.0f);
                StreamStorageBuffer.Write(data, new vec4(position - particle.Velocity * (system.GhostSeparation * ghost), angle));
                StreamStorageBuffer.Write(data[4..], new vec4(size, 0.0f, 0.0f));
                StreamStorageBuffer.Write(data[8..], new vec4(color.xyz, color.w * fade));
                StreamStorageBuffer.Write(data[12..], textureRect);
            }
        }

        buffer.Upload();
        page.Bind(TextureUnit.Texture0);
        var gl = Context.Gl;
        gl.BindVertexArray(shared.EmptyVao);
        gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, 6, (uint)buffer.Count, 0);
        gl.BindVertexArray(0);
    }
}
