using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Scene;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

public class Scenery : Renderable
{
    public Scenery(RenderContext context, MeshService meshService, SceneryData sceneryData) : base(context)
    {
        var assetManager = AssetManager.Get();
        foreach (var placement in sceneryData.Placements)
        {
            var model = placement.Model;
            if (placement.IsLod)
            {
                // The LOD as it's drawn up close
                var lod = assetManager.GetAssetData<LodModelData>(placement.Model);
                if (lod.Meshes.Count == 0)
                {
                    continue;
                }

                model = lod.Meshes[0];
            }

            var mesh = meshService.GetMesh(model, true);
            if (mesh.Model == null)
            {
                continue;
            }

            mesh.Model.SetLocalTransform(placement.Matrix.ToGlm());
            AddChild(mesh.Model);
        }
    }
}
