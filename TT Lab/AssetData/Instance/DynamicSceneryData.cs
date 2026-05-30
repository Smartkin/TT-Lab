using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SharpGLTF.Animations;
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

public class DynamicSceneryData : AbstractAssetData
{
    public DynamicSceneryData(IAsset asset) : base(asset)
    {
        DynamicModels = [];
    }

    public DynamicSceneryData(IAsset asset, ITwinDynamicScenery dynamicScenery) : this(asset)
    {
        SetTwinItem(dynamicScenery);
    }

    public List<DynamicSceneryModelData> DynamicModels { get; set; }

    public SharpGLTF.Scenes.NodeBuilder GetInGltfFormat(SharpGLTF.Scenes.SceneBuilder scene)
    {
        var assetManager = AssetManager.Get();
        var root = new SharpGLTF.Scenes.NodeBuilder("DYNAMIC_SCENERY_ROOT");
        var materialDescs = root.CreateNode($"DYNAMIC_SCENERY_MATERIAL_DESCS");

        var modelIdx = 0;
        foreach (var dynamicModel in DynamicModels)
        {
            var parentNode = root.CreateNode($"DYNAMIC_SCENERY_MESH_{modelIdx}");
            parentNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(dynamicModel);
            
            var meshData = assetManager.GetAssetData<MeshData>(dynamicModel.Mesh);
            var model = assetManager.GetAssetData<ModelData>(meshData.Model);
            var meshes = model.GetMeshes(parentNode, meshData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
            foreach (var mesh in meshes)
            {
                mesh.Mesh.Name = $"{parentNode.Name}_{mesh.Mesh.Name.Replace("RIGIDIDPLACEHOLDER", modelIdx.ToString())}";
                scene.AddRigidMesh(mesh.Mesh, parentNode);
            }

            var submodelIdx = 0;
            foreach (var subvertex in model.Vertexes)
            {
                var material = assetManager.GetAssetData<MaterialData>(meshData.Materials[submodelIdx]);
                var materialDescNode = new SharpGLTF.Scenes.NodeBuilder
                {
                    Extras = material.GetJsonFormat(),
                    Name = $"DYNAMIC_SCENERY_MATERIAL_DESC_{modelIdx}_{submodelIdx}_{material.Name}"
                };
                
                materialDescs.AddNode(materialDescNode);

                submodelIdx++;
            }

            var animation = dynamicModel.GetAnimationSamples();
            parentNode.SetTranslationTrack("DYNAMIC_SCENERY_ANIMATION", animation.Select(key => key.Translation).CreateSampler());
            parentNode.SetRotationTrack("DYNAMIC_SCENERY_ANIMATION", animation.Select(key => key.Rotation).CreateSampler());

            modelIdx++;
        }

        return root;
    }

    public void LoadFromGltf(SharpGLTF.Schema2.ModelRoot gltfModel, SharpGLTF.Schema2.Node rootNode)
    {
        var assetManager = AssetManager.Get();
        var materialDescs = rootNode.VisualChildren.FirstOrDefault(n => n is {Name: "DYNAMIC_SCENERY_MATERIAL_DESCS"});
        if (materialDescs == null)
        {
            Log.WriteLine("Dynamic scenery meshes missing material descs! Empty materials will be used!", Log.LogType.Warning);
        }

        DynamicModels = [];
        
        var dynamicModelIdx = 0;
        foreach (var dynamicModelNode in rootNode.VisualChildren)
        {
            if (dynamicModelNode == materialDescs)
            {
                continue;
            }

            var model = new Model
            {
                Package = Owner.Package,
                InvariantName = $"{Owner.Chunk}_{dynamicModelNode.Name}_DYNAMIC_MODEL",
                Alias = $"{dynamicModelNode.Name}_DYNAMIC_MODEL",
                IsInternal = true
            };

            var meshesGltf = gltfModel.LogicalMeshes.Where(m => m.Name.StartsWith($"{dynamicModelNode.Name}_mesh_{dynamicModelIdx}_")).ToList();
            var modelData = new ModelData(model);
            modelData.LoadFromGltfMeshes(meshesGltf);
            model.SetData(modelData);
            
            assetManager.AddAsset(model);

            var mesh = new Mesh
            {
                Package = Owner.Package,
                InvariantName = $"{Owner.Chunk}_{dynamicModelNode.Name}_DYNAMIC_MESH",
                Alias = $"{dynamicModelNode.Name}_DYNAMIC_MESH",
                IsInternal = true
            };

            var materialsGltf = meshesGltf.SelectMany(m => m.Primitives).Select(prim => prim.Material).Distinct()
                .ToList();
            var materials = materialsGltf.Select(_ => LabURI.Empty).ToList();
            if (materialDescs != null)
            {
                var modelIndex = 0;
                foreach (var vertex in modelData.Vertexes)
                {
                    var materialDescNameMask =
                        $"DYNAMIC_SCENERY_MATERIAL_DESC_{dynamicModelIdx}_{modelIndex}_";
                    var materialDesc =
                        materialDescs.VisualChildren.FirstOrDefault(n =>
                            n.Name.Contains(materialDescNameMask))!;
                    var materialName = materialDesc.Name.Replace(materialDescNameMask, "");
                
                    var material = new Material
                    {
                        Package = Owner.Package,
                        InvariantName = $"{dynamicModelNode.VisualParent.Name}_{materialName}_MATERIAL",
                        Alias = $"{dynamicModelNode.VisualParent.Name}_{materialName}_MATERIAL",
                        IsInternal = true
                    };

                    var materialData = MaterialData.LoadFromGltf(material, materialDesc,
                        materialsGltf.Where(m => m.Name.Contains($"RIGID{GraphicsHelpers.MaterialTokenDivider}MATERIAL{GraphicsHelpers.MaterialTokenDivider}{materialName}{GraphicsHelpers.MaterialTokenDivider}{modelIndex}{GraphicsHelpers.MaterialTokenDivider}"))
                            .ToList());
                    material.SetData(materialData);
                
                    assetManager.TryAddAsset(material);

                    materials[modelIndex] = material.URI;
                
                    modelIndex++;
                }
            }

            var meshData = new MeshData(mesh);
            meshData.Model = model.URI;
            meshData.Materials = materials;
            
            mesh.SetData(meshData);
            assetManager.AddAsset(mesh);
            
            var dynamicModel = dynamicModelNode.Extras.Deserialize<DynamicSceneryModelData>()!;
            dynamicModel.Mesh = mesh.URI;
            dynamicModel.LodFlag = 0;

            var dynamicSceneryAnimation = dynamicModelNode.GetCurveSamplers(gltfModel.LogicalAnimations.First(a => a is {Name : "DYNAMIC_SCENERY_ANIMATION"}));
            dynamicModel.ReadAnimationFromGltf(dynamicSceneryAnimation);
            
            DynamicModels.Add(dynamicModel);

            dynamicModelIdx++;
        }
    }

    protected override void Dispose(Boolean disposing)
    {
        DynamicModels.Clear();
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanityDynamicScenery_{Owner.Name}");
        var root = GetInGltfFormat(scene);
        scene.AddNode(root);
        
        var resultModel = scene.ToGltf2();
        resultModel.SaveGLB(dataPath);
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

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
        var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);

        foreach (var model in DynamicModels)
        {
            assetManager.GetAsset(model.Mesh).ResolveChunkResources(factory, meshSection);
        }
        
        var item = base.ResolveChunkResources(factory, section, id, layoutId);
        item?.SetID(id);

        return item;
    }
}