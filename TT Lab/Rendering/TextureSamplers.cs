using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering;

/// <summary>
/// GL's samplers for the GS's ways of reading a texture (<see cref="GsTextureFiltering"/>): the magnifying filter, the nearest mip filtered
/// bilinearly past it, and repeated or clamped coordinates. Materials bind one over their texture's own settings, on the render thread
/// </summary>
public sealed class TextureSamplers(RenderContext context) : IDisposable
{
    private readonly Dictionary<(bool Linear, bool Clamped), uint> _samplers = [];

    public uint Get(bool magnifiesLinearly, bool clampsCoordinates)
    {
        if (_samplers.TryGetValue((magnifiesLinearly, clampsCoordinates), out var sampler))
        {
            return sampler;
        }

        var gl = context.Gl;
        sampler = gl.GenSampler();
        var wrap = (int)(clampsCoordinates ? GLEnum.ClampToEdge : GLEnum.Repeat);
        gl.SamplerParameter(sampler, SamplerParameterI.MagFilter, (int)(magnifiesLinearly ? GLEnum.Linear : GLEnum.Nearest));
        gl.SamplerParameter(sampler, SamplerParameterI.MinFilter, (int)GLEnum.LinearMipmapNearest);
        gl.SamplerParameter(sampler, SamplerParameterI.WrapS, wrap);
        gl.SamplerParameter(sampler, SamplerParameterI.WrapT, wrap);
        _samplers[(magnifiesLinearly, clampsCoordinates)] = sampler;
        return sampler;
    }

    public void Dispose()
    {
        foreach (var sampler in _samplers.Values)
        {
            context.Gl.DeleteSampler(sampler);
        }

        _samplers.Clear();
    }
}
