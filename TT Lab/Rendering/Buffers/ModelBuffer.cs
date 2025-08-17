using System;
using System.Collections.Generic;
using System.Linq;
using Silk.NET.OpenGL;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering.Factories;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Passes;
using TT_Lab.Rendering.UniformDescs;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Buffers;

public class ModelBuffer(RenderContext context, ModelBufferBuild build, MaterialFactory materialFactory, MaterialData material)
{
    public event Action? MaterialReplaced;
    
    private readonly Dictionary<LabShader, TwinMaterial> _materials = [];
    private TwinMaterial? _currentRenderMaterial;
    private MaterialData _material = material;
    
    public uint IndexCount => build.IndicesAmount;

    public TwinMaterial? GetMaterial() => _currentRenderMaterial;

    private void InvalidateMaterials()
    {
        _materials.Clear();
    }

    public void ReplaceMaterial(MaterialData newMaterial)
    {
        _material = newMaterial;
        InvalidateMaterials();
        MaterialReplaced?.Invoke();
    }

    public (string, int)[] GetPriorityPass()
    {
        var result = new List<(string, int)>();
        var shaderIndex = 0;
        foreach (var shader in _material.Shaders)
        {
            var passName = shader.ShaderName;
            var priority = (int)(shader.UnkVector2.W - 128 + _material.DmaChainIndex + shaderIndex);
            result.Add((passName, priority));
            shaderIndex++;
        }
        return result.ToArray();
    }
    
    public virtual bool Bind()
    {
        var shader = GetShaderFromPass(context.CurrentPass);
        if (shader == null)
        {
            return false;
        }
        
        build.Vao.Bind();
        if (!_materials.TryGetValue(shader, out _currentRenderMaterial))
        {
            _currentRenderMaterial = new TwinMaterial(context, materialFactory.GetTwinMaterialFromShader(shader));
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
        return _material.Shaders.FirstOrDefault(s => s.ShaderName == passName);
    }
}