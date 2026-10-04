using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.Services;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Buffers;

public class ModelBuffer(RenderContext context, ModelBufferBuild build, MaterialFactory materialFactory, MaterialData material)
{
    public event Action? MaterialReplaced;
    
    private readonly Dictionary<LabShader, TwinMaterial> _materials = [];
    // Shader names get built on every access, looking them up for every draw adds up
    private readonly Dictionary<string, LabShader?> _passShaders = [];
    private TwinMaterial? _currentRenderMaterial;
    private MaterialData _material = material;
    private ModelBufferBuild _build = build;
    
    public uint IndexCount => _build.IndicesAmount;

    /// <summary>
    /// Shades the part flat for the editor, whatever its shaders say
    /// </summary>
    public bool EditorShading { get; init; }

    public TwinMaterial? GetMaterial() => _currentRenderMaterial;

    private bool? _isLit;

    /// <summary>
    /// Whether any of the material's shaders is lit by the game's lights, its meshes then get their object's
    /// </summary>
    public bool IsLit => _isLit ??= _material.Shaders.Any(shader => MaterialFactory.IsLit(shader.ShaderType));
    public VertexArrayObject<float, uint> GetVertexArrayObject() => _build.Vao;

    /// <summary>
    /// Draws the new vertexes from now on, every mesh drawing the buffer with it (an edited collision), and frees the old. On the render
    /// thread
    /// </summary>
    public void ReplaceBuild(ModelBufferBuild newBuild)
    {
        var old = _build;
        _build = newBuild;
        old.Release();
    }

    private void InvalidateMaterials()
    {
        _materials.Clear();
        _passShaders.Clear();
    }

    public void ReplaceMaterial(MaterialData newMaterial)
    {
        _material = newMaterial;
        _isLit = null;
        InvalidateMaterials();
        MaterialReplaced?.Invoke();
    }

    // Where the part draws among the skydome's parts, which the game paints over each other in their order
    public int DrawOrder { get; set; }

    public (string, int)[] GetPriorityPass()
    {
        var result = new List<(string, int)>();
        var shaderIndex = 0;
        foreach (var shader in _material.Shaders)
        {
            var passName = PassOf(shader);
            var priority = passName == PassService.SkydomePassName
                ? (DrawOrder << 8) + shaderIndex
                : (int)(-shader.ShaderColor.W + _material.DmaChainIndex + shaderIndex);
            result.Add((passName, priority));
            shaderIndex++;
        }
        return result.ToArray();
    }

    // The sky's program (type 10, VU1 code 0x2e3ec0) never reads its model's matrix, only the one the sky's drawer leaves in VU memory
    // (FUN_001c1f90), so the game draws a part of that type around the camera whatever model it's on. They all go in one pass, blended or not
    private static string PassOf(LabShader shader)
    {
        return shader.ShaderType == TwinShader.Type.UnlitSkydome && shader.ForcedShaderName == null ? PassService.SkydomePassName : shader.ShaderName;
    }
    
    public virtual bool Bind()
    {
        var shader = GetShaderFromPass(context.CurrentPass);
        if (shader == null)
        {
            return false;
        }
        
        _build.Vao.Bind();
        if (!_materials.TryGetValue(shader, out _currentRenderMaterial))
        {
            var desc = materialFactory.GetTwinMaterialFromShader(shader);
            _currentRenderMaterial = new TwinMaterial(context, EditorShading ? desc with { EditorShading = true } : desc);
            _materials[shader] = _currentRenderMaterial;
        }
        _currentRenderMaterial.Bind();

        return true;
    }

    public void Unbind()
    {
        _currentRenderMaterial?.Unbind();
    }

    private LabShader? GetShaderFromPass(RenderPass renderPass)
    {
        var passName = renderPass.Name;
        if (_passShaders.TryGetValue(passName, out var shader))
        {
            return shader;
        }

        shader = _material.Shaders.FirstOrDefault(s => PassOf(s) == passName);
        _passShaders[passName] = shader;
        return shader;
    }
}