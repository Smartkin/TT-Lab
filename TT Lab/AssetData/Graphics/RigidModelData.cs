using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SharpGLTF.Schema2;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Material = TT_Lab.Assets.Graphics.Material;
using Mesh = TT_Lab.Assets.Graphics.Mesh;

namespace TT_Lab.AssetData.Graphics;

[ReferencesAssets]
public class RigidModelData : AbstractAssetData
{
    public RigidModelData(IAsset asset) : base(asset)
    {
        Materials = new List<LabURI>();
        Model = LabURI.Empty;
    }

    public RigidModelData(IAsset asset, ITwinRigidModel rigidModel) : this(asset)
    {
        SetTwinItem(rigidModel);
    }

    public List<LabURI> Materials { get; set; }
    public LabURI Model { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Materials.Clear();
    }

    public override String GetStringified()
    {
        var assetManager = AssetManager.Get();
        var result = new StringBuilder();
        result.AppendLine(assetManager.GetAsset(Model).GetDataHash().ToString());
        foreach (var mat in Materials)
        {
            result.AppendLine(assetManager.GetAsset(mat).GetDataHash().ToString());
        }
        
        return result.ToString();
    }
    
    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanitySkydome_{Owner.Name}");
        var root = new SharpGLTF.Scenes.NodeBuilder("RIGID_MODEL_ROOT");

        ExportGltf(scene, root, "_EXTERNAL_FILE");
        
        var resultModel = scene.ToGltf2();
        resultModel.SaveGLB(dataPath);
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var model = ModelRoot.Load(dataPath);
        var rigidModelRoot = model.DefaultScene.VisualChildren.FirstOrDefault(n => n.Name.Contains("RIGID_MODEL_ROOT"));
        if (rigidModelRoot == null)
        {
            Log.WriteLine($"Misconfigured Rigid Model {dataPath}! Make sure it contains RIGID_MODEL_ROOT node!", Log.LogType.Error);
            return;
        }

        var rigidModel = ImportGltf<RigidModel>(Owner, model, rigidModelRoot);
        var data = (RigidModelData)rigidModel.GetData();
        Model = data.Model;
        Materials.Clear();
        foreach (var material in data.Materials)
        {
            Materials.Add(material);
        }
    }
    
    /// <summary>
    /// Creates the needed internal Mesh/RigidModel asset, Model asset, Material assets and Texture assets needed for RigidModel or Mesh
    /// </summary>
    /// <param name="requester">Asset requesting the creation</param>
    /// <param name="gltfModel">Loaded GLB file with all the data</param>
    /// <param name="containerNode">GLTF node that stores all the meshes and material descs</param>
    /// <typeparam name="T">Mesh or RigidModel</typeparam>
    /// <returns>Newly created Mesh or RigidModel asset</returns>
    public static T ImportGltf<T>(IAsset requester, ModelRoot gltfModel, Node containerNode) where T : RigidModel, new()
    {
        var meshesNode = containerNode.VisualChildren.FirstOrDefault(n => n.Name.StartsWith($"{containerNode.Name}_MESHES"));
        var isFromExternalFile = false;
        if (meshesNode == null)
        {
            meshesNode = containerNode.VisualChildren.FirstOrDefault(n =>
                n.Name.StartsWith($"{containerNode.Name}_RIGID_MODEL__EXTERNAL_FILE"));
            isFromExternalFile = meshesNode != null;
        }
        if (meshesNode == null)
        {
            Log.WriteLine($"Imported Rigid Model {containerNode.Name} does not contain any meshes! Returning empty mesh...", Log.LogType.Error);
        }

        var meshesGltf = CollectMeshes(gltfModel, meshesNode);
        var assetManager = AssetManager.Get();
        var saltName = string.IsNullOrEmpty(requester.Chunk) ? requester.Name : requester.Chunk;
        var model = new Model
        {
            Package = requester.Package,
            InvariantName = $"{saltName}_{containerNode.Name}_MODEL",
            Alias = $"{saltName}_{containerNode.Name}_MODEL",
            IsInternal = true
        };
        
        var modelData = new ModelData(model);
        modelData.LoadFromGltfMeshes(meshesGltf);
        model.SetData(modelData);
        
        assetManager.TryAddAsset(model);
        
        var mesh = new T
        {
            Package = requester.Package,
            InvariantName = $"{saltName}_{containerNode.Name}_MESH",
            Alias = $"{saltName}_{containerNode.Name}_MESH",
            IsInternal = true
        };

        var materialDescs = containerNode.VisualChildren.FirstOrDefault(n => n.Name.StartsWith($"{containerNode.Name}_MATERIAL_DESCS"));
        if (isFromExternalFile)
        {
            materialDescs = meshesNode.VisualChildren.FirstOrDefault(n => n.Name.StartsWith($"{containerNode.Name}_RIGID_MODEL__EXTERNAL_FILE_MATERIAL_DESCS"));
        }
        if (materialDescs == null)
        {
            Log.WriteLine($"No material descriptors found for Rigid Model {containerNode.Name}! Empty ones will be used...", Log.LogType.Warning);
        }
        var materialsGltf = meshesGltf.SelectMany(m => m.Primitives).Select(prim => prim.Material).Distinct().ToList();
        var materials = materialsGltf.Select(_ => LabURI.Empty).ToList();
        if (materialDescs != null)
        {
            var modelIndex = 0;
            foreach (var vertex in modelData.Vertexes)
            {
                var materialDescNameMask =
                    $"{containerNode.Name}_MATERIAL_DESC_{modelIndex}_";
                if (isFromExternalFile)
                {
                    materialDescNameMask = $"{containerNode.Name}_RIGID_MODEL__EXTERNAL_FILE_MATERIAL_DESC_{modelIndex}_";
                }
                var materialDesc =
                    materialDescs.VisualChildren.FirstOrDefault(n =>
                        n.Name.StartsWith(materialDescNameMask))!;
                var materialName = materialDesc.Name.Replace(materialDescNameMask, "");
                
                var material = new Material
                {
                    Package = requester.Package,
                    InvariantName = $"{meshesNode.VisualParent.Name}_{materialName}_MATERIAL",
                    Alias = $"{meshesNode.VisualParent.Name}_{materialName}_MATERIAL",
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
            
            materials = materials[..modelIndex];
        }

        if (typeof(T) == typeof(Mesh))
        {
            var meshData = new MeshData(mesh);
            meshData.Model = model.URI;
            meshData.Materials = materials;

            mesh.SetData(meshData);
        }
        else
        {
            var rigidModelData = new RigidModelData(mesh);
            rigidModelData.Model = model.URI;
            rigidModelData.Materials = materials;
            
            mesh.SetData(rigidModelData);
        }

        assetManager.TryAddAsset(mesh);

        return mesh;
    }

    private static List<SharpGLTF.Schema2.Mesh> CollectMeshes(ModelRoot model, Node node)
    {
        var result = new List<SharpGLTF.Schema2.Mesh>();
        result.AddRange(model.LogicalMeshes.Where(m => m.VisualParents.ToList().All(n => n.LogicalIndex == node.LogicalIndex)).ToList());
        foreach (var child in node.VisualChildren.ToList())
        {
            result.AddRange(CollectMeshes(model, child));
        }
        
        return result;
    }

    public SharpGLTF.Scenes.NodeBuilder ExportGltf(SharpGLTF.Scenes.SceneBuilder scene, SharpGLTF.Scenes.NodeBuilder parentNode, string id)
    {
        var assetManager = AssetManager.Get();
        var rigidModelData = this;
        var model = assetManager.GetAssetData<ModelData>(rigidModelData.Model);
        var proxyNode = parentNode.CreateNode($"{parentNode.Name}_RIGID_MODEL_{id}");
        var meshesNode = proxyNode.CreateNode($"{proxyNode.Name}_MESHES");
        var meshes = model.GetMeshes(meshesNode, rigidModelData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
        foreach (var mesh in meshes)
        {
            mesh.Mesh.Name = mesh.Mesh.Name.Replace("RIGIDIDPLACEHOLDER", id);
            scene.AddRigidMesh(mesh.Mesh, meshesNode);
        }
        
        var subModelId = 0;
        var rigidMaterialDescs = proxyNode.CreateNode($"{proxyNode.Name}_MATERIAL_DESCS");
        foreach (var vertex in model.Vertexes)
        {
            var material = assetManager.GetAssetData<MaterialData>(rigidModelData.Materials[subModelId]);
            var materialDescNode = new SharpGLTF.Scenes.NodeBuilder
            {
                Extras = material.GetJsonFormat(),
                Name = $"{proxyNode.Name}_MATERIAL_DESC_{subModelId}_{material.Name}"
            };
        
            rigidMaterialDescs.AddNode(materialDescNode);

            subModelId++;
        }

        return proxyNode;
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinRigidModel rigidModel = GetTwinItem<ITwinRigidModel>();
        Materials = new List<LabURI>();
        foreach (var mat in rigidModel.Materials)
        {
            Materials.Add(AssetManager.Get().GetUriByTwinId<Material>(Owner, mat));
        }
        Model = AssetManager.Get().GetUriByTwinId<Model>(Owner, rigidModel.Model);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(257); // Unused header
        writer.Write(Materials.Count);
        foreach (var mat in Materials)
        {
            writer.Write(assetManager.GetAsset(mat).ExportTwinID);
        }
        writer.Write(assetManager.GetAsset(Model).ExportTwinID);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateRigidModel(ms);
    }

    protected virtual void ResolveResources(ITwinItemFactory factory, ITwinSection section)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        var modelsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MODELS_SECTION);

        foreach (var material in Materials)
        {
            assetManager.GetAsset(material).ResolveChunkResources(factory, materialsSection);
        }

        assetManager.GetAsset(Model).ResolveChunkResources(factory, modelsSection);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        ResolveResources(factory, section);
        return base.ResolveChunkResources(factory, section, id, layoutID);
    }
}