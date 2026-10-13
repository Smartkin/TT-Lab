using Silk.NET.OpenGL;
using TT_Lab.Rendering.Shaders;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Materials;

/// <summary>
/// A shader of a material as the Generic program draws it: its values are an entry of the context's <see cref="MaterialTable"/>, its
/// texture, sampler, blending and depth are GL state set through the state cache. What a pass's materials leave set is put back once at
/// the pass's end (<see cref="Renderer"/>), not after every draw
/// </summary>
public class TwinMaterial(RenderContext context, TwinMaterialDesc materialDesc) : Material
{
    private readonly TwinMaterialDesc _materialDesc = materialDesc;
    private int _entry;
    private int _generation = -1;
    private uint _sampler;
    private ulong _handle;
    private int _handleGeneration = -1;

    private ShaderProgram Program => context.CurrentPass.Program;

    /// <summary>
    /// The material's entry in the context's material table
    /// </summary>
    public int Entry
    {
        get
        {
            Resolve();
            return _entry;
        }
    }

    public bool HasTexture => _materialDesc.Texture != null;

    /// <summary>
    /// Whether the fragments failing the alpha test write something else than the passing ones (<see cref="AlphaFailDraw.Apart"/>): every
    /// instance is drawn twice then (<see cref="BeginPassing"/>, <see cref="BeginFailing"/>), never in an indirect draw
    /// </summary>
    public bool FailsApart => _materialDesc.FailDraw == AlphaFailDraw.Apart;

    public Buffers.TextureBuffer? Texture => _materialDesc.Texture;

    /// <summary>
    /// The texture with the material's sampler as a bindless handle, 0 without one (no bindless textures, no texture or a deleted one)
    /// </summary>
    public ulong TextureHandle
    {
        get
        {
            Resolve();
            var texture = _materialDesc.Texture;
            if (texture == null || context.Bindless is not { } bindless || texture.Handler == 0)
            {
                return 0;
            }

            if (_handleGeneration != texture.Generation)
            {
                _handle = texture.GetHandle(bindless, _sampler);
                _handleGeneration = texture.Generation;
            }

            return _handle;
        }
    }

    private void Resolve()
    {
        var table = context.Materials;
        if (_generation == table.Generation)
        {
            return;
        }

        _entry = table.IndexOf(_materialDesc);
        _generation = table.Generation;
        // The GS's filter and coordinates over the texture's own settings (GsTextureFiltering)
        _sampler = context.Samplers.Get(_materialDesc.MagnifiesLinearly, _materialDesc.ClampsCoordinates);
    }

    // The GS tests the depth whether it writes it or not
    private DepthFunction DepthFunction => _materialDesc.DepthTest switch
    {
        TwinShader.DepthTestMethod.NEVER => DepthFunction.Never,
        TwinShader.DepthTestMethod.ALWAYS => DepthFunction.Always,
        TwinShader.DepthTestMethod.GREATER => DepthFunction.Less,
        // GEQUAL, and the test of a byte past the four methods, which drew with the Lequal every material put back after its draw
        _ => DepthFunction.Lequal,
    };

    private bool Blends => _materialDesc.AlphaBlend.Equals(1.0f);

    /// <summary>
    /// Whether the other material draws with the same GL state, blending and depth, what lets their draws be one indirect draw
    /// </summary>
    public bool DrawsLike(TwinMaterial other)
    {
        var desc = other._materialDesc;
        if (Blends != other.Blends || _materialDesc.DepthWrite != desc.DepthWrite || DepthFunction != other.DepthFunction)
        {
            return false;
        }

        if (!Blends)
        {
            return true;
        }

        if (_materialDesc.CustomBlend != desc.CustomBlend)
        {
            return false;
        }

        return _materialDesc.CustomBlend
            ? _materialDesc.BlendA == desc.BlendA && _materialDesc.BlendB == desc.BlendB && _materialDesc.BlendC == desc.BlendC && _materialDesc.BlendD == desc.BlendD
              && _materialDesc.BlendFix == desc.BlendFix
            : _materialDesc.BlendFunc == desc.BlendFunc;
    }

    /// <summary>
    /// The GL state the material draws with: blending and depth
    /// </summary>
    public void ApplyState()
    {
        var state = context.State;
        state.MaterialBinds++;
        var blends = Blends;
        state.SetBlend(blends);
        if (blends)
        {
            ApplyBlending();
        }

        state.SetDepthMask(_materialDesc.DepthWrite);
        state.SetDepthFunc(DepthFunction);
    }

    private void ApplyBlending()
    {
        if (_materialDesc.CustomBlend)
        {
            GsBlending.ApplyFormula(context, _materialDesc.BlendA, _materialDesc.BlendB, _materialDesc.BlendC, _materialDesc.BlendD, _materialDesc.BlendFix);
            return;
        }

        // The material shader hands over its color, so Brighten brightens what's behind by the color rather than the alpha. No retail
        // material blends with it
        GsBlending.Apply(context.State, _materialDesc.BlendFunc);
    }

    public override void Bind()
    {
        Resolve();
        // Without a texture the unit keeps the one drawn before, which the shader still reads for environment maps
        _materialDesc.Texture?.Bind();
        context.State.BindSampler(0, _sampler);
        Program.SetMaterialEntry(_entry);
        ApplyState();
    }

    /// <summary>
    /// A draw of the fragments passing the alpha test, with the material's own writes
    /// </summary>
    public void BeginPassing()
    {
        var state = context.State;
        state.SetColorMask(true, true, true, true);
        state.SetDepthMask(_materialDesc.DepthWrite);
        Program.SetAlphaSubpass(1);
    }

    /// <summary>
    /// A draw of the fragments failing the alpha test, with what their AFAIL writes of the material's writes
    /// </summary>
    public void BeginFailing()
    {
        var (color, alpha, depth) = GsAlphaTest.FailedWrites(_materialDesc.AlphaFail, _materialDesc.DepthWrite);
        var state = context.State;
        state.SetColorMask(color, color, color, alpha);
        state.SetDepthMask(depth);
        Program.SetAlphaSubpass(2);
    }

    /// <summary>
    /// Back to the draws of every fragment with the material's writes
    /// </summary>
    public void EndSubpasses()
    {
        var state = context.State;
        state.SetColorMask(true, true, true, true);
        state.SetDepthMask(_materialDesc.DepthWrite);
        Program.SetAlphaSubpass(0);
    }

    public override void Unbind()
    {
    }
}
