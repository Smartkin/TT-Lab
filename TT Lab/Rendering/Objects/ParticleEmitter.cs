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
using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// Particles of an emitter placed in a chunk, run every frame on the render thread and drawn by the particle pass with the others
/// </summary>
public sealed class ParticleEmitter : Renderable
{
    // Has to match ParticleData in Particle.vert
    private const int FloatsPerParticle = 16;
    private const uint Binding = 4;
    private const int HexagonVertices = 18;
    // Every emitter of a viewport queues its particles into the same buffer, they go away with the context
    private static readonly ConditionalWeakTable<RenderContext, ParticleBatch> Batches = new();

    private readonly ParticleSimulation _simulation;
    private readonly LabURI[] _pages;
    private readonly TwinShader.AlphaBlendPresets[] _pageBlends;
    private bool _ignoresCamera;

    private sealed class ParticleBatch(RenderContext context)
    {
        public readonly StreamStorageBuffer Particles = new(context, Binding, FloatsPerParticle);
        public readonly List<Draw> Draws = [];
        public readonly uint EmptyVao = context.Gl.GenVertexArray();
        public FrameBuffer? FrameCopy;
        public ivec2 FrameCopySize;
    }

    private readonly record struct Draw(int First, int Count, Byte BlendMode, TextureBuffer Page, TwinShader.AlphaBlendPresets PageBlend, vec2 Distortion, int Queued);

    /// <param name="pageBlends">The alpha blending of every page's material, the Page material mode draws with it</param>
    public ParticleEmitter(RenderContext context, string name, IEnumerable<LabURI> pages, IEnumerable<TwinShader.AlphaBlendPresets> pageBlends, int seed) : base(context, name)
    {
        _simulation = new ParticleSimulation(seed);
        _pages = pages.ToArray();
        _pageBlends = pageBlends.ToArray();
    }

    /// <summary>
    /// Can be called from the UI thread while the emitter runs. Previews ignore the camera's distance, the game only runs an emitter
    /// while the camera is between its cut radii and draws it within its draw cut off
    /// </summary>
    public void Configure(ParticleSystem? system, ParticleSystemInstance emitter, bool ignoresCamera = false)
    {
        _simulation.EmitRotation = ParticleSimulation.Rotation(emitter.EmitTilt, emitter.EmitYaw, emitter.EmitRoll);
        _simulation.GravityRotation = ParticleSimulation.Rotation(emitter.GravityTilt, emitter.GravityYaw);
        _simulation.TimingOffset = emitter.TimingOffset;
        _simulation.PlaneOffset = emitter.PlaneOffset;
        _simulation.BounceFactor = emitter.BounceFactor;
        _simulation.BouncePlaneAngle = emitter.BouncePlaneAngle;
        _ignoresCamera = ignoresCamera;
        _simulation.System = system;
    }

    /// <summary>
    /// The system running and its particles, for what the viewport shows of it
    /// </summary>
    internal ParticleSimulation Simulation => _simulation;

    public override (String, Int32)[] GetPriorityPasses()
    {
        return [(PassService.ParticlesPass, 0)];
    }

    // The game's pixel of a texture rectangle value
    public static float TexturePixel(float value) => ParticleTextureRect.Pixel(value);

    protected override void RenderSelf(float delta)
    {
        var origin = RenderTransform.Column3.xyz;
        float? distance = _ignoresCamera ? null : (Context.FrameCamera.Position - origin).Length;
        _simulation.CameraDistance = distance;
        _simulation.Update(delta);
        var system = _simulation.System;
        var particles = _simulation.Particles;
        if (system == null || particles.Count == 0 || system.TexturePage < 0 || system.TexturePage >= _pages.Length || distance > system.DrawCutOff
            || !ParticleBlendModes.IsDrawn(system.BlendMode, system.DrawFlag))
        {
            return;
        }

        var page = Context.TextureService.GetTexture(_pages[system.TexturePage]);
        if (page == null)
        {
            return;
        }

        var batch = Batches.GetValue(Context, context => new ParticleBatch(context));
        var buffer = batch.Particles;
        var textureRect = new vec4(TexturePixel(system.TextureStart.X), TexturePixel(system.TextureStart.Y), TexturePixel(system.TextureEnd.X), TexturePixel(system.TextureEnd.Y));
        var gravity = _simulation.GravityRotation;
        var time = _simulation.Time;
        var data = buffer.Allocate(particles.Count, out var first);
        for (var i = 0; i < particles.Count; i++)
        {
            var particle = particles[i];
            _simulation.Evaluate(particle, time, out var color, out var size, out var angle, out var jibber);
            var position = origin + gravity * _simulation.PositionAt(particle, time);
            var element = data.Slice(i * FloatsPerParticle, FloatsPerParticle);
            StreamStorageBuffer.Write(element, new vec4(position, angle));
            StreamStorageBuffer.Write(element[4..], new vec4(size.x, size.y, jibber.x, jibber.y));
            StreamStorageBuffer.Write(element[8..], color);
            StreamStorageBuffer.Write(element[12..], textureRect);
        }

        var pageBlend = system.TexturePage < _pageBlends.Length ? _pageBlends[system.TexturePage] : TwinShader.AlphaBlendPresets.Mix;
        batch.Draws.Add(new Draw(first, particles.Count, system.BlendMode, page, pageBlend, new vec2(system.Distortion.X, system.Distortion.Y), batch.Draws.Count));
    }

    /// <summary>
    /// Draws the particles every emitter of the context queued this frame, the blend modes in the game's order: 3, 2, 1, 0, then the
    /// Distortion mode's hexagons over a copy of the frame
    /// </summary>
    internal static void DrawQueued(RenderContext context)
    {
        if (!Batches.TryGetValue(context, out var batch) || batch.Draws.Count == 0)
        {
            return;
        }

        batch.Particles.Upload();
        var gl = context.Gl;
        var state = context.State;
        gl.BindVertexArray(batch.EmptyVao);
        var program = context.GetProgram("Particle");
        var hexagons = context.GetProgram("ParticleDistortion");
        var copied = false;
        foreach (var draw in batch.Draws.OrderBy(draw => ParticleBlendModes.DrawOrder(draw.BlendMode)).ThenBy(draw => draw.Queued))
        {
            if (draw.BlendMode == ParticleBlendModes.Distortion)
            {
                // The game copies the frame once, the first time its shader is set up in the frame
                if (!copied && !CopyFrame(context, batch))
                {
                    break;
                }

                copied = true;
                DrawHexagons(context, batch, hexagons, draw);
                continue;
            }

            program.Use();
            var alphaAsColor = ApplyBlending(state, draw.BlendMode, draw.PageBlend);
            program.SetUniform(KnownUniform.AlphaAsColor, alphaAsColor);
            draw.Page.Bind(TextureUnit.Texture0);
            gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, 6, (uint)draw.Count, (uint)draw.First);
        }

        gl.BindVertexArray(0);
        batch.Draws.Clear();
        batch.Particles.Clear();
    }

    // Sets the GL state of a blend mode, true when the fragments have to hand their alpha over as the color
    private static bool ApplyBlending(GlStateCache state, Byte blendMode, TwinShader.AlphaBlendPresets pageBlend)
    {
        state.SetDepthTest(true);
        state.SetDepthFunc(DepthFunction.Lequal);
        if (blendMode == ParticleBlendModes.Cutout)
        {
            state.SetBlend(false);
            state.SetDepthMask(true);
            return false;
        }

        state.SetBlend(true);
        state.SetDepthMask(false);
        var preset = blendMode switch
        {
            ParticleBlendModes.Additive => TwinShader.AlphaBlendPresets.Add,
            ParticleBlendModes.Subtractive => TwinShader.AlphaBlendPresets.Sub,
            _ => pageBlend
        };
        return GsBlending.Apply(state, preset);
    }

    // The hexagons show what's already drawn behind them through a copy of the frame
    private static bool CopyFrame(RenderContext context, ParticleBatch batch)
    {
        var gl = context.Gl;
        var size = new ivec2((int)context.ViewportSize.x, (int)context.ViewportSize.y);
        if (size.x <= 0 || size.y <= 0)
        {
            return false;
        }

        if (batch.FrameCopy == null || batch.FrameCopySize != size)
        {
            batch.FrameCopy?.Dispose();
            batch.FrameCopy = new FrameBuffer(context, size);
            batch.FrameCopySize = size;
            var copy = batch.FrameCopy.TextureAttachment!;
            copy.Bind(TextureUnit.Texture0);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }

        gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, context.GetOutputBuffer());
        gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, batch.FrameCopy.Handler);
        gl.BlitFramebuffer(0, 0, size.x, size.y, 0, 0, size.x, size.y, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        gl.BindFramebuffer(FramebufferTarget.Framebuffer, context.GetOutputBuffer());
        return true;
    }

    private static void DrawHexagons(RenderContext context, ParticleBatch batch, ShaderProgram program, Draw draw)
    {
        var gl = context.Gl;
        var state = context.State;
        program.Use();
        var camera = context.FrameCamera;
        program.SetUniform(KnownUniform.StartProjection, camera.Projection);
        program.SetUniform(KnownUniform.StartView, camera.View);
        program.SetUniform(KnownUniform.InverseView, camera.World);
        program.SetUniform(KnownUniform.Distortion, draw.Distortion);
        batch.FrameCopy!.TextureAttachment!.Bind(TextureUnit.Texture0);
        // The shader normally blends, tests the depth for nearer than what's drawn and doesn't write it
        state.SetDepthTest(true);
        state.SetDepthFunc(DepthFunction.Less);
        state.SetDepthMask(false);
        state.SetBlend(true);
        GsBlending.Apply(state, TwinShader.AlphaBlendPresets.Mix);
        gl.DrawArraysInstancedBaseInstance(PrimitiveType.Triangles, 0, HexagonVertices, (uint)draw.Count, (uint)draw.First);
    }
}
