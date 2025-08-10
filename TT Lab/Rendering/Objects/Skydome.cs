using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Rendering.Services;

namespace TT_Lab.Rendering.Objects;

public class Skydome : Renderable
{
    // public Skydome(string name, SceneManager sceneManager, SkydomeData skydomeData) : base(name)
    // {
    //     var assetManager = AssetManager.Get();
    //     var skydomeNode = sceneManager.getRootSceneNode().createChildSceneNode("skydome");
    //     
    //     // foreach (var meshUri in skydomeData.Meshes)
    //     // {
    //     //     var mesh = assetManager.GetAssetData<RigidModelData>(meshUri);
    //     //     var meshNode = new ModelBuffer(sceneManager, skydomeNode, meshUri, mesh);
    //     //     foreach (var meshNodeMaterial in meshNode.MeshNodes)
    //     //     {
    //     //         meshNodeMaterial.MeshNode.getAttachedObject(0).setRenderQueueGroup((byte)RenderQueueGroupID.RENDER_QUEUE_SKIES_EARLY);
    //     //     }
    //     // }
    //
    //     var skydomeScale = 50;
    //     skydomeNode.scale(skydomeScale, skydomeScale, skydomeScale);
    //     skydomeNode.attachObject(this);
    // }
    
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