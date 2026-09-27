using Silk.NET.OpenGL;

namespace TT_Lab.Rendering;

// Reading GL state back makes the driver wait for the GPU, so the state the renderer changes is tracked here instead
public sealed class GlStateCache(GL gl)
{
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
    private uint _program;

    public bool Blend => _blend;
    public bool DepthMask => _depthMask;
    public DepthFunction DepthFunc => _depthFunc;

    // Anything outside the renderer (ImGui, resource creation) may have changed the state since the last frame
    public void Reset()
    {
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

    public void SetPolygonMode(PolygonMode mode)
    {
        if (_polygonMode == mode)
        {
            return;
        }

        _polygonMode = mode;
        gl.PolygonMode(TriangleFace.FrontAndBack, mode);
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
}
