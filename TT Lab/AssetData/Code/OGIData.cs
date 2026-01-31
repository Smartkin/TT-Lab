using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using GlmSharp;
using SharpGLTF.Animations;
using SharpGLTF.Schema2;
using Tmds.DBus.Protocol;
using TT_Lab.AssetData.Graphics;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Animation = TT_Lab.Assets.Code.Animation;
using Material = SharpGLTF.Schema2.Material;
using Mesh = SharpGLTF.Schema2.Mesh;
using Skin = TT_Lab.Assets.Graphics.Skin;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Code;

[ReferencesAssets]
public class OGIData : AbstractAssetData
{
    public OGIData(IAsset asset) : base(asset)
    {
        BoundingBox = [new Vector4(0, 0, 0, 1), new Vector4(10, 10, 10, 1)];
        var rootJoint = new TwinJoint
        {
            Index = 0,
            LocalRotation = new Vector4(0, 0, 0, 1),
            LocalTranslation = new Vector4(0, 0, 0, 1),
            ParentIndex = 255
        };
        Joints = new List<TwinJoint>
        {
            rootJoint
        };
        ExitPoints = [];
        RigidModelJointIndices = [0];
        RigidModelIds = [LabURI.Empty];
        SkinInverseMatrices = [mat4.Identity.ToTwin()];
        Skin = LabURI.Empty;
        BlendSkin = LabURI.Empty;
        BoundingBoxBuilders = [];
        BoundingBoxBuilderToJointIndex = [];
        AnimationLinks = [];
    }

    public OGIData(IAsset asset, ITwinOGI ogi) : this(asset)
    {
        SetTwinItem(ogi);
    }

    public void LinkAnimations(List<LabURI> animations)
    {
        AnimationLinks = animations;
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        base.SaveInternal(dataPath, settings);
            
        ExportGltf(dataPath + ".glb");
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        base.LoadInternal(dataPath, settings);
        
        ImportGltf(dataPath);
    }

    private void ImportGltf(string dataPath)
    {
        Joints.Clear();
        RigidModelJointIndices.Clear();
        RigidModelIds.Clear();
        SkinInverseMatrices.Clear();
        Skin = LabURI.Empty;
        BlendSkin = LabURI.Empty;
        var model = ModelRoot.Load($"{dataPath}.glb");
        var rigidMeshes = new List<List<Mesh>>();
        var rigidMaterials = new List<List<Material>>();
        var skinMeshes = new List<Mesh>();
        var skinMaterials = new List<Material>();
        var blendSkinMeshes = new List<Mesh>();
        var blendMaterials = new List<Material>();
        var materialDescs = model.DefaultScene.VisualChildren.First(n => n.Name == "MATERIAL_DESCS");
        ExtractSkinsAndBlendSkins(model, skinMeshes, blendSkinMeshes, blendMaterials, skinMaterials);
        TraverseNodeTree(model, model.DefaultScene.VisualChildren.FirstOrDefault(), rigidMeshes, rigidMaterials);

        var assetManager = AssetManager.Get();
        
        if (blendSkinMeshes.Count > 0)
        {
            var labSkin = new BlendSkin
            {
                Package = Owner.Package,
                InvariantName = $"BlendSkin_{Owner.Name}",
                Alias = $"BlendSkin_{Owner.Name}"
            };
            assetManager.AddAsset(labSkin);
            
            var labMaterials = new List<GltfMaterialLabUri>();
            foreach (var material in blendMaterials)
            {
                var materialTokens = material.Name.Split(GraphicsHelpers.MaterialTokenDivider, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (materialTokens.Length < 4 || materialTokens[0] != "BLEND_SKIN" || materialTokens[1] != "MATERIAL")
                {
                    Log.WriteLine($"Misconfigured material {material.Name}! Empty one will be used instead.", Log.LogType.Error);
                    labMaterials.Add(new GltfMaterialLabUri(material.LogicalIndex, LabURI.Empty));
                    continue;
                }

                if (materialTokens[2] == "NULL")
                {
                    labMaterials.Add(new GltfMaterialLabUri(material.LogicalIndex, LabURI.Empty));
                    continue;
                }

                var materialIndex = int.Parse(materialTokens[3]);
                while (labMaterials.Count <= materialIndex)
                {
                    labMaterials.Add(new GltfMaterialLabUri(-1, LabURI.Empty));
                }

                if (labMaterials[materialIndex].MaterialUri != LabURI.Empty)
                {
                    continue;
                }
                
                var labMaterial = new TT_Lab.Assets.Graphics.Material
                {
                    Package = labSkin.Package,
                    InvariantName = $"Material_{labSkin.Name}_{materialTokens[2]}",
                    Alias = $"Material_{labSkin.Name}_{materialTokens[2]}"
                };
                assetManager.AddAsset(labMaterial);
                
                labMaterials[materialIndex] = new GltfMaterialLabUri(material.LogicalIndex, labMaterial.URI);
                
                labMaterial.SetData(MaterialData.LoadFromGltf(labMaterial, 
                    materialDescs.VisualChildren.First(n => n.Name.Contains($"MATERIAL_DESC_FOR_BLEND_{materialIndex}")),
                    blendMaterials.Where(m => m.Name.Contains($"{materialTokens[0]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[1]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[2]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[3]}")).ToList()));
            }
            
            var blendSkinData = new BlendSkinData(labSkin);
            blendSkinData.LoadFromGltf(blendMaterials.Distinct().ToList(), blendSkinMeshes, labMaterials);
            labSkin.SetData(blendSkinData);
            BlendSkin = labSkin.URI;
        }
        
        if (skinMeshes.Count > 0)
        {
            var labSkin = new Skin
            {
                Package = Owner.Package,
                InvariantName = $"Skin_{Owner.Name}",
                Alias = $"Skin_{Owner.Name}"
            };
            assetManager.AddAsset(labSkin);
            
            var labMaterials = new List<LabURI>();
            foreach (var material in skinMaterials)
            {
                var materialTokens = material.Name.Split(GraphicsHelpers.MaterialTokenDivider, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (materialTokens.Length < 4 || materialTokens[0] != "SKIN" || materialTokens[1] != "MATERIAL")
                {
                    Log.WriteLine($"Misconfigured material {material.Name}! Empty one will be used instead.", Log.LogType.Error);
                    labMaterials.Add(LabURI.Empty);
                    continue;
                }

                if (materialTokens[2] == "NULL")
                {
                    labMaterials.Add(LabURI.Empty);
                    continue;
                }

                var materialIndex = int.Parse(materialTokens[3]);
                while (labMaterials.Count <= materialIndex)
                {
                    labMaterials.Add(LabURI.Empty);
                }

                if (labMaterials[materialIndex] != LabURI.Empty)
                {
                    continue;
                }
                
                var labMaterial = new TT_Lab.Assets.Graphics.Material
                {
                    Package = labSkin.Package,
                    InvariantName = $"Material_{labSkin.Name}_{materialTokens[2]}",
                    Alias = $"Material_{labSkin.Name}_{materialTokens[2]}"
                };
                assetManager.AddAsset(labMaterial);
                
                labMaterials[materialIndex] = labMaterial.URI;
                
                labMaterial.SetData(MaterialData.LoadFromGltf(labMaterial, 
                    materialDescs.VisualChildren.First(n => n.Name.Contains($"MATERIAL_DESC_FOR_SKIN_{materialIndex}")),
                    skinMaterials.Where(m => m.Name.Contains($"{materialTokens[0]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[1]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[2]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[3]}")).ToList()));
            }
            
            var skinData = new SkinData(labSkin);
            skinData.LoadFromGltf(skinMeshes, labMaterials);
            labSkin.SetData(skinData);
            Skin = labSkin.URI;
        }

        var resultingRigidMeshes = new List<List<Mesh>>();
        var resultingRigidMaterials = new List<List<Material>>();
        foreach (var rigidMesh in rigidMeshes)
        {
            foreach (var mesh in rigidMesh)
            {
                var meshTokens = mesh.Name.Split(GraphicsHelpers.MeshTokenDivider, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (meshTokens.Length < 3 || meshTokens[0] != "mesh")
                {
                    Log.WriteLine($"Misconfigured mesh {mesh.Name}! Empty one will be used instead.", Log.LogType.Error);
                    continue;
                }
                
                var rigidIndex = int.Parse(meshTokens[1]);
                while (resultingRigidMeshes.Count <= rigidIndex)
                {
                    resultingRigidMeshes.Add([]);
                    resultingRigidMaterials.Add([]);
                }
                
                var meshesList = resultingRigidMeshes[rigidIndex];
                var materialsList = resultingRigidMaterials[rigidIndex];
                var meshIndex = int.Parse(meshTokens[2]);
                while (meshesList.Count <= meshIndex)
                {
                    meshesList.Add(null);
                    materialsList.Add(null);
                }
                
                meshesList[meshIndex] = mesh;
                materialsList[meshIndex] = mesh.Primitives.Select(prim => prim.Material).First();
            }
        }

        var modelId = 0;
        foreach (var rigidMesh in resultingRigidMeshes)
        {
            var labModel = new Model
            {
                Package = Owner.Package,
                InvariantName = $"Model_{modelId}_{Owner.Name}",
                Alias = $"Model_{modelId}_{Owner.Name}"
            };
            assetManager.AddAsset(labModel);
            
            var modelData = new ModelData(labModel);
            modelData.LoadFromGltfMeshes(rigidMesh);
            labModel.SetData(modelData);

            var labRigidModel = new RigidModel
            {
                Package = Owner.Package,
                InvariantName = $"RigidModel_{modelId}_{Owner.Name}",
                Alias = $"RigidModel_{modelId}_{Owner.Name}"
            };
            assetManager.AddAsset(labRigidModel);
            
            var materials = resultingRigidMaterials[modelId];
            var labMaterials = new List<LabURI>();
            foreach (var material in materials)
            {
                var materialTokens = material.Name.Split(GraphicsHelpers.MaterialTokenDivider, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (materialTokens.Length < 4 || materialTokens[0] != "RIGID" || materialTokens[1] != "MATERIAL")
                {
                    Log.WriteLine($"Misconfigured material {material.Name}! Empty one will be used instead.", Log.LogType.Error);
                    labMaterials.Add(LabURI.Empty);
                    continue;
                }

                if (materialTokens[2] == "NULL")
                {
                    labMaterials.Add(LabURI.Empty);
                    continue;
                }

                var materialIndex = int.Parse(materialTokens[3]);
                while (labMaterials.Count <= materialIndex)
                {
                    labMaterials.Add(LabURI.Empty);
                }

                if (labMaterials[materialIndex] != LabURI.Empty)
                {
                    continue;
                }
                
                var labMaterial = new TT_Lab.Assets.Graphics.Material
                {
                    Package = labRigidModel.Package,
                    InvariantName = $"Material_{labRigidModel.Name}_{materialTokens[2]}",
                    Alias = $"Material_{labRigidModel.Name}_{materialTokens[2]}"
                };
                assetManager.AddAsset(labMaterial);
                
                labMaterials[materialIndex] = labMaterial.URI;
                
                labMaterial.SetData(MaterialData.LoadFromGltf(labMaterial, 
                    materialDescs.VisualChildren.First(n => n.Name.Contains($"MATERIAL_DESC_FOR_RIGID_{modelId}_{materialIndex}")),
                    materials.Where(m => m.Name.Contains($"{materialTokens[0]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[1]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[2]}{GraphicsHelpers.MaterialTokenDivider}{materialTokens[3]}")).ToList()));
            }
            
            var rigidModelData = new RigidModelData(labRigidModel);
            rigidModelData.Model = labModel.URI;
            rigidModelData.Materials = labMaterials;
            labRigidModel.SetData(rigidModelData);

            var meshParent = rigidMesh[0].VisualParents.First();
            if (meshParent.Name == null)
            {
                meshParent = meshParent.VisualParent;
            }
            var jointIndex = meshParent.Name == "SKELETON_ROOT"
                ? 0
                : int.Parse(meshParent.Name.Split('_')[^1]);
            RigidModelJointIndices.Add((byte)jointIndex);
            RigidModelIds.Add(labRigidModel.URI);

            modelId++;
        }
    }

    private void ExtractSkinsAndBlendSkins(ModelRoot model, List<Mesh> skinMeshes, List<Mesh> blendSkinMeshes, List<Material> blendMaterials, List<Material> skinMaterials)
    {
        var skinned = model.LogicalMeshes.Where(m => m.Extras != null && m.Extras.Deserialize<MeshExtraInfo>()!.Type == MeshExportType.Skinned).ToList();
        var blendSkinned = model.LogicalMeshes.Where(m => m.Extras != null && m.Extras.Deserialize<MeshExtraInfo>()!.Type == MeshExportType.BlendSkinned).ToList();
        var blendMats = blendSkinned.SelectMany(m => m.Primitives.Select(prim => prim.Material)).Distinct().ToList();
        var skinMats = skinned.SelectMany(m => m.Primitives.Select(prim => prim.Material)).Distinct().ToList();
        blendSkinMeshes.AddRange(blendSkinned.DistinctBy(m => m.Name));
        blendMaterials.AddRange(blendMats);
        skinMeshes.AddRange(skinned.DistinctBy(m => m.Name));
        skinMaterials.AddRange(skinMats);
    }

    private void TraverseNodeTree(ModelRoot model, Node? node, List<List<Mesh>> rigidMeshes, List<List<Material>> rigidMaterials)
    {
        if (node == null)
        {
            return;
        }
        
        var meshes = model.LogicalMeshes.Where(m => m.VisualParents.All(n => n.LogicalIndex == node.LogicalIndex)).ToList();
        var rigids = meshes.Where(m => m.Extras != null && m.Extras.Deserialize<MeshExtraInfo>()!.Type == MeshExportType.Rigid).ToList();
        var rigidMats = rigids.SelectMany(m => m.Primitives.Select(prim => prim.Material)).Distinct().ToList();
        if (rigids.Count > 0)
        {
            rigidMeshes.Add(rigids.DistinctBy(m => m.Name).ToList());
            rigidMaterials.Add(rigidMats);
        }

        if (string.IsNullOrEmpty(node.Name))
        {
            return;
        }
        
        var jointIndex = (byte)0;
        if (node.Name != "SKELETON_ROOT")
        {
            jointIndex = byte.Parse(node.Name.Split('_')[^1]);
        }
        
        while (Joints.Count <= jointIndex)
        {
            Joints.Add(new TwinJoint());
        }
        
        var twinJoint = Joints[jointIndex];
        var parentIndex = 255;
        if (node.VisualParent != null)
        {
            parentIndex = node.VisualParent.Name == "SKELETON_ROOT" ? 0 : int.Parse(node.VisualParent.Name.Split('_')[^1]);
        }
        twinJoint.ParentIndex = parentIndex;
        if (twinJoint.ParentIndex < 0)
        {
            twinJoint.ParentIndex = 255;
        }
        var additionalAnimationJson = node.Extras;
        twinJoint.AdditionalAnimationRotation = additionalAnimationJson != null ? new Vector4((float)additionalAnimationJson["X"]!, (float)additionalAnimationJson["Y"]!, (float)additionalAnimationJson["Z"]!, (float)additionalAnimationJson["W"]!) : new Vector4(0, 0, 0, 1);
        twinJoint.ChildrenAmt1 = node.VisualChildren.Count();
        twinJoint.Index = jointIndex;
        twinJoint.LocalTranslation = node.LocalTransform.Translation.ToTwin();
        twinJoint.LocalRotation = node.LocalTransform.Rotation.ToTwin();
        twinJoint.WorldTranslation = node.WorldMatrix.Translation.ToTwin();
        Matrix4x4.Invert(node.WorldMatrix, out var invMatrix);
        SkinInverseMatrices.Add(invMatrix.ToTwin());

        foreach (var child in node.VisualChildren)
        {
            TraverseNodeTree(model, child, rigidMeshes, rigidMaterials);
        }
    }

    private void ExportGltf(string path)
    {
        var assetManager = AssetManager.Get();
        var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanityModel_{Owner.Name}");
        var rootJoint = new SharpGLTF.Scenes.NodeBuilder{
            Extras = System.Text.Json.Nodes.JsonNode.Parse(
                System.Text.Json.JsonSerializer.Serialize(Joints[0].AdditionalAnimationRotation)),
            Name = "SKELETON_ROOT"
        };
        scene.AddNode(rootJoint);

        var materialDescs = new SharpGLTF.Scenes.NodeBuilder
        {
            Name = "MATERIAL_DESCS"
        };

        var nodeMap = new Dictionary<int, GltfBone>
        {
            {
                Joints[0].Index,
                new GltfBone
                {
                    Node = rootJoint,
                    InverseBindMatrix = Matrix4x4.Identity,
                    Parent = null,
                    ParentIndex = -1
                }
            }
        };
        foreach (var joint in Joints.Skip(1))
        {
            var parentJoint = nodeMap[joint.ParentIndex].Node;
            var jointNode = new SharpGLTF.Scenes.NodeBuilder
            {
                Extras = System.Text.Json.Nodes.JsonNode.Parse(
                    System.Text.Json.JsonSerializer.Serialize(joint.AdditionalAnimationRotation))
            };
            jointNode.Name = $"BONE_{joint.Index}";
            jointNode.WithLocalTranslation(new System.Numerics.Vector3(joint.LocalTranslation.X,
                    joint.LocalTranslation.Y, joint.LocalTranslation.Z))
                .WithLocalRotation(new System.Numerics.Quaternion(joint.LocalRotation.X, joint.LocalRotation.Y,
                    joint.LocalRotation.Z, joint.LocalRotation.W));
            parentJoint.AddNode(jointNode);
            nodeMap.Add(joint.Index, new GltfBone
            {
                Node = jointNode,
                InverseBindMatrix = jointNode.GetInverseBindMatrix(),
                Parent = parentJoint,
                ParentIndex = joint.ParentIndex
            });
        }

        var nodeList = nodeMap.Values.ToList();

        if (Skin != LabURI.Empty)
        {
            var skinData = assetManager.GetAssetData<SkinData>(Skin);
            var meshes = skinData.GetMeshes(rootJoint, nodeList);
            foreach (var mesh in meshes)
            {
                scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.Select(j => (j.Node, j.InverseBindMatrix)).ToArray());
            }

            var subSkinIndex = 0;
            foreach (var subSkin in skinData.SubSkins)
            {
                var material = assetManager.GetAssetData<MaterialData>(subSkin.Material);
                var materialDescNode = new SharpGLTF.Scenes.NodeBuilder
                {
                    Extras = material.GetJsonFormat(),
                    Name = $"MATERIAL_DESC_FOR_SKIN_{subSkinIndex++}_{material.Name}"
                };
                
                materialDescs.AddNode(materialDescNode);
            }
        }
        
        if (BlendSkin != LabURI.Empty)
        {
            var blendSkinData = assetManager.GetAssetData<BlendSkinData>(BlendSkin);
            var meshes = blendSkinData.GetMeshes(rootJoint, nodeList);
            foreach (var mesh in meshes)
            {
                var inst = scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.Select(j => (j.Node, j.InverseBindMatrix)).ToArray());
                // if (mesh.FacesSquashedOnExport)
                // {
                //     continue;
                // }
                //
                // foreach (var animation in animations)
                // {
                //     var animData = animation.GetData<AnimationData>();
                //     if (animData.FacialAnimation.JointSettings.Count <= 0)
                //     {
                //         continue;
                //     }
                //     
                //     var morphAnimations = animData.GetAnimationKeyframesForMorphAnimation();
                //     inst.Content.UseMorphing().SetValue(new float[blendSkinData.BlendsAmount]);
                //     var morphAnim = inst.Content.UseMorphing(animation.Name);
                //     for (var i = 0; i < morphAnimations.Count; i++)
                //     {
                //         morphAnim.SetPoint(morphAnimations[i].Time, true, morphAnimations[i].Weights);
                //     }
                // }
            }
            
            var blendIndex = 0;
            foreach (var blend in blendSkinData.Blends)
            {
                var material = assetManager.GetAssetData<MaterialData>(blend.Material);
                var materialDescNode = new SharpGLTF.Scenes.NodeBuilder
                {
                    Extras = material.GetJsonFormat(),
                    Name = $"MATERIAL_DESC_FOR_BLEND_{blendIndex++}_{material.Name}"
                };
                
                materialDescs.AddNode(materialDescNode);
            }
        }

        var jointIndex = 0;
        var rigidModel = 0;
        foreach (var rigidModelId in RigidModelIds)
        {
            var jointNode = nodeMap[RigidModelJointIndices[jointIndex++]];
            if (rigidModelId == LabURI.Empty)
            {
                continue;
            }

            var rigidModelData = assetManager.GetAssetData<RigidModelData>(rigidModelId);
            var model = assetManager.GetAssetData<ModelData>(rigidModelData.Model);
            var meshes = model.GetMeshes(jointNode.Node, rigidModelData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
            foreach (var mesh in meshes)
            {
                mesh.Mesh.Name = mesh.Mesh.Name.Replace("RIGIDIDPLACEHOLDER", rigidModel.ToString());
                scene.AddRigidMesh(mesh.Mesh, jointNode.Node);
            }
            
            var subModelId = 0;
            foreach (var vertex in model.Vertexes)
            {
                var material = assetManager.GetAssetData<MaterialData>(rigidModelData.Materials[subModelId]);
                var materialDescNode = new SharpGLTF.Scenes.NodeBuilder
                {
                    Extras = material.GetJsonFormat(),
                    Name = $"MATERIAL_DESC_FOR_RIGID_{rigidModel}_{subModelId}_{material.Name}"
                };
        
                materialDescs.AddNode(materialDescNode);

                subModelId++;
            }
            
            rigidModel++;
        }
        //
        // var animNodeMap = new Dictionary<int, AnimationNodeMetadata>();
        // for (var i = 0; i < nodeList.Count; ++i)
        // {
        //     var nodeMetadata = new AnimationNodeMetadata();
        //     animNodeMap.Add(i, nodeMetadata);
        // }
        // foreach (var animation in animations)
        // {
        //     var animData = animation.GetData<AnimationData>();
        //     var jointCount = Math.Min(nodeList.Count, animData.MainAnimation.JointSettings.Count);
        //     for (var i = 0; i < jointCount; i++)
        //     {
        //         var node = nodeMap[i];
        //         var isIndependentScaling = animData.MainAnimation.JointSettings[i].IndependentScaling;
        //         animNodeMap[i].NodeHasIndependentScaling.Add(animation.Name, isIndependentScaling);
        //         var keyframes = animData.GetAnimationKeyframesForMainAnimation(i, this, node.ParentIndex);
        //         node.Node.SetTranslationTrack(animation.Name, keyframes.Select(key => key.Translation).CreateSampler());
        //         node.Node.SetRotationTrack(animation.Name, keyframes.Select(key => key.Rotation).CreateSampler());
        //         node.Node.SetScaleTrack(animation.Name, keyframes.Select(key => key.Scale).CreateSampler());
        //     }
        // }
        //
        // foreach (var (nodeIndex, metadata) in animNodeMap)
        // {
        //     var node = nodeMap[nodeIndex];
        //     node.Node.Extras = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(metadata));
        // }
        
        scene.AddNode(materialDescs);

        var resultModel = scene.ToGltf2();
        resultModel.SaveGLB(path);
    }

    // private class AnimationNodeMetadata
    // {
    //     public Dictionary<string, bool> NodeHasIndependentScaling { get; set; } = [];
    // }

    [JsonProperty(Required = Required.Always)]
    public Vector4[] BoundingBox { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<TwinExitPoint> ExitPoints { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<Byte> BoundingBoxBuilderToJointIndex { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<LabURI> AnimationLinks { get; set; }
    
    public List<TwinJoint> Joints { get; set; }
    public List<Byte> RigidModelJointIndices { get; set; }
    public List<LabURI> RigidModelIds { get; set; }
    public List<Matrix4> SkinInverseMatrices { get; set; }
    public LabURI Skin { get; set; }
    public LabURI BlendSkin { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Joints.Clear();
        ExitPoints.Clear();
        RigidModelJointIndices.Clear();
        RigidModelIds.Clear();
        SkinInverseMatrices.Clear();
        BoundingBoxBuilders.Clear();
        BoundingBoxBuilderToJointIndex.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinOGI ogi = GetTwinItem<ITwinOGI>();
        BoundingBox = CloneUtils.CloneArray(ogi.BoundingBox);
        RigidModelJointIndices = CloneUtils.CloneList(ogi.JointIndices);
        RigidModelIds = new List<LabURI>();
        foreach (var model in ogi.RigidModelIds)
        {
            RigidModelIds.Add(AssetManager.Get().GetUriByTwinId<RigidModel>(Owner, model));
        }
        BoundingBoxBuilderToJointIndex = CloneUtils.CloneList(ogi.CollisionJointIndices);
        Joints = CloneUtils.DeepClone(ogi.Joints);
        ExitPoints = CloneUtils.DeepClone(ogi.ExitPoints);
        BoundingBoxBuilders = CloneUtils.DeepClone(ogi.Collisions);
        SkinInverseMatrices = CloneUtils.CloneListUnsafe(ogi.SkinInverseBindMatrices);
        Skin = ogi.SkinID != 0 ? AssetManager.Get().GetUriByTwinId<Skin>(Owner, ogi.SkinID) : LabURI.Empty;
        BlendSkin = ogi.BlendSkinID != 0 ? AssetManager.Get().GetUriByTwinId<BlendSkin>(Owner, ogi.BlendSkinID) : LabURI.Empty;
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        foreach (var vec in BoundingBox)
        {
            vec.Write(writer);
        }

        writer.Write(RigidModelJointIndices.Count);
        foreach (var jointIndex in RigidModelJointIndices)
        {
            writer.Write(jointIndex);
        }

        writer.Write(Joints.Count);
        foreach (var joint in Joints)
        {
            joint.Write(writer);
        }

        writer.Write(RigidModelIds.Count);
        foreach (var rigidModel in RigidModelIds)
        {
            writer.Write(assetManager.GetAsset(rigidModel).ID);
        }

        writer.Write(ExitPoints.Count);
        foreach (var exitPoint in ExitPoints)
        {
            exitPoint.Write(writer);
        }

        writer.Write(SkinInverseMatrices.Count);
        foreach (var matrix in SkinInverseMatrices)
        {
            matrix.Write(writer);
        }

        writer.Write(BoundingBoxBuilders.Count);
        foreach (var builder in BoundingBoxBuilders)
        {
            builder.Write(writer);
        }

        writer.Write(BoundingBoxBuilderToJointIndex.Count);
        foreach (var idx in BoundingBoxBuilderToJointIndex)
        {
            writer.Write(idx);
        }

        writer.Write(Skin == LabURI.Empty ? 0U : assetManager.GetAsset(Skin).ID);
        writer.Write(BlendSkin == LabURI.Empty ? 0U : assetManager.GetAsset(BlendSkin).ID);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateOGI(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
        var blendSkinScale = UInt32.MaxValue;
        if (RigidModelIds.Count > 0)
        {
            var rigidModelSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_RIGID_MODELS_SECTION);
            foreach (var rigidModel in RigidModelIds)
            {
                assetManager.GetAsset(rigidModel).ResolveChunkResources(factory, rigidModelSection);
                var rigid = assetManager.GetAsset(rigidModel);
                var model = (ITwinModel)assetManager.GetAsset<Model>(rigid.GetData<RigidModelData>().Model).Export(factory);
                model.Compile();
                var minCoord = model.GetMinSkinCoord();
                if (minCoord < blendSkinScale && minCoord != 0)
                {
                    blendSkinScale = minCoord;
                }
            }
        }
        if (Skin != LabURI.Empty)
        {
            var skinSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKINS_SECTION);
            assetManager.GetAsset(Skin).ResolveChunkResources(factory, skinSection);
            var skinAsset = assetManager.GetAssetData<SkinData>(Skin);
            var skin = (ITwinSkin)skinAsset.Export(factory);
            skin.Compile();
            var minCoord = skin.GetMinSkinCoord();
            if (minCoord < blendSkinScale && minCoord != 0)
            {
                blendSkinScale = minCoord;
            }
        }
        if (BlendSkin != LabURI.Empty)
        {
            var blendSkinSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_BLEND_SKINS_SECTION);
            var blendSkinData = assetManager.GetAssetData<BlendSkinData>(BlendSkin);
            if (blendSkinScale != UInt32.MaxValue)
            {
                blendSkinData.CompileScale = blendSkinScale;
            }
            assetManager.GetAsset(BlendSkin).ResolveChunkResources(factory, blendSkinSection);
        }
        return base.ResolveChunkResources(factory, section, id);
    }
}