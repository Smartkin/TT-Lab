using System.Linq;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// A chunk's sky, drawn the way the game draws it (FUN_001ba350): its meshes in their own units around the camera, their parts painted
/// over each other in the order of the meshes and of their parts, behind everything. Only parts with the sky's shader (type 10) follow
/// the camera, the game's other programs take the model's matrix from where the sky's drawer never puts one
/// </summary>
public sealed class Skydome : Renderable
{
    public Skydome(RenderContext context, SkydomeData skydomeData, MeshService meshService, string name = "SKYDOME") : base(context, name)
    {
        var order = 0;
        foreach (var mesh in skydomeData.Meshes.Select(meshUri => meshService.GetMesh(meshUri).Model).OfType<Mesh>())
        {
            foreach (var model in mesh.GetModels())
            {
                model.DrawOrder = order++;
            }

            AddChild(mesh);
        }
    }
}
