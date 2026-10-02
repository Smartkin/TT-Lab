using System.Collections.Generic;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

public class DynamicScenery : Renderable
{
    private readonly List<DynamicSceneryMesh> _models = [];

    public DynamicScenery(RenderContext context, MeshService meshService, DynamicSceneryData dynamicSceneryData) : base(context, "DYNAMIC SCENERY")
    {
        foreach (var dynamicModel in dynamicSceneryData.DynamicModels)
        {
            var mesh = meshService.GetMesh(dynamicModel.Mesh);
            if (mesh.Model == null)
            {
                continue;
            }

            var bounds = dynamicModel.BoundingBox;
            var model = new DynamicSceneryMesh(context, mesh.Model, dynamicModel.Animation)
            {
                BoundsMin = bounds.Count > 1 ? new vec3(bounds[0].X, bounds[0].Y, bounds[0].Z) : vec3.Zero,
                BoundsMax = bounds.Count > 1 ? new vec3(bounds[1].X, bounds[1].Y, bounds[1].Z) : vec3.Zero
            };
            _models.Add(model);
            AddChild(model);
        }
    }

    public IReadOnlyList<DynamicSceneryMesh> Models => _models;
}
