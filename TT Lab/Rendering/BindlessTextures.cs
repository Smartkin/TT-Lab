using System;
using System.Collections.Generic;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering;

/// <summary>
/// ARB_bindless_texture, what lets the batches of a pass be drawn in one indirect draw with each its texture (<see cref="IndirectDraws"/>):
/// a texture and a sampler become a handle the shader samples through. Only used where a handle may differ between the fragments of one
/// shader invocation group (NV_gpu_shader5, NVIDIA's drivers): the draws of one indirect draw can share a group, everywhere else that's
/// undefined
/// </summary>
public sealed unsafe class BindlessTextures
{
    private readonly delegate* unmanaged<uint, uint, ulong> _getTextureSamplerHandle;
    private readonly delegate* unmanaged<ulong, void> _makeResident;
    private readonly delegate* unmanaged<ulong, void> _makeNonResident;

    private BindlessTextures(GL gl)
    {
        _getTextureSamplerHandle = (delegate* unmanaged<uint, uint, ulong>)gl.Context.GetProcAddress("glGetTextureSamplerHandleARB");
        _makeResident = (delegate* unmanaged<ulong, void>)gl.Context.GetProcAddress("glMakeTextureHandleResidentARB");
        _makeNonResident = (delegate* unmanaged<ulong, void>)gl.Context.GetProcAddress("glMakeTextureHandleNonResidentARB");
    }

    /// <summary>
    /// The context's bindless textures, none when its driver can't give every draw of an indirect draw a texture of its own
    /// </summary>
    public static BindlessTextures? Of(GL gl)
    {
        var extensions = new HashSet<string>();
        var count = gl.GetInteger(GetPName.NumExtensions);
        for (var i = 0; i < count; i++)
        {
            extensions.Add(gl.GetStringS(StringName.Extensions, (uint)i));
        }

        if (!extensions.Contains("GL_ARB_bindless_texture") || !extensions.Contains("GL_NV_gpu_shader5"))
        {
            return null;
        }

        var bindless = new BindlessTextures(gl);
        return bindless._getTextureSamplerHandle == null || bindless._makeResident == null || bindless._makeNonResident == null ? null : bindless;
    }

    /// <summary>
    /// A resident handle of the texture sampled with the sampler, which leaves both as they are for good
    /// </summary>
    public ulong MakeHandle(uint texture, uint sampler)
    {
        var handle = _getTextureSamplerHandle(texture, sampler);
        if (handle != 0)
        {
            _makeResident(handle);
        }

        return handle;
    }

    /// <summary>
    /// Before the texture is deleted
    /// </summary>
    public void Release(ulong handle)
    {
        _makeNonResident(handle);
    }
}
