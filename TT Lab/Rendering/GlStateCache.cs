using System;
using Silk.NET.OpenGL;

namespace TT_Lab.Rendering;

// Reading GL state back makes the driver wait for the GPU, so the state the renderer changes is tracked here instead
public sealed class GlStateCache(GL gl)
{
    // A binding that isn't known: nothing compares equal to it, so the next bind is always made
    private const uint Unknown = uint.MaxValue;
    private const int TextureUnits = 32;

    // GL_TEXTURE_2D bound to each unit and the sampler of each unit, as bound through here (TextureBuffer binds through here too)
    private readonly uint[] _textures = CreateUnknown(TextureUnits);
    private readonly uint[] _samplers = CreateUnknown(TextureUnits);
    private int _activeTexture = -1;
    private uint _vertexArray = Unknown;
    // The texture indirect draws would have left on unit 0 had they bound theirs, bound before a draw that may read the unit
    private uint _pendingTexture0;
    private bool _hasPendingTexture0;
    private bool _blend;
    private bool _depthTest;
    private bool _depthMask;
    private bool _cullFace;
    private TriangleFace _culledFace;
    private DepthFunction _depthFunc;
    private BlendEquationModeEXT _blendEquation;
    private BlendingFactor _blendSrcRgb;
    private BlendingFactor _blendDstRgb;
    private BlendingFactor _blendSrcAlpha;
    private BlendingFactor _blendDstAlpha;
    private PolygonMode _polygonMode;
    // Red, green, blue and alpha writes as bits 0 to 3
    private int _colorMask;
    private StencilFunction _stencilFunc;
    private int _stencilRef;
    private StencilOp _stencilFail;
    private StencilOp _stencilDepthFail;
    private StencilOp _stencilPass;
    private uint _program;

    public bool Blend => _blend;
    public bool DepthMask => _depthMask;
    public DepthFunction DepthFunc => _depthFunc;

    /// <summary>
    /// How many materials have been bound so far, what tells a pass whether to put back the state its materials set
    /// </summary>
    public int MaterialBinds { get; set; }

    // Anything outside the renderer (resource creation) may have changed the state since the last frame
    public void Reset()
    {
        // Which textures the units hold is left as it is, only forgotten: a material without a texture draws with what unit 0 has
        ForgetBindings();
        // No sampler on unit 0 is what the particles sample their pages with
        BindSampler(0, 0);
        _blend = true;
        gl.Enable(EnableCap.Blend);
        _depthTest = true;
        gl.Enable(EnableCap.DepthTest);
        _depthMask = true;
        gl.DepthMask(true);
        _cullFace = false;
        gl.Disable(EnableCap.CullFace);
        // Only ever enabled for closed shapes, the scene itself is drawn without culling
        _culledFace = TriangleFace.Back;
        gl.CullFace(TriangleFace.Back);
        _depthFunc = DepthFunction.Lequal;
        gl.DepthFunc(DepthFunction.Lequal);
        _blendEquation = BlendEquationModeEXT.FuncAdd;
        gl.BlendEquation(BlendEquationModeEXT.FuncAdd);
        _blendSrcRgb = BlendingFactor.SrcAlpha;
        _blendDstRgb = BlendingFactor.OneMinusSrcAlpha;
        _blendSrcAlpha = BlendingFactor.One;
        _blendDstAlpha = BlendingFactor.OneMinusSrcAlpha;
        gl.BlendFuncSeparate(_blendSrcRgb, _blendDstRgb, _blendSrcAlpha, _blendDstAlpha);
        _polygonMode = PolygonMode.Fill;
        gl.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);
        _colorMask = 0xF;
        gl.ColorMask(true, true, true, true);
        // The stencil test is always on, passing everything and writing nothing unless a pass asks for it (PreviewPass)
        _stencilFunc = StencilFunction.Always;
        _stencilRef = 0;
        gl.StencilFunc(StencilFunction.Always, 0, 0xFF);
        _stencilFail = StencilOp.Keep;
        _stencilDepthFail = StencilOp.Keep;
        _stencilPass = StencilOp.Keep;
        gl.StencilOp(StencilOp.Keep, StencilOp.Keep, StencilOp.Keep);
        gl.StencilMask(0xFF);
        _program = 0;
        gl.UseProgram(0);
    }

    public void SetBlend(bool enabled)
    {
        if (_blend == enabled)
        {
            return;
        }

        _blend = enabled;
        if (enabled)
        {
            gl.Enable(EnableCap.Blend);
        }
        else
        {
            gl.Disable(EnableCap.Blend);
        }
    }

    public void SetDepthTest(bool enabled)
    {
        if (_depthTest == enabled)
        {
            return;
        }

        _depthTest = enabled;
        if (enabled)
        {
            gl.Enable(EnableCap.DepthTest);
        }
        else
        {
            gl.Disable(EnableCap.DepthTest);
        }
    }

    public void SetCullFace(bool enabled)
    {
        if (_cullFace == enabled)
        {
            return;
        }

        _cullFace = enabled;
        if (enabled)
        {
            gl.Enable(EnableCap.CullFace);
        }
        else
        {
            gl.Disable(EnableCap.CullFace);
        }
    }

    public void SetCulledFace(TriangleFace face)
    {
        if (_culledFace == face)
        {
            return;
        }

        _culledFace = face;
        gl.CullFace(face);
    }

    public void SetDepthMask(bool enabled)
    {
        if (_depthMask == enabled)
        {
            return;
        }

        _depthMask = enabled;
        gl.DepthMask(enabled);
    }

    public void SetDepthFunc(DepthFunction func)
    {
        if (_depthFunc == func)
        {
            return;
        }

        _depthFunc = func;
        gl.DepthFunc(func);
    }

    public void SetBlendEquation(BlendEquationModeEXT equation)
    {
        if (_blendEquation == equation)
        {
            return;
        }

        _blendEquation = equation;
        gl.BlendEquation(equation);
    }

    public void SetBlendFunc(BlendingFactor srcRgb, BlendingFactor dstRgb, BlendingFactor srcAlpha, BlendingFactor dstAlpha)
    {
        if (_blendSrcRgb == srcRgb && _blendDstRgb == dstRgb && _blendSrcAlpha == srcAlpha && _blendDstAlpha == dstAlpha)
        {
            return;
        }

        _blendSrcRgb = srcRgb;
        _blendDstRgb = dstRgb;
        _blendSrcAlpha = srcAlpha;
        _blendDstAlpha = dstAlpha;
        gl.BlendFuncSeparate(srcRgb, dstRgb, srcAlpha, dstAlpha);
    }

    public void SetColorMask(bool red, bool green, bool blue, bool alpha)
    {
        var mask = (red ? 1 : 0) | (green ? 2 : 0) | (blue ? 4 : 0) | (alpha ? 8 : 0);
        if (_colorMask == mask)
        {
            return;
        }

        _colorMask = mask;
        gl.ColorMask(red, green, blue, alpha);
    }

    public void SetPolygonMode(PolygonMode mode)
    {
        if (_polygonMode == mode)
        {
            return;
        }

        _polygonMode = mode;
        gl.PolygonMode(TriangleFace.FrontAndBack, mode);
    }

    public void SetStencil(StencilFunction func, int reference, StencilOp fail, StencilOp depthFail, StencilOp pass)
    {
        if (_stencilFunc != func || _stencilRef != reference)
        {
            _stencilFunc = func;
            _stencilRef = reference;
            gl.StencilFunc(func, reference, 0xFF);
        }

        if (_stencilFail != fail || _stencilDepthFail != depthFail || _stencilPass != pass)
        {
            _stencilFail = fail;
            _stencilDepthFail = depthFail;
            _stencilPass = pass;
            gl.StencilOp(fail, depthFail, pass);
        }
    }

    public void UseProgram(uint program)
    {
        if (_program == program)
        {
            return;
        }

        _program = program;
        gl.UseProgram(program);
    }

    /// <summary>
    /// Binds the texture to the unit and leaves the unit active, like glActiveTexture and glBindTexture do, skipping what's already so
    /// </summary>
    public void BindTexture(int unit, uint texture)
    {
        if (unit == 0)
        {
            _hasPendingTexture0 = false;
        }

        SetActiveTexture(unit);
        if (_textures[unit] == texture)
        {
            return;
        }

        _textures[unit] = texture;
        gl.BindTexture(TextureTarget.Texture2D, texture);
    }

    /// <summary>
    /// No texture on the active unit, which is all glBindTexture can unbind without picking a unit
    /// </summary>
    public void UnbindActiveTexture()
    {
        if (_activeTexture <= 0)
        {
            _hasPendingTexture0 = false;
        }

        gl.BindTexture(TextureTarget.Texture2D, 0);
        if (_activeTexture < 0)
        {
            ForgetBindings();
            return;
        }

        _textures[_activeTexture] = 0;
    }

    public void SetActiveTexture(int unit)
    {
        if (_activeTexture == unit)
        {
            return;
        }

        _activeTexture = unit;
        gl.ActiveTexture(TextureUnit.Texture0 + unit);
    }

    public void BindSampler(int unit, uint sampler)
    {
        if (_samplers[unit] == sampler)
        {
            return;
        }

        _samplers[unit] = sampler;
        gl.BindSampler((uint)unit, sampler);
    }

    /// <summary>
    /// A texture is going to be deleted: GL unbinds it from every unit, and its name can come back for a new texture
    /// </summary>
    public void ForgetTexture(uint texture)
    {
        if (_hasPendingTexture0 && _pendingTexture0 == texture)
        {
            _pendingTexture0 = 0;
        }

        for (var unit = 0; unit < TextureUnits; unit++)
        {
            if (_textures[unit] == texture)
            {
                _textures[unit] = 0;
            }
        }
    }

    /// <summary>
    /// What draws that sample their textures through handles leave on unit 0 for a later draw reading the unit, as if they had bound them
    /// </summary>
    public void SetPendingTexture0(uint texture)
    {
        _pendingTexture0 = texture;
        _hasPendingTexture0 = true;
    }

    /// <summary>
    /// Binds the texture indirect draws left pending on unit 0
    /// </summary>
    public void SettleTexture0()
    {
        if (!_hasPendingTexture0)
        {
            return;
        }

        BindTexture(0, _pendingTexture0);
    }

    public void BindVertexArray(uint vertexArray)
    {
        if (_vertexArray == vertexArray)
        {
            return;
        }

        _vertexArray = vertexArray;
        gl.BindVertexArray(vertexArray);
    }

    /// <summary>
    /// A vertex array is going to be deleted: GL binds 0 in its place when it's bound, and its name can come back for a new one
    /// </summary>
    public void ForgetVertexArray(uint vertexArray)
    {
        if (_vertexArray == vertexArray)
        {
            _vertexArray = Unknown;
        }
    }

    /// <summary>
    /// Makes the next texture, sampler and vertex array binds happen whatever was bound before, for when something else may have bound them
    /// </summary>
    public void ForgetBindings()
    {
        Array.Fill(_textures, Unknown);
        Array.Fill(_samplers, Unknown);
        _activeTexture = -1;
        _vertexArray = Unknown;
    }

    private static uint[] CreateUnknown(int count)
    {
        var values = new uint[count];
        Array.Fill(values, Unknown);
        return values;
    }
}
