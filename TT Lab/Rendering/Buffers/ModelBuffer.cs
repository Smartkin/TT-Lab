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
    private TwinMaterial? _currentRenderMaterial;
    private MaterialData _material = material;
    private ModelBufferBuild _build = build;
    
    public uint IndexCount => _build.IndicesAmount;

    /// <summary>
    /// The first vertex of the model's draws, where its vertexes start in the context's VertexPool
    /// </summary>
    public int FirstVertex => _build.FirstVertex;

    /// <summary>
    /// Shades the part flat for the editor, whatever its shaders say
    /// </summary>
    public bool EditorShading { get; init; }

    /// <summary>
    /// Whether the part's triangles lie in one plane, where none lies over another on screen (<see cref="MeshBuilder.IsFlat"/>)
    /// </summary>
    public bool IsFlat => _build.Flat;

    public int DataHash => _build.DataHash;

    /// <summary>
    /// The game's model the part is of, the same for the parts of every placement of a model the build makes one of
    /// (<see cref="SceneOrder"/>), none for TT Lab's own models
    /// </summary>
    public int? GameModel { get; set; }

    /// <summary>
    /// The part's place among its model's parts
    /// </summary>
    public int PartIndex { get; set; }

    public TwinMaterial? GetMaterial() => _currentRenderMaterial;

    private bool? _isLit;

    /// <summary>
    /// Whether any of the material's shaders is lit by the game's lights, its meshes then get their object's
    /// </summary>
    public bool IsLit => _isLit ??= _material.Shaders.Any(shader => MaterialFactory.IsLit(shader.ShaderType));

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

    /// <summary>
    /// Whether what the model was drawn from was let go of (<see cref="Release"/>): nothing may draw it any more
    /// </summary>
    public bool IsReleased { get; private set; }

    /// <summary>
    /// Frees the model's vertexes, its range of the context's vertex pool or its own buffers, once nothing draws it. On the render thread
    /// </summary>
    public void Release()
    {
        if (IsReleased)
        {
            return;
        }

        IsReleased = true;
        _build.Release();
    }

    private void InvalidateMaterials()
    {
        _materials.Clear();
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

    /// <summary>
    /// The material the model draws with, which its draws are ordered by (SceneOrder)
    /// </summary>
    public MaterialData Material => _material;

    /// <summary>
    /// The game's render bucket the material's draws go in (RenderBuckets)
    /// </summary>
    public uint Bucket => _material.DmaChainIndex;

    public int ShaderCount => _material.Shaders.Count;

    /// <summary>
    /// Whether a shader of the material draws a copy of the frame (the reflection surface), taken as the material starts drawing
    /// </summary>
    public bool ReadsScreenCopy => _material.Shaders.Any(shader => shader.ShaderType == TwinShader.Type.LitReflectionSurface);

    /// <summary>
    /// The pass a shader of the material names for itself, none for the scene's draws: the sky's for the sky's type, or its forced pass
    /// </summary>
    public string? NamedPassOf(int shader)
    {
        var labShader = _material.Shaders[shader];
        // The sky's program (type 10, VU1 code 0x2e3ec0) never reads its model's matrix, only the one the sky's drawer leaves in VU memory
        // (FUN_001c1f90), so the game draws a part of that type around the camera whatever model it's on. They all go in one pass, blended or not
        if (labShader.ForcedShaderName == null && labShader.ShaderType == TwinShader.Type.UnlitSkydome)
        {
            return PassService.SkydomePassName;
        }

        return labShader.ForcedShaderName;
    }

    /// <summary>
    /// Where a shader of a sky's part draws in the sky's pass: by the part's place among the sky's and then the shader's
    /// </summary>
    public int SkyPriorityOf(int shader) => (DrawOrder << 8) + shader;

    /// <summary>
    /// Binds the model with a shader of its material
    /// </summary>
    public virtual bool Bind(int shader)
    {
        var material = MaterialOf(shader);
        if (material == null)
        {
            return false;
        }

        context.State.BindVertexArray(_build.VertexArray);
        _currentRenderMaterial = material;
        material.Bind();
        return true;
    }

    /// <summary>
    /// The material a shader of the model's material draws with, none past its shaders
    /// </summary>
    public TwinMaterial? MaterialOf(int shader)
    {
        if (shader < 0 || shader >= _material.Shaders.Count)
        {
            return null;
        }

        var labShader = _material.Shaders[shader];
        if (!_materials.TryGetValue(labShader, out var material))
        {
            var desc = materialFactory.GetTwinMaterialFromShader(labShader, _material.DmaChainIndex);
            material = new TwinMaterial(context, EditorShading ? desc with { EditorShading = true, Fog = TwinMaterialDesc.NoFog } : desc);
            _materials[labShader] = material;
        }

        return material;
    }

    /// <summary>
    /// Whether the model is drawn from the context's vertex pool, through its vertex array like every model that is
    /// </summary>
    public bool IsPooled => _build.Pool != null;

    public void Unbind()
    {
        _currentRenderMaterial?.Unbind();
    }
}