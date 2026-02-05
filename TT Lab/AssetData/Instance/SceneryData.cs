using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using SharpGLTF.Schema2;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using Material = TT_Lab.Assets.Graphics.Material;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Vector3 = System.Numerics.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class SceneryData : AbstractAssetData
{
    private static readonly Dictionary<ITwinScenery.SceneryType, Type> ScIndexToType = new();

    static SceneryData()
    {
        ScIndexToType.Add(ITwinScenery.SceneryType.Root, typeof(SceneryRootData));
        ScIndexToType.Add(ITwinScenery.SceneryType.Leaf, typeof(SceneryLeafData));
        ScIndexToType.Add(ITwinScenery.SceneryType.Node, typeof(SceneryNodeData));
    }

    [System.Text.Json.Serialization.JsonConstructor]
    private SceneryData() : base(null) { }

    public SceneryData(IAsset asset) : base(asset)
    {
        SkydomeID = LabURI.Empty;
        Collision = LabURI.Empty;
        DynamicScenery = LabURI.Empty;
        HasLighting = false;
        AmbientLights = new List<AmbientLight>();
        DirectionalLights = new List<DirectionalLight>();
        PointLights = new List<PointLight>();
        NegativeLights = new List<NegativeLight>();
        Sceneries = new List<SceneryBaseData>();
    }

    public SceneryData(IAsset asset, ITwinScenery scenery) : this(asset)
    {
        SetTwinItem(scenery);
    }
    
    [JsonProperty(Required = Required.Always)]
    public LabURI SkydomeID { get; set; }
    
    public LabURI DynamicScenery { get; set; }
    
    public LabURI Collision { get; set; }
    
    public UInt32 FogColor { get; set; }
    
    public Byte UnkByte { get; set; }
    
    public Boolean HasLighting { get; set; }
    
    public List<AmbientLight> AmbientLights { get; set; }
    
    public List<DirectionalLight> DirectionalLights { get; set; }
    
    public List<PointLight> PointLights { get; set; }
    
    public List<NegativeLight> NegativeLights { get; set; }
    
    public List<SceneryBaseData> Sceneries { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        var assetManager = AssetManager.Get();
        if (DynamicScenery != LabURI.Empty)
        {
            var dynScenery = assetManager.GetAsset(DynamicScenery);
            if (dynScenery.IsInternal)
            {
                assetManager.GetAsset(DynamicScenery).Delete();
            }
        }

        if (Collision != LabURI.Empty)
        {
            var collision = assetManager.GetAsset(Collision);
            if (collision.IsInternal)
            {
                assetManager.GetAsset(Collision).Delete();
            }
        }

        foreach (var sceneryBaseData in Sceneries)
        {
            foreach (var labUri in sceneryBaseData.MeshIDs)
            {
                var mesh = assetManager.GetAsset(labUri);
                if (mesh.IsInternal)
                {
                    assetManager.GetAsset(labUri).Delete();
                }
            }

            foreach (var labUri in sceneryBaseData.LodIDs)
            {
                var lod = assetManager.GetAsset(labUri);
                if (lod.IsInternal)
                {
                    assetManager.GetAsset(labUri).Delete();
                }
            }
        }
        
        AmbientLights.Clear();
        DirectionalLights.Clear();
        PointLights.Clear();
        NegativeLights.Clear();
        Sceneries.Clear();
    }
    
    private const string SCENERY_ROOT_NAME = "SCENERY_ROOT";
    private const string SCENERY_NODE_START_NAME = "SCENERY_NODE_";
    private const string SCENERY_LEAF_START_NAME = "SCENERY_LEAF_";
    private const string LIGHTING_ROOT_NODE_NAME = "LIGHTING_ROOT";
    private const string DYNAMIC_SCENERY_ROOT_NODE_NAME = "DYNAMIC_SCENERY_ROOT";
    private const string COLLISION_ROOT_NODE_NAME = "COLLISION_ROOT";
    private const string AMBIENT_LIGHTS_NODE_NAME = "AMBIENT_LIGHTS";
    private const string DIRECTIONAL_LIGHTS_NODE_NAME = "DIRECTIONAL_LIGHTS";
    private const string POINTS_LIGHTS_NODE_NAME = "POINT_LIGHTS";
    private const string NEGATIVE_LIGHTS_NODE_NAME = "NEGATIVE_LIGHTS";

    private void ExportGltf(string path)
    {
        var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanityScenery_{Owner.Name}");
        var root = new SharpGLTF.Scenes.NodeBuilder(SCENERY_ROOT_NAME);
        scene.AddNode(root);

        var dynamicScenery = AssetManager.Get().GetAssetData<DynamicSceneryData>(DynamicScenery).GetInGltfFormat(scene);
        scene.AddNode(dynamicScenery);

        var collisionNode = new SharpGLTF.Scenes.NodeBuilder(COLLISION_ROOT_NODE_NAME);
        var collision = AssetManager.Get().GetAssetData<CollisionData>(Collision).GetMesh(collisionNode);
        scene.AddRigidMesh(collision.Mesh, collisionNode);

        // TODO: Try to use GLTF lights, maybe
        if (HasLighting)
        {
            var lightingRoot = new SharpGLTF.Scenes.NodeBuilder(LIGHTING_ROOT_NODE_NAME);
            scene.AddNode(lightingRoot);

            if (AmbientLights.Count > 0)
            {
                var ambients = lightingRoot.CreateNode(AMBIENT_LIGHTS_NODE_NAME);
                var ambientIndex = 0;
                foreach (var ambientLight in AmbientLights)
                {
                    var ambientLightNode = ambients.CreateNode($"AMBIENT_{ambientIndex++}")
                        .WithLocalTranslation(new Vector3(ambientLight.Position.X, ambientLight.Position.Y,
                            ambientLight.Position.Z));
                    var ambientJson = new AmbientLightJsonFormat(ambientLight);
                    ambientLightNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(ambientJson);
                }
            }

            if (DirectionalLights.Count > 0)
            {
                var directionals = lightingRoot.CreateNode(DIRECTIONAL_LIGHTS_NODE_NAME);
                var directionalIndex = 0;
                foreach (var directionalLight in DirectionalLights)
                {
                    var directionalLightNode = directionals.CreateNode($"DIRECTIONAL_{directionalIndex++}")
                        .WithLocalTranslation(new Vector3(directionalLight.Position.X, directionalLight.Position.Y,
                            directionalLight.Position.Z))
                        .WithLocalRotation(new Quaternion(directionalLight.Direction.X,  directionalLight.Direction.Y, directionalLight.Direction.Z, directionalLight.Direction.W));
                    var directionalJson = new DirectionalLightJsonFormat(directionalLight);
                    directionalLightNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(directionalJson);
                }
            }

            if (PointLights.Count > 0)
            {
                var points = lightingRoot.CreateNode(POINTS_LIGHTS_NODE_NAME);
                var pointIndex = 0;
                foreach (var pointLight in PointLights)
                {
                    var pointLightNode = points.CreateNode($"POINT_{pointIndex++}")
                        .WithLocalTranslation(new Vector3(pointLight.Position.X, pointLight.Position.Y,
                            pointLight.Position.Z));
                    var pointJson = new PointLightJsonFormat(pointLight);
                    pointLightNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(pointJson);
                }
            }

            if (NegativeLights.Count > 0)
            {
                var negatives = lightingRoot.CreateNode(NEGATIVE_LIGHTS_NODE_NAME);
                var negativeIndex = 0;
                foreach (var negativeLight in NegativeLights)
                {
                    var negativeLightNode = negatives.CreateNode($"NEGATIVE_{negativeIndex++}")
                        .WithLocalTranslation(new Vector3(negativeLight.Position.X, negativeLight.Position.Y,
                            negativeLight.Position.Z));
                    var negativeJson = new NegativeLightJsonFormat(negativeLight);
                    negativeLightNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(negativeJson);
                }
            }

            // lightingRoot.WithLocalScale(new Vector3(-1, 1, 1));
        }

        var sceneryRoot = (SceneryRootData)Sceneries[0];
        var sceneryList = Sceneries.Skip(1).ToList();
        BuildSceneryRenderTreeForNode(scene, root, sceneryRoot, ref sceneryList);
        ExportSceneryNodesToGltf(scene, root, sceneryRoot);

        // root.WithLocalScale(new Vector3(-1, 1, 1));
        root.Extras = System.Text.Json.JsonSerializer.SerializeToNode(new SceneryRootJsonFormat
        {
            FogColor = FogColor,
            UnkByte = UnkByte,
            RootData = sceneryRoot
        });
        
        var resultModel = scene.ToGltf2();
        resultModel.SaveGLB(path);
    }
    
    private void BuildSceneryRenderTreeForNode(SharpGLTF.Scenes.SceneBuilder scene, SharpGLTF.Scenes.NodeBuilder parentNode, SceneryNodeData sceneryNode, ref List<SceneryBaseData> sceneryTree)
    {
        foreach (var sceneryType in sceneryNode.SceneryTypes)
        {
            if (sceneryType == ITwinScenery.SceneryType.Node)
            {
                var childNode = parentNode.CreateNode($"{SCENERY_NODE_START_NAME}{sceneryTree.Count}");
                var data = (SceneryNodeData)sceneryTree[0];
                childNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(data);
                sceneryTree = sceneryTree.Skip(1).ToList();
                ExportSceneryNodesToGltf(scene, childNode, data);
                BuildSceneryRenderTreeForNode(scene, childNode, data, ref sceneryTree);
            }
            else if (sceneryType == ITwinScenery.SceneryType.Leaf)
            {
                var childNode = parentNode.CreateNode($"{SCENERY_LEAF_START_NAME}{sceneryTree.Count}");
                var data = sceneryTree[0];
                childNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(data);
                sceneryTree = sceneryTree.Skip(1).ToList();
                ExportSceneryNodesToGltf(scene, childNode, data);
            }
        }
    }

    private void ExportSceneryNodesToGltf(SharpGLTF.Scenes.SceneBuilder scene, SharpGLTF.Scenes.NodeBuilder parentNode, SceneryBaseData sceneryData)
    {
        var assetManager = AssetManager.Get();
        if (sceneryData.MeshIDs.Count > 0)
        {
            var index = 0;
            var meshesNode = parentNode.CreateNode($"{parentNode.Name}_MESHES");
            var meshMaterialDescs = meshesNode.CreateNode($"{meshesNode.Name}_MATERIAL_DESCS");
            foreach (var meshId in sceneryData.MeshIDs)
            {
                var meshData = assetManager.GetAssetData<MeshData>(meshId);
                var model = assetManager.GetAssetData<ModelData>(meshData.Model);
                var meshMatrix = sceneryData.MeshModelMatrices[index];
                var meshNode = meshesNode.CreateNode($"{meshesNode.Name}_MESH_{index}");
                meshNode.LocalMatrix = meshMatrix.ToSystem();
                var meshes = model.GetMeshes(meshNode,
                    meshData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
                foreach (var mesh in meshes)
                {
                    mesh.Mesh.Name = $"{meshNode.Name}_{mesh.Mesh.Name.Replace("RIGIDIDPLACEHOLDER", index.ToString())}";
                    scene.AddRigidMesh(mesh.Mesh, meshNode);
                }

                var subModelId = 0;
                foreach (var vertex in model.Vertexes)
                {
                    var material = assetManager.GetAssetData<MaterialData>(meshData.Materials[subModelId]);
                    var materialDescNode = new SharpGLTF.Scenes.NodeBuilder
                    {
                        Extras = material.GetJsonFormat(),
                        Name = $"{parentNode.Name}_MATERIAL_DESC_{index}_{subModelId}_{material.Name}"
                    };

                    meshMaterialDescs.AddNode(materialDescNode);

                    subModelId++;
                }

                index++;
            }
        }

        if (sceneryData.LodIDs.Count > 0)
        {
            var index = 0;
            var lodsNode = parentNode.CreateNode($"{parentNode.Name}_LODS");
            var lodsMaterialDescs = lodsNode.CreateNode($"{lodsNode.Name}_MATERIAL_DESCS");
            foreach (var lodId in sceneryData.LodIDs)
            {
                var lodData = assetManager.GetAssetData<LodModelData>(lodId);
                var lodNode = lodsNode.CreateNode($"{lodsNode.Name}_LOD_{index}");
                var lodMatrix = sceneryData.LodModelMatrices[index];
                lodNode.LocalMatrix = lodMatrix.ToSystem();
                lodNode.Extras = System.Text.Json.JsonSerializer.SerializeToNode(lodData);
                var meshIdx = 0;
                foreach (var meshId in lodData.Meshes)
                {
                    var meshData = assetManager.GetAssetData<MeshData>(meshId);
                    var model = assetManager.GetAssetData<ModelData>(meshData.Model);
                    var meshNode = lodNode.CreateNode($"{lodNode.Name}_MESH_{meshIdx}");
                    var meshes = model.GetMeshes(meshNode,
                        meshData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
                    foreach (var mesh in meshes)
                    {
                        mesh.Mesh.Name = $"{meshNode.Name}_{mesh.Mesh.Name.Replace("RIGIDIDPLACEHOLDER", $"lod_{meshIdx}")}";
                        scene.AddRigidMesh(mesh.Mesh, meshNode);
                    }

                    var subModelId = 0;
                    foreach (var vertex in model.Vertexes)
                    {
                        var material = assetManager.GetAssetData<MaterialData>(meshData.Materials[subModelId]);
                        var materialDescNode = new SharpGLTF.Scenes.NodeBuilder
                        {
                            Extras = material.GetJsonFormat(),
                            Name = $"{meshNode.Name}_MATERIAL_DESC_{meshIdx}_{subModelId}_{material.Name}"
                        };

                        lodsMaterialDescs.AddNode(materialDescNode);

                        subModelId++;
                    }

                    meshIdx++;
                }

                index++;
            }
        }
    }

    private void ImportGltf(string path)
    {
        var importErrored = false;
        
        Sceneries.Clear();
        AmbientLights.Clear();
        PointLights.Clear();
        DirectionalLights.Clear();
        NegativeLights.Clear();
        var model = ModelRoot.Load(path + ".glb");
        var scene = model.DefaultScene;

        var lightsNode = scene.VisualChildren.FirstOrDefault(n => n is { Name: LIGHTING_ROOT_NODE_NAME });
        HasLighting = lightsNode != null;
        if (HasLighting)
        {
            ImportGltfLights(lightsNode!);
        }

        var dynamicSceneryNode = scene.VisualChildren.FirstOrDefault(n => n is { Name: DYNAMIC_SCENERY_ROOT_NODE_NAME });
        if (dynamicSceneryNode == null)
        {
            Log.WriteLine($"Scene {path}.glb does not have {DYNAMIC_SCENERY_ROOT_NODE_NAME} node. No Dynamic Scenery will be loaded.");
        }

        var sceneryRoot = scene.VisualChildren.FirstOrDefault(n => n is { Name: SCENERY_ROOT_NAME });
        if (sceneryRoot == null)
        {
            Log.WriteLine($"Misconfigured scenery {path}.glb! No root found. Make sure you have {SCENERY_ROOT_NAME} node in your scene!", Log.LogType.Error);
            importErrored = true;
        }

        var collisionRoot = scene.VisualChildren.FirstOrDefault(n => n is { Name: COLLISION_ROOT_NODE_NAME });
        if (collisionRoot == null)
        {
            Log.WriteLine($"Misconfigured scenery {path}.glb! No collision found. Make sure you have {COLLISION_ROOT_NODE_NAME} node in your scene!", Log.LogType.Error);
            importErrored = true;
        }

        if (importErrored)
        {
            return;
        }

        if (dynamicSceneryNode != null)
        {
            var dynamicScenery = new Assets.Instance.DynamicScenery
            {
                Package = Owner.Package,
                Chunk = Owner.Chunk,
                InvariantName = $"{Owner.Chunk}_DYNAMIC_SCENERY",
                Alias = "Dynamic Scenery",
                IsInternal = true
            };

            var dynamicSceneryData = new DynamicSceneryData(dynamicScenery);
            dynamicSceneryData.LoadFromGltf(model, dynamicSceneryNode!);

            dynamicScenery.SetData(dynamicSceneryData);
            AssetManager.Get().AddAsset(dynamicScenery);
            
            DynamicScenery = dynamicScenery.URI;
        }

        var collision = new Assets.Instance.Collision
        {
            Package = Owner.Package,
            Chunk = Owner.Chunk,
            InvariantName = $"{Owner.Chunk}_COLLISION",
            Alias = "Collision",
            IsInternal = true
        };
        var collisionData = new CollisionData(collision);
        collisionData.LoadFromGltf(model.LogicalMeshes.Where(m => m is {Name: "STATIC_COLLISION_MESH"}).ToList());
        
        collision.SetData(collisionData);
        AssetManager.Get().AddAsset(collision);

        Collision = collision.URI;
        
        ImportGltfSceneryRootData(sceneryRoot!, model);
    }

    private void ImportGltfLights(Node lightsRoot)
    {
        var ambientLights = lightsRoot.VisualChildren.FirstOrDefault(n => n is { Name : AMBIENT_LIGHTS_NODE_NAME });
        if (ambientLights != null)
        {
            foreach (var ambientLightNode in ambientLights.VisualChildren)
            {
                var ambientLightJson = ambientLightNode.Extras.Deserialize<AmbientLightJsonFormat>()!;
                var ambientLight = new AmbientLight
                {
                    Position = ambientLightNode.LocalTransform.Translation.ToTwin(),
                    Color = ambientLightJson.Color,
                    UnkData = ambientLightJson.UnkData,
                    Radius = ambientLightJson.Radius,
                    UnkVec1 = ambientLightJson.UnkVec1,
                    UnkVec2 = ambientLightJson.UnkVec2
                };
                AmbientLights.Add(ambientLight);
            }
        }

        var pointLights = lightsRoot.VisualChildren.FirstOrDefault(n => n is { Name : POINTS_LIGHTS_NODE_NAME });
        if (pointLights != null)
        {
            foreach (var pointLightNode in pointLights.VisualChildren)
            {
                var pointLightJson = pointLightNode.Extras.Deserialize<PointLightJsonFormat>()!;
                var pointLight = new PointLight
                {
                    Position = pointLightNode.LocalTransform.Translation.ToTwin(),
                    Color = pointLightJson.Color,
                    UnkData = pointLightJson.UnkData,
                    Radius = pointLightJson.Radius,
                    UnkVec1 = pointLightJson.UnkVec1,
                    UnkVec2 = pointLightJson.UnkVec2,
                    UnkShort = pointLightJson.UnkShort
                };
                PointLights.Add(pointLight);
            }
        }
        
        var directionalLights = lightsRoot.VisualChildren.FirstOrDefault(n => n is { Name : DIRECTIONAL_LIGHTS_NODE_NAME });
        if (directionalLights != null)
        {
            foreach (var directionalLightNode in directionalLights.VisualChildren)
            {
                var directionalLightJson = directionalLightNode.Extras.Deserialize<DirectionalLightJsonFormat>()!;
                var directionalLight = new DirectionalLight
                {
                    Position = directionalLightNode.LocalTransform.Translation.ToTwin(),
                    Color = directionalLightJson.Color,
                    UnkData = directionalLightJson.UnkData,
                    Radius = directionalLightJson.Radius,
                    UnkVec1 = directionalLightJson.UnkVec1,
                    UnkVec2 = directionalLightJson.UnkVec2,
                    UnkShort = directionalLightJson.UnkShort,
                    Direction = directionalLightNode.LocalTransform.Rotation.ToTwin()
                };
                DirectionalLights.Add(directionalLight);
            }
        }
        
        var negativeLights = lightsRoot.VisualChildren.FirstOrDefault(n => n is { Name : NEGATIVE_LIGHTS_NODE_NAME });
        if (negativeLights != null)
        {
            foreach (var negativeLightNode in negativeLights.VisualChildren)
            {
                var negativeLightJson = negativeLightNode.Extras.Deserialize<NegativeLightJsonFormat>()!;
                var negativeLight = new NegativeLight
                {
                    Position = negativeLightNode.LocalTransform.Translation.ToTwin(),
                    Color = negativeLightJson.Color,
                    UnkData = negativeLightJson.UnkData,
                    Radius = negativeLightJson.Radius,
                    UnkVec1 = negativeLightJson.UnkVec1,
                    UnkVec2 = negativeLightJson.UnkVec2,
                    UnkFloat1 = negativeLightJson.UnkFloat1,
                    UnkFloat2 = negativeLightJson.UnkFloat2,
                };
                NegativeLights.Add(negativeLight);
            }
        }
    }

    private void ImportGltfSceneryRootData(Node sceneryRoot, ModelRoot model)
    {
        var initialData = sceneryRoot.Extras.Deserialize<SceneryRootJsonFormat>()!;
        UnkByte = initialData.UnkByte;
        FogColor = initialData.FogColor;

        initialData.RootData.SceneryTypes = [
            ITwinScenery.SceneryType.None,
            ITwinScenery.SceneryType.None,
            ITwinScenery.SceneryType.None,
            ITwinScenery.SceneryType.None,
            ITwinScenery.SceneryType.None,
            ITwinScenery.SceneryType.None,
            ITwinScenery.SceneryType.None,
            ITwinScenery.SceneryType.None
        ];
        initialData.RootData.MeshIDs = [];
        initialData.RootData.LodIDs = [];
        initialData.RootData.MeshModelMatrices = [];
        initialData.RootData.LodModelMatrices = [];
        Sceneries.Add(initialData.RootData);
        var sceneryTree = Sceneries;
        ImportGltfSceneryNodeData(sceneryRoot, (SceneryNodeData)Sceneries[0], ref sceneryTree, model);
        ImportGltfMeshesAndLods(sceneryRoot, Sceneries[0], model);
    }

    private void ImportGltfSceneryNodeData(Node sceneryNode, SceneryNodeData parentNodeData, ref List<SceneryBaseData> sceneryTree, ModelRoot model)
    {
        var sceneryTypeIndex = 0;
        foreach (var sceneryNodeChild in sceneryNode.VisualChildren)
        {
            if (sceneryNodeChild.Name.EndsWith("_LODS") ||
                sceneryNodeChild.Name.EndsWith("_MESHES"))
            {
                continue;
            }
            
            if (sceneryNodeChild.Name.StartsWith(SCENERY_NODE_START_NAME))
            {
                parentNodeData.SceneryTypes[sceneryTypeIndex] = ITwinScenery.SceneryType.Node;
                var nodeData = sceneryNodeChild.Extras.Deserialize<SceneryNodeData>()!;
                nodeData.MeshIDs = [];
                nodeData.LodIDs = [];
                nodeData.MeshModelMatrices = [];
                nodeData.LodModelMatrices = [];
                nodeData.SceneryTypes = [
                    ITwinScenery.SceneryType.None,
                    ITwinScenery.SceneryType.None,
                    ITwinScenery.SceneryType.None,
                    ITwinScenery.SceneryType.None,
                    ITwinScenery.SceneryType.None,
                    ITwinScenery.SceneryType.None,
                    ITwinScenery.SceneryType.None,
                    ITwinScenery.SceneryType.None
                ];
                sceneryTree.Add(nodeData);
                ImportGltfSceneryNodeData(sceneryNodeChild, nodeData, ref sceneryTree, model);
                ImportGltfMeshesAndLods(sceneryNodeChild, nodeData, model);
            }
            else if (sceneryNodeChild.Name.StartsWith(SCENERY_LEAF_START_NAME))
            {
                parentNodeData.SceneryTypes[sceneryTypeIndex] = ITwinScenery.SceneryType.Leaf;
                var leafData = sceneryNodeChild.Extras.Deserialize<SceneryLeafData>()!;
                leafData.MeshIDs = [];
                leafData.LodIDs = [];
                leafData.MeshModelMatrices = [];
                leafData.LodModelMatrices = [];
                sceneryTree.Add(leafData);
                ImportGltfMeshesAndLods(sceneryNodeChild, leafData, model);
            }
            
            sceneryTypeIndex++;
            if (sceneryTypeIndex <= 8)
            {
                continue;
            }
            
            Log.WriteLine($"Scenery node {sceneryNode.Name} has more than 8 child nodes! Aborting its processing...", Log.LogType.Warning);
            break;
        }
    }

    private void ImportGltfMeshesAndLods(Node sceneryNode, SceneryBaseData sceneryData, ModelRoot model)
    {
        var meshesNode = sceneryNode.VisualChildren.FirstOrDefault(n => n.Name == $"{sceneryNode.Name}_MESHES");
        if (meshesNode != null)
        {
            ImportGltfMeshes(meshesNode, sceneryData, model);
        }
        
        var lodsNode = sceneryNode.VisualChildren.FirstOrDefault(n => n.Name == $"{sceneryNode.Name}_LODS");
        if (lodsNode != null)
        {
            ImportGltfLods(lodsNode, sceneryData, model);
        }
    }

    private Mesh ImportGltfMesh(List<SharpGLTF.Schema2.Mesh> meshesGltf, Node meshesNode, Node meshNode, int meshNodeIndex, bool isLod, Node? materialDescs)
    {
        var assetManager = AssetManager.Get();
        var model = new Model
        {
            Package = Owner.Package,
            InvariantName = $"{Owner.Chunk}_{meshNode.Name}_MODEL",
            Alias = $"{Owner.Chunk}_{meshNode.Name}_MODEL",
            IsInternal = true
        };
        
        var modelData = new ModelData(model);
        modelData.LoadFromGltfMeshes(meshesGltf);
        model.SetData(modelData);
        
        assetManager.AddAsset(model);
        
        var mesh = new Mesh
        {
            Package = Owner.Package,
            InvariantName = $"{Owner.Chunk}_{meshNode.Name}_MESH{(isLod ? "_LOD" : "")}",
            Alias = $"{Owner.Chunk}_{meshNode.Name}_MESH{(isLod ? "_LOD" : "")}",
            IsInternal = true
        };

        var materialsGltf = meshesGltf.SelectMany(m => m.Primitives).Select(prim => prim.Material).Distinct().ToList();
        var materials = materialsGltf.Select(_ => LabURI.Empty).ToList();
        if (materialDescs != null)
        {
            var modelIndex = 0;
            foreach (var vertex in modelData.Vertexes)
            {
                var materialDescNameMask =
                    $"{meshesNode.VisualParent.Name}_MATERIAL_DESC_{meshNodeIndex}_{modelIndex}_";
                if (isLod)
                {
                    materialDescNameMask = $"{meshNode.Name}_MATERIAL_DESC_{meshNodeIndex}_{modelIndex}_";
                }
                var materialDesc =
                    materialDescs.VisualChildren.FirstOrDefault(n =>
                        n.Name.Contains(materialDescNameMask))!;
                var materialName = materialDesc.Name.Replace(materialDescNameMask, "");
                
                var material = new Material
                {
                    Package = Owner.Package,
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
        
        var meshData = new MeshData(mesh);
        meshData.Model = model.URI;
        meshData.Materials = materials;
        
        mesh.SetData(meshData);
        assetManager.AddAsset(mesh);

        return mesh;
    }

    private void ImportGltfMeshes(Node meshesNode, SceneryBaseData sceneryData, ModelRoot sceneryModel)
    {
        var materialDescs = meshesNode.VisualChildren.FirstOrDefault(n => n.Name == $"{meshesNode.Name}_MATERIAL_DESCS");
        if (materialDescs == null)
        {
            Log.WriteLine("No material descs for meshes found! Empty materials will be used!", Log.LogType.Warning);
        }

        var meshNodeIndex = 0;
        foreach (var meshNode in meshesNode.VisualChildren)
        {
            if (meshNode == materialDescs)
            {
                continue;
            }

            sceneryData.MeshModelMatrices.Add(meshNode.LocalMatrix.ToTwin());
            var meshesGltf = sceneryModel.LogicalMeshes.Where(m => m.Name.StartsWith($"{meshNode.Name}_mesh_{meshNodeIndex}")).ToList();
            var mesh = ImportGltfMesh(meshesGltf, meshesNode, meshNode, meshNodeIndex, false, materialDescs);
            sceneryData.MeshIDs.Add(mesh.URI);
            meshNodeIndex++;
        }
    }

    private void ImportGltfLods(Node lodsNode, SceneryBaseData sceneryData, ModelRoot model)
    {
        var assetManager = AssetManager.Get();
        var materialDescs = lodsNode.VisualChildren.FirstOrDefault(n => n.Name == $"{lodsNode.Name}_MATERIAL_DESCS");
        if (materialDescs == null)
        {
            Log.WriteLine("No material descs for LODs found! Empty materials will be used!", Log.LogType.Warning);
        }

        foreach (var lodNode in lodsNode.VisualChildren)
        {
            if (lodNode == materialDescs)
            {
                continue;
            }
            
            sceneryData.LodModelMatrices.Add(lodNode.LocalMatrix.ToTwin());

            var lod = new LodModel
            {
                Package = Owner.Package,
                InvariantName = $"{lodNode.Name}_LOD_MODEL",
                Alias = $"{lodNode.Name}_LOD_MODEL",
                IsInternal = true
            };
            
            var lodData = lodNode.Extras.Deserialize<LodModelData>()!;
            lodData.Meshes = [];
            lodData.SetOwner(Owner);

            var lodMeshIndex = 0;
            foreach (var meshNode in lodNode.VisualChildren)
            {
                var meshesGltf = model.LogicalMeshes.Where(m => m.Name.StartsWith($"{meshNode.Name}_mesh_lod_{lodMeshIndex}_")).ToList();
                var labMesh = ImportGltfMesh(meshesGltf, lodNode, meshNode, lodMeshIndex, true, materialDescs);
                lodData.Meshes.Add(labMesh.URI);
                lodMeshIndex++;
            }
            
            lod.SetData(lodData);
            
            assetManager.AddAsset(lod);
            
            sceneryData.LodIDs.Add(lod.URI);
        }
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };
        base.SaveInternal(dataPath, settings);
            
        ExportGltf(dataPath + ".glb");
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All
        };
        base.LoadInternal(dataPath, settings);
        
        ImportGltf(dataPath);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var scenery = GetTwinItem<ITwinScenery>();
        FogColor = scenery.FogColor;
        UnkByte = scenery.UnkByte;
        if (scenery.SkydomeID != 0)
        {
            SkydomeID = AssetManager.Get().GetUriByTwinId<Skydome>(Owner, scenery.SkydomeID);
        }
        HasLighting = scenery.HasLighting;
        if (HasLighting)
        {
            AmbientLights = CloneUtils.DeepClone(scenery.AmbientLights);
            DirectionalLights = CloneUtils.DeepClone(scenery.DirectionalLights);
            PointLights = CloneUtils.DeepClone(scenery.PointLights);
            NegativeLights = CloneUtils.DeepClone(scenery.NegativeLights);
        }
        Sceneries = new List<SceneryBaseData>();
        foreach (var sc in scenery.Sceneries)
        {
            Sceneries.Add((SceneryBaseData)Activator.CreateInstance(ScIndexToType[sc.GetObjectIndex()], Owner, sc)!);
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write("TTLabScenery");
        writer.Write(FogColor);
        writer.Write(UnkByte);
        writer.Write(SkydomeID == LabURI.Empty ? 0 : assetManager.GetAsset(SkydomeID).ExportTwinID);
        writer.Write(HasLighting);
        if (HasLighting)
        {
            writer.Write(AmbientLights.Count);
            foreach (var ambient in AmbientLights)
            {
                ambient.Write(writer);
            }

            writer.Write(DirectionalLights.Count);
            foreach (var directional in DirectionalLights)
            {
                directional.Write(writer);
            }

            writer.Write(PointLights.Count);
            foreach (var point in PointLights)
            {
                point.Write(writer);
            }

            writer.Write(NegativeLights.Count);
            foreach (var negative in NegativeLights)
            {
                negative.Write(writer);
            }
        }
        writer.Write(Sceneries.Count);
        foreach (var scenery in Sceneries)
        {
            writer.Write((Int32)scenery.GetSceneryType());
            scenery.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateScenery(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
        var skydomeSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKYDOMES_SECTION);
        if (SkydomeID != LabURI.Empty)
        {
            assetManager.GetAsset(SkydomeID).ResolveChunkResources(factory, skydomeSection);
        }

        if (DynamicScenery != LabURI.Empty)
        {
            assetManager.GetAssetData<DynamicSceneryData>(DynamicScenery).ResolveChunkResources(factory, section,
                Constants.SCENERY_DYNAMIC_SECENERY_ITEM, layoutID);
        }
        else
        {
            var dummyDynamicScenery = new DynamicSceneryData(null);
            dummyDynamicScenery.ResolveChunkResources(factory, section, Constants.SCENERY_DYNAMIC_SECENERY_ITEM, layoutID);
        }

        foreach (var scenery in Sceneries)
        {
            scenery.ResolveChunkResouces(factory, graphicsSection);
        }

        return base.ResolveChunkResources(factory, section, Constants.SCENERY_SECENERY_ITEM, layoutID);
    }
}

public abstract class LightJsonFormat
{
    [System.Text.Json.Serialization.JsonConstructor]
    protected LightJsonFormat() { }

    protected LightJsonFormat(Light light)
    {
        UnkData = light.UnkData;
        Radius = light.Radius;
        Color = light.Color;
        UnkVec1 = light.UnkVec1;
        UnkVec2 = light.UnkVec2;
    }

    public UInt32 UnkData { get; set; }
    public Single Radius  { get; set; }

    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 Color { get; set; } = new(1, 1, 1, 1);

    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 UnkVec1 { get; set; } = new(0, 0, 0, 1);

    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 UnkVec2 { get; set; } = new(0, 0, 0, 1);
}

public class AmbientLightJsonFormat : LightJsonFormat
{
    [System.Text.Json.Serialization.JsonConstructor]
    private AmbientLightJsonFormat() { }

    public AmbientLightJsonFormat(AmbientLight light) : base(light) { }
}

public class DirectionalLightJsonFormat : LightJsonFormat
{
    [System.Text.Json.Serialization.JsonConstructor]
    private DirectionalLightJsonFormat() { }

    public DirectionalLightJsonFormat(DirectionalLight light) : base(light)
    {
        UnkShort = light.UnkShort;
    }

    public Int16 UnkShort { get; set; }
}

public class PointLightJsonFormat : LightJsonFormat
{
    [System.Text.Json.Serialization.JsonConstructor]
    private PointLightJsonFormat() { }

    public PointLightJsonFormat(PointLight light) : base(light)
    {
        UnkShort = light.UnkShort;
    }
    
    public Int16 UnkShort { get; set; }
}

public class NegativeLightJsonFormat : LightJsonFormat
{
    [System.Text.Json.Serialization.JsonConstructor]
    private NegativeLightJsonFormat() { }

    public NegativeLightJsonFormat(NegativeLight light) : base(light)
    {
        UnkVec3 = light.UnkVec3;
        UnkFloat1 = light.UnkFloat1;
        UnkFloat2 = light.UnkFloat2;
        UnkUInt1 = light.UnkUInt1;
        UnkUInt2 = light.UnkUInt2;
        UnkUShort1 = light.UnkUShort1;
        UnkUShort2 = light.UnkUShort2;
    }
    
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 UnkVec3 { get; set; } = new(0, 0, 0, 1);
    public Single UnkFloat1 { get; set; }
    public Single UnkFloat2 { get; set; }
    public UInt32 UnkUInt1 { get; set; }
    public UInt32 UnkUInt2 { get; set; }
    public UInt16 UnkUShort1 { get; set; }
    public UInt16 UnkUShort2 { get; set; }
}

public class SceneryRootJsonFormat
{
    public SceneryRootData RootData { get; set; }
    public UInt32 FogColor { get; set; }
    public Byte UnkByte { get; set; }
}