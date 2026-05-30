using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

public sealed class Skydome : Renderable
{
    public Skydome(RenderContext context, SkydomeData skydomeData, MeshService meshService, string name = "SKYDOME") : base(context, name)
    {
        foreach (var mesh in skydomeData.Meshes.Select(meshUri => meshService.GetMesh(meshUri)))
        {
            if (mesh.Model != null)
            {
                AddChild(mesh.Model);
            }
        }
        
        Scale(new vec3(50));
    }
}