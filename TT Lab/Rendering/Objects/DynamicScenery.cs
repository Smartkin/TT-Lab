using TT_Lab.AssetData.Instance;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

public class DynamicScenery : Renderable
{
    public DynamicScenery(RenderContext context, MeshService meshService, DynamicSceneryData dynamicSceneryData) : base(context, "DYNAMIC SCENERY")
    {
        foreach (var dynamicModel in dynamicSceneryData.DynamicModels)
        {
            var mesh = meshService.GetMesh(dynamicModel.Mesh);
            if (mesh.Model != null)
            {
                AddChild(new DynamicSceneryMesh(context, mesh.Model, dynamicModel.Animation));
            }
        }
    }
}