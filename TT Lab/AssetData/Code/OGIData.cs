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
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
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
        BoundingBoxBuilders.Clear();
        BoundingBoxBuilderToJointIndex.Clear();
        ExitPoints.Clear();
        Joints.Clear();
        RigidModelJointIndices.Clear();
        RigidModelIds.Clear();
        SkinInverseMatrices.Clear();
        Skin = LabURI.Empty;
        BlendSkin = LabURI.Empty;
        var model = ModelRoot.Load($"{dataPath}.glb");
        var rigidMeshes = new List<Node>();
        var skinMeshes = new List<Mesh>();
        var blendSkinMeshes = new List<Mesh>();
        var materialDescs = model.DefaultScene.VisualChildren.First(n => n.Name == "MATERIAL_DESCS");
        ExtractSkinsAndBlendSkins(model, skinMeshes, blendSkinMeshes);
        TraverseNodeTree(model, model.DefaultScene.VisualChildren.FirstOrDefault(n => n.Name == "SKELETON_ROOT"), rigidMeshes);
        
        ExitPoints.Sort((e1, e2) => (int)e1.ID - (int)e2.ID);

        var ogiJsonData = model.DefaultScene.Extras.Deserialize<OgiGltfData>()!;
        BoundingBox = [ogiJsonData.BoundingBoxTopRight, ogiJsonData.BoundingBoxBottomLeft];
        foreach (var boxBuilderToJointLink in ogiJsonData.BoundingBoxBuilders)
        {
            BoundingBoxBuilders.Add(boxBuilderToJointLink.BoundingBoxBuilder);
            BoundingBoxBuilderToJointIndex.Add(boxBuilderToJointLink.JointIndex);
        }

        var assetManager = AssetManager.Get();
        
        if (blendSkinMeshes.Count > 0)
        {
            var labSkin = new BlendSkin
            {
                Package = Owner.Package,
                InvariantName = $"BlendSkin_{Owner.Name}",
                Alias = $"BlendSkin_{Owner.Name}",
                IsInternal = true
            };
            assetManager.TryAddAsset(labSkin);
            
            var blendSkinData = new BlendSkinData(labSkin);
            blendSkinData.LoadFromGltf(materialDescs, blendSkinMeshes);
            labSkin.SetData(blendSkinData);
            BlendSkin = labSkin.URI;
        }
        
        if (skinMeshes.Count > 0)
        {
            var labSkin = new Skin
            {
                Package = Owner.Package,
                InvariantName = $"Skin_{Owner.Name}",
                Alias = $"Skin_{Owner.Name}",
                IsInternal = true
            };
            assetManager.TryAddAsset(labSkin);
            
            var skinData = new SkinData(labSkin);
            skinData.LoadFromGltf(skinMeshes, materialDescs);
            labSkin.SetData(skinData);
            Skin = labSkin.URI;
        }

        foreach (var rigidMesh in rigidMeshes)
        {
            var labRigidModel = RigidModelData.ImportGltf<RigidModel>(Owner, model, rigidMesh);
            var meshParent = rigidMesh.VisualParent;
            var jointIndex = meshParent.Name == "SKELETON_ROOT"
                ? 0
                : int.Parse(meshParent.Name.Split('_')[^1]);
            RigidModelJointIndices.Add((byte)jointIndex);
            RigidModelIds.Add(labRigidModel.URI);
        }
    }

    private void ExtractSkinsAndBlendSkins(ModelRoot model, List<Mesh> skinMeshes, List<Mesh> blendSkinMeshes)
    {
        var skinned = model.LogicalMeshes.Where(m => m.Extras != null && m.Extras.Deserialize<MeshExtraInfo>()!.Type == MeshExportType.Skinned).ToList();
        var blendSkinned = model.LogicalMeshes.Where(m => m.Extras != null && m.Extras.Deserialize<MeshExtraInfo>()!.Type == MeshExportType.BlendSkinned).ToList();
        blendSkinMeshes.AddRange(blendSkinned.DistinctBy(m => m.Name));
        skinMeshes.AddRange(skinned.DistinctBy(m => m.Name));
    }

    private void TraverseNodeTree(ModelRoot model, Node? node, List<Node> rigidMeshes)
    {
        if (node == null || string.IsNullOrEmpty(node.Name))
        {
            return;
        }

        var rigidNodes = node.VisualChildren.Where(n => n.Name.Contains($"{node.Name}_RIGID_MODEL_")).ToList();
        rigidMeshes.AddRange(rigidNodes);
        
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

        var children = node.VisualChildren.Where(n => !string.IsNullOrEmpty(n.Name) && !n.Name.Contains($"{node.Name}_RIGID_MODEL_")).ToList();
        var additionalAnimationJson = node.Extras;
        var jointJson = new JointJsonFormat();
        if (additionalAnimationJson != null)
        {
            jointJson = additionalAnimationJson.Deserialize<JointJsonFormat>()!;
        }
        twinJoint.AdditionalAnimationRotation = jointJson.AdditionalAnimationRotation;
        twinJoint.ReactId = jointJson.ReactId;
        twinJoint.ChildrenAmt1 = children.Count;
        twinJoint.Index = jointIndex;
        twinJoint.LocalTranslation = node.LocalTransform.Translation.ToTwin();
        twinJoint.LocalRotation = node.LocalTransform.Rotation.ToTwin();
        twinJoint.WorldTranslation = node.WorldMatrix.Translation.ToTwin();
        Matrix4x4.Invert(node.WorldMatrix, out var invMatrix);
        SkinInverseMatrices.Add(invMatrix.ToTwin());

        foreach (var child in children)
        {
            if (!child.Name.Contains("EXIT_POINT"))
            {
                continue;
            }
            
            var exitPoint = new TwinExitPoint
            {
                ID = uint.Parse(child.Name.Split('_')[^1]),
                ParentJointIndex = (uint)twinJoint.Index,
                Matrix = child.LocalMatrix.ToTwin()
            };
            ExitPoints.Add(exitPoint);
        }

        foreach (var child in children)
        {
            if (child.Name.Contains("EXIT_POINT"))
            {
                continue;
            }
            
            TraverseNodeTree(model, child, rigidMeshes);
        }
    }

    private void ExportGltf(string path)
    {
        var assetManager = AssetManager.Get();
        var scene = new SharpGLTF.Scenes.SceneBuilder($"TwinsanityModel_{Owner.Name}")
        {
            Extras = System.Text.Json.JsonSerializer.SerializeToNode(new OgiGltfData(this))
        };
        var rootJoint = new SharpGLTF.Scenes.NodeBuilder{
            Extras = System.Text.Json.JsonSerializer.SerializeToNode(new JointJsonFormat
            {
                AdditionalAnimationRotation = Joints[0].AdditionalAnimationRotation,
                ReactId = Joints[0].ReactId
            }),
            Name = "SKELETON_ROOT"
        };
        rootJoint.WithLocalTranslation(new System.Numerics.Vector3(Joints[0].LocalTranslation.X,
                Joints[0].LocalTranslation.Y, Joints[0].LocalTranslation.Z))
            .WithLocalRotation(new System.Numerics.Quaternion(Joints[0].LocalRotation.X, Joints[0].LocalRotation.Y,
                Joints[0].LocalRotation.Z, Joints[0].LocalRotation.W));
        scene.AddNode(rootJoint);

        foreach (var twinExitPoint in ExitPoints)
        {
            if (twinExitPoint.ParentJointIndex != 0)
            {
                continue;
            }
            
            var exitPoint = new SharpGLTF.Scenes.NodeBuilder($"EXIT_POINT_{twinExitPoint.ID}")
            {
                LocalMatrix = twinExitPoint.Matrix.ToSystem()
            };
            rootJoint.AddNode(exitPoint);
        }

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
                Extras = System.Text.Json.JsonSerializer.SerializeToNode(new JointJsonFormat
                {
                    AdditionalAnimationRotation = joint.AdditionalAnimationRotation,
                    ReactId = joint.ReactId
                })
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
            
            foreach (var twinExitPoint in ExitPoints)
            {
                if (twinExitPoint.ParentJointIndex != joint.Index)
                {
                    continue;
                }
            
                var exitPoint = new SharpGLTF.Scenes.NodeBuilder($"EXIT_POINT_{twinExitPoint.ID}")
                {
                    LocalMatrix = twinExitPoint.Matrix.ToSystem()
                };
                jointNode.AddNode(exitPoint);
            }
        }

        var nodeList = nodeMap.Values.ToList();
        var materialDescs = new SharpGLTF.Scenes.NodeBuilder
        {
            Name = "MATERIAL_DESCS"
        };

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
            rigidModelData.ExportGltf(scene, jointNode.Node, rigidModel.ToString());
            
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
    public List<LabURI> AnimationLinks { get; set; }

    public Vector4[] BoundingBox { get; set; }
    public List<TwinExitPoint> ExitPoints { get; set; }
    public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
    public List<Byte> BoundingBoxBuilderToJointIndex { get; set; }
    public List<TwinJoint> Joints { get; set; }
    public List<Byte> RigidModelJointIndices { get; set; }
    public List<LabURI> RigidModelIds { get; set; }
    public List<Matrix4> SkinInverseMatrices { get; set; }
    public LabURI Skin { get; set; }
    public LabURI BlendSkin { get; set; }

    private class OgiGltfData
    {
        [System.Text.Json.Serialization.JsonConstructor]
        private OgiGltfData() { }

        public OgiGltfData(OGIData data)
        {
            BoundingBoxTopRight = data.BoundingBox[0];
            BoundingBoxBottomLeft = data.BoundingBox[1];
            var bbBuilderIndex = 0;
            foreach (var twinBoundingBoxBuilder in data.BoundingBoxBuilders)
            {
                BoundingBoxBuilders.Add(new BoxBuilderToJointLink
                {
                    BoundingBoxBuilder = twinBoundingBoxBuilder,
                    JointIndex = data.BoundingBoxBuilderToJointIndex[bbBuilderIndex++]
                });
            }
        }

        [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
        public Vector4 BoundingBoxTopRight { get; set; } = new(-10, -10, -10, 1);
        
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
        public Vector4 BoundingBoxBottomLeft { get; set; } = new(10, 10, 10, 1);

        [System.Text.Json.Serialization.JsonConverter(typeof(JsonListConverter<BoxBuilderToJointLink>))]
        public List<BoxBuilderToJointLink> BoundingBoxBuilders { get; set; } = [];
    }

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
            writer.Write(assetManager.GetAsset(rigidModel).ExportTwinID);
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

        writer.Write(Skin == LabURI.Empty ? 0U : assetManager.GetAsset(Skin).ExportTwinID);
        writer.Write(BlendSkin == LabURI.Empty ? 0U : assetManager.GetAsset(BlendSkin).ExportTwinID);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateOGI(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
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
        return base.ResolveChunkResources(factory, section, id, layoutId);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var context = viewportContext.RenderContext;
        var ogiRender = new OGI(context, context.SkeletonManager, context.MeshService, this);
        var viewportObject = new ViewportObject(new EditableObject(context, ogiRender, "OGIRender"), property.Name, property);
        return [viewportObject];
    }
}

public class JointJsonFormat
{
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonVector4Converter))]
    public Vector4 AdditionalAnimationRotation { get; set; } = new(0, 0, 0, 1);

    public Int32 ReactId { get; set; } = 255;
}