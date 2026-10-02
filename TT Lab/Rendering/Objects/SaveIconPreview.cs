using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Rendering.Buffers;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using NVector3 = System.Numerics.Vector3;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// The save icon's viewer: its triangles upright the way its model file has them, with its texture times the corners' colors (bytes,
/// 255 the texture as it is), in the frame of its animation the editor's animation panel plays (SaveIconAnimationViewModel). Frames are
/// what PS2IODB's player makes of them, whose timing was compared with the PS2 BIOS: every shape times its weight over the weights'
/// sum. The icon's texture is no asset, the texture service gets it under the icon's URI
/// </summary>
public sealed class SaveIconPreview : Renderable
{
    /// <summary>
    /// The browser plays this many of the animation's frames a second times the icon's speed, over and over
    /// </summary>
    public const double FramesPerSecond = 60.0;

    private readonly PS2SaveIcon _icon;
    // Only the render thread changes and uploads them
    private readonly float[] _vertexData;
    private readonly BufferObject<float> _vertexBuffer;
    private int _shownFrame;

    public SaveIconPreview(RenderContext context, LabURI icon, PS2SaveIcon saveIcon) : base(context, "SAVE_ICON_PREVIEW")
    {
        _icon = saveIcon;
        context.TextureService.SetTexture(icon, GetTexture(saveIcon), PS2SaveIcon.TextureSize, PS2SaveIcon.TextureSize);
        var material = new MaterialData(null!);
        var shader = material.Shaders[0];
        shader.ShaderType = TwinShader.Type.StandardUnlit;
        shader.TxtMapping = TwinShader.TextureMapping.ON;
        shader.TextureId = icon;
        var vertexes = GetVertexes(saveIcon);
        var faces = Enumerable.Range(0, vertexes.Count / 3).Select(face => new IndexedFace { Indexes = [face * 3, face * 3 + 1, face * 3 + 2] }).ToList();
        var build = context.MeshBuilder.BuildRigidVaoFromVertexes(vertexes, faces, dynamic: true);
        _vertexData = build.VertexData!;
        _vertexBuffer = build.VertexBuffer!;
        AddChild(new Mesh(context, [new ModelBuffer(context, build, context.MaterialFactory, material)]));
    }

    /// <summary>
    /// Shows the icon as the frame of its animation has it, called on the UI thread
    /// </summary>
    public void ShowFrame(int frame)
    {
        if (frame == _shownFrame)
        {
            return;
        }

        _shownFrame = frame;
        Context.QueueRenderAction(() => UploadFrame(frame));
    }

    private void UploadFrame(int frame)
    {
        var weights = ShapeWeightsAt(_icon, frame);
        var corners = _icon.Vertexes.Count / 3 * 3;
        for (var corner = 0; corner < corners; corner++)
        {
            var position = PositionAt(_icon.Vertexes[corner], weights);
            var at = corner * MeshBuilder.RigidVertexFloats;
            _vertexData[at] = position.X;
            _vertexData[at + 1] = position.Y;
            _vertexData[at + 2] = position.Z;
        }

        _vertexBuffer.BufferData(_vertexData);
    }

    /// <summary>
    /// A frame's weight at the time: its keys joined by straight lines, the first and last weight held before and after them
    /// </summary>
    public static float WeightAt(SaveIconFrame frame, float time)
    {
        var keys = frame.Keys;
        if (keys.Count == 0)
        {
            return 0.0f;
        }

        if (time <= keys[0].Time)
        {
            return keys[0].Value;
        }

        for (var i = 1; i < keys.Count; i++)
        {
            var (start, end) = (keys[i - 1], keys[i]);
            if (start.Time <= time && time < end.Time)
            {
                return start.Value + (end.Value - start.Value) * (time - start.Time) / (end.Time - start.Time);
            }
        }

        return keys[^1].Value;
    }

    /// <summary>
    /// Every shape's share at the frame: the weights of the frames naming it over all their weights, the first shape alone while they
    /// add up to nothing
    /// </summary>
    public static float[] ShapeWeightsAt(PS2SaveIcon icon, int frame)
    {
        var weights = new float[Math.Max(icon.ShapeCount, 1)];
        var sum = 0.0f;
        foreach (var animationFrame in icon.Frames.Where(animationFrame => animationFrame.Shape < weights.Length))
        {
            var weight = WeightAt(animationFrame, frame);
            weights[animationFrame.Shape] += weight;
            sum += weight;
        }

        if (MathF.Abs(sum) < 1e-6f || !float.IsFinite(sum))
        {
            Array.Clear(weights);
            weights[0] = 1.0f;
            return weights;
        }

        for (var shape = 0; shape < weights.Length; shape++)
        {
            weights[shape] /= sum;
        }

        return weights;
    }

    /// <summary>
    /// Where the corner is with the shapes' shares, in the file's units with Y up
    /// </summary>
    public static NVector3 PositionAt(SaveIconVertex corner, float[] weights)
    {
        var position = NVector3.Zero;
        for (var shape = 0; shape < weights.Length && (shape + 1) * 4 <= corner.Positions.Length; shape++)
        {
            if (weights[shape] != 0.0f)
            {
                position += SaveIconTlm.PositionOf(corner, shape) * weights[shape];
            }
        }

        return position;
    }

    /// <summary>
    /// The corners of the icon's triangles as its animation's first frame has them
    /// </summary>
    public static List<Vertex> GetVertexes(PS2SaveIcon icon)
    {
        var weights = ShapeWeightsAt(icon, 0);
        return icon.Vertexes.Take(icon.Vertexes.Count / 3 * 3).Select(corner =>
        {
            var position = PositionAt(corner, weights);
            var normal = SaveIconTlm.NormalOf(corner);
            var uv = SaveIconTlm.UvOf(corner);
            var color = new Vector4((corner.Color & 0xFF) / 255.0f, (corner.Color >> 8 & 0xFF) / 255.0f, (corner.Color >> 16 & 0xFF) / 255.0f, (corner.Color >> 24) / 255.0f);
            return new Vertex(new Vector4(position.X, position.Y, position.Z, 1.0f), color, new Vector4(uv.X, uv.Y, 0.0f, 0.0f))
            {
                Normal = new Vector4(normal.X, normal.Y, normal.Z, 0.0f)
            };
        }).ToList();
    }

    /// <summary>
    /// The corner and the far corner of the box around every shape, where the animation stays
    /// </summary>
    public static (vec3 Min, vec3 Max) GetBounds(PS2SaveIcon icon)
    {
        if (icon.Vertexes.Count == 0)
        {
            return (vec3.Zero, vec3.Zero);
        }

        var min = new vec3(float.MaxValue);
        var max = new vec3(float.MinValue);
        foreach (var corner in icon.Vertexes)
        {
            for (var shape = 0; shape < Math.Max(icon.ShapeCount, 1) && (shape + 1) * 4 <= corner.Positions.Length; shape++)
            {
                var position = SaveIconTlm.PositionOf(corner, shape);
                var point = new vec3(position.X, position.Y, position.Z);
                min = vec3.Min(min, point);
                max = vec3.Max(max, point);
            }
        }

        return (min, max);
    }

    // Blue, green, red and alpha, what the texture service uploads
    private static byte[] GetTexture(PS2SaveIcon icon)
    {
        var pixels = new byte[PS2SaveIcon.TexelCount * 4];
        for (var i = 0; i < PS2SaveIcon.TexelCount && i < icon.Texture.Length; i++)
        {
            var (r, g, b, a) = PS2SaveIcon.ToRgba(icon.Texture[i]);
            pixels[i * 4] = b;
            pixels[i * 4 + 1] = g;
            pixels[i * 4 + 2] = r;
            pixels[i * 4 + 3] = a;
        }

        return pixels;
    }
}
