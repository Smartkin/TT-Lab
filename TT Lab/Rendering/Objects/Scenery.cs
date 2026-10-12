using GlmSharp;
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
            Renderable? model;
            if (placement.IsLod)
            {
                // The LOD's mesh of the camera's distance
                var lod = assetManager.GetAssetData<LodModelData>(placement.Model);
                if (lod.Meshes.Count == 0)
                {
                    continue;
                }

                var min = new vec3(placement.Box.V1.X, placement.Box.V1.Y, placement.Box.V1.Z);
                var max = new vec3(placement.Box.V2.X, placement.Box.V2.Y, placement.Box.V2.Z);
                model = new LodVisual(context, meshService, lod, (min + max) * 0.5f);
            }
            else
            {
                model = meshService.GetMesh(placement.Model, true).Model;
            }

            if (model == null)
            {
                continue;
            }

            model.SetLocalTransform(placement.Matrix.ToGlm());
            AddChild(model);
        }
    }
}
