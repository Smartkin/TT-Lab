using System.Linq;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Rendering.Factories;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// The material editor's preview: a sphere drawn with the material the way the game's models are, its shaders, blending, scrolling,
/// animation and deformation included
/// </summary>
public sealed class MaterialPreview : Renderable
{
    private readonly MaterialData _material;
    private Mesh _mesh;
    private bool _isLit;

    public MaterialPreview(RenderContext context, MaterialData material) : base(context, "MATERIAL_PREVIEW")
    {
        _material = material;
        _isLit = IsLit();
        _mesh = context.MeshFactory.CreateMaterialPreview(material);
        AddChild(_mesh);
    }

    private bool IsLit() => _material.Shaders.Any(shader => MaterialFactory.IsLit(shader.ShaderType));

    /// <summary>
    /// Shows the material as it is now, on the render thread, which draws it: its shaders' looks made again (the batch moves to the passes
    /// they draw in), a new sphere when it turned lit or unlit
    /// </summary>
    public void Refresh()
    {
        Context.QueueRenderAction(() =>
        {
            if (IsLit() == _isLit)
            {
                _mesh.GetModels()[0].ReplaceMaterial(_material);
                return;
            }

            _isLit = !_isLit;
            RemoveChild(_mesh);
            _mesh = Context.MeshFactory.CreateMaterialPreview(_material);
            AddChild(_mesh);
        });
    }
}
