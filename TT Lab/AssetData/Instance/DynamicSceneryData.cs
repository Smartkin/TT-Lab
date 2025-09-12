using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpGLTF.Animations;
using SharpGLTF.Schema2;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance.DynamicScenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class DynamicSceneryData : AbstractAssetData
{
    public DynamicSceneryData(IAsset asset) : base(asset)
    {
        DynamicModels = new List<DynamicSceneryModelData>();
    }

    public DynamicSceneryData(IAsset asset, ITwinDynamicScenery dynamicScenery) : this(asset)
    {
        SetTwinItem(dynamicScenery);
    }

    [JsonProperty(Required = Required.Always)]
    public List<DynamicSceneryModelData> DynamicModels { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        DynamicModels.Clear();
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        base.SaveInternal(dataPath, settings);
        
        var assetManager = AssetManager.Get();
        var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanityDynamicScenery_{Owner.Name}");
        var root = new SharpGLTF.Scenes.NodeBuilder("dynamic_scenery_root");

        foreach (var dynamicModel in DynamicModels)
        {
            var parentNode = root.CreateNode();
            var meshData = assetManager.GetAssetData<MeshData>(dynamicModel.Mesh);
            var model = assetManager.GetAssetData<ModelData>(meshData.Model);
            var meshes = model.GetMeshes(parentNode, meshData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
            foreach (var mesh in meshes)
            {
                scene.AddRigidMesh(mesh.Mesh, parentNode);
            }

            var animation = dynamicModel.GetAnimationSamples();
            parentNode.SetTranslationTrack("DYNAMIC_SCENERY_ANIMATION", animation.Select(key => key.Translation).CreateSampler());
            parentNode.SetRotationTrack("DYNAMIC_SCENERY_ANIMATION", animation.Select(key => key.Rotation).CreateSampler());
        }
        
        var resultModel = scene.ToGltf2();
        resultModel.SaveGLB(dataPath + ".glb");
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        base.LoadInternal(dataPath, settings);
        
        var assetManager = AssetManager.Get();
        var model = ModelRoot.Load(dataPath + ".glb");
        var modelId = 0;
        foreach (var node in model.LogicalNodes)
        {
            if (node.Name == "dynamic_scenery_root")
            {
                continue;
            }
            
            var modelMeshes = model.LogicalMeshes
                .Where(m => m.VisualParents.All(n => n.LogicalIndex == node.LogicalIndex)).ToList();
            var labModel = new Model
            {
                Package = Owner.Package,
                InvariantName = $"Model_{modelId++}_{Owner.Name}",
            };
            labModel.RegenerateLinks();
            assetManager.AddAssetUnsafe(labModel);
            var modelData = new ModelData(labModel);
            modelData.LoadFromGltfMeshes(modelMeshes);
            labModel.SetData(modelData);
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var dynamicScenery = GetTwinItem<ITwinDynamicScenery>();
        DynamicModels.Clear();
        foreach (var model in dynamicScenery.DynamicModels)
        {
            DynamicModels.Add(new DynamicSceneryModelData(Owner, model));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(65545); // Dynamic scenery header
        writer.Write((Int16)DynamicModels.Count);
        foreach (var model in DynamicModels)
        {
            model.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateDynamicScenery(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
        var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);

        foreach (var model in DynamicModels)
        {
            assetManager.GetAsset(model.Mesh).ResolveChunkResources(factory, meshSection);
        }

        return base.ResolveChunkResources(factory, section, id, layoutID);
    }
}