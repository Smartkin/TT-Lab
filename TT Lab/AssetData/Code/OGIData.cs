using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using SharpGLTF.Animations;
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

namespace TT_Lab.AssetData.Code
{
    [ReferencesAssets]
    public class OGIData : AbstractAssetData
    {
        public OGIData()
        {
            BoundingBox = new[] { new Vector4(0, 0, 0, 1), new Vector4(10, 10, 10, 1) };
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
            ExitPoints = new List<TwinExitPoint>();
            JointIndices = new List<Byte> { 0 };
            RigidModelIds = new List<LabURI> { LabURI.Empty };
            SkinInverseMatrices = new List<Matrix4> { mat4.Identity.ToTwin() };
            Skin = LabURI.Empty;
            BlendSkin = LabURI.Empty;
            BoundingBoxBuilders = new List<TwinBoundingBoxBuilder>();
            BoundingBoxBuilderToJointIndex = new List<Byte>();
        }

        public OGIData(ITwinOGI ogi) : this()
        {
            SetTwinItem(ogi);
        }

        public void ExportGltf(string path, AnimationData? animation = null)
        {
            var scene = new SharpGLTF.Scenes.SceneBuilder("TwinsanitySkeleton");
            var root = new SharpGLTF.Scenes.NodeBuilder("model_root");
            scene.AddNode(root);

            var nodeMap = new Dictionary<int, (SharpGLTF.Scenes.NodeBuilder, System.Numerics.Matrix4x4)>();
            var rootJoint = new SharpGLTF.Scenes.NodeBuilder();
            nodeMap.Add(Joints[0].Index, (rootJoint, System.Numerics.Matrix4x4.Identity));
            root.AddNode(rootJoint);
            foreach (var joint in Joints.Skip(1))
            {
                var parentJoint = nodeMap[joint.ParentIndex].Item1;
                var jointNode = new SharpGLTF.Scenes.NodeBuilder();
                jointNode.WithLocalTranslation(new System.Numerics.Vector3(joint.LocalTranslation.X,
                        joint.LocalTranslation.Y, joint.LocalTranslation.Z))
                    .WithLocalRotation(new System.Numerics.Quaternion(joint.LocalRotation.X, joint.LocalRotation.Y,
                        joint.LocalRotation.Z, joint.LocalRotation.W));
                parentJoint.AddNode(jointNode);
                nodeMap.Add(joint.Index, (jointNode, jointNode.GetInverseBindMatrix()));
            }

            var nodeList = nodeMap.Values.ToList();

            if (Skin != LabURI.Empty)
            {
                var skinData = AssetManager.Get().GetAssetData<SkinData>(Skin);
                var meshes = skinData.GetMeshes(root, nodeList);
                foreach (var mesh in meshes)
                {
                    scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.ToArray());
                }
            }

            List<MorphAnimationSample>? morphAnimations = null;
            if (animation != null && animation.FacialAnimation.JointSettings.Count > 0)
            {
                morphAnimations = animation.GetAnimationKeyframesForMorphAnimation();
            }

            if (BlendSkin != LabURI.Empty)
            {
                var blendSkinData = AssetManager.Get().GetAssetData<BlendSkinData>(BlendSkin);
                var meshes = blendSkinData.GetMeshes(root, nodeList);
                foreach (var mesh in meshes)
                {
                    var inst = scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.ToArray());
                    if (mesh.FacesSquashedOnExport || morphAnimations == null)
                    {
                        continue;
                    }
                    
                    inst.Content.UseMorphing().SetValue(new float[blendSkinData.BlendsAmount]);
                    var morphAnim = inst.Content.UseMorphing("EXPORTED_ANIMATION");
                    for (var i = 0; i < morphAnimations.Count; i++)
                    {
                        morphAnim.SetPoint(morphAnimations[i].Time, true, morphAnimations[i].Weights);
                    }
                }
            }

            var jointIndex = 0;
            foreach (var rigidModelId in RigidModelIds)
            {
                var jointNode = nodeMap[JointIndices[jointIndex++]];
                if (rigidModelId == LabURI.Empty)
                {
                    continue;
                }

                var rigidModelData = AssetManager.Get().GetAssetData<RigidModelData>(rigidModelId);
                var model = AssetManager.Get().GetAssetData<ModelData>(rigidModelData.Model);
                var meshes = model.GetMeshes(jointNode.Item1, rigidModelData.Materials.Select(matUri => AssetManager.Get().GetAssetData<MaterialData>(matUri)).ToList());
                foreach (var mesh in meshes)
                {
                    scene.AddRigidMesh(mesh.Mesh, jointNode.Item1);
                }
            }
            
            if (animation != null)
            {
                for (var i = 0; i < nodeList.Count; i++)
                {
                    var node = nodeMap[i];
                    var keyframes = animation.GetAnimationKeyframesForMainAnimation(i, this);
                    node.Item1.SetTranslationTrack("EXPORTED_ANIMATION", keyframes.Select(key => key.Translation).CreateSampler());
                    node.Item1.SetRotationTrack("EXPORTED_ANIMATION", keyframes.Select(key => key.Rotation).CreateSampler());
                    node.Item1.SetScaleTrack("EXPORTED_ANIMATION", keyframes.Select(key => key.Scale).CreateSampler());
                }
            }

            var resultModel = scene.ToGltf2();
            resultModel.SaveGLB(path);
        }

        [JsonProperty(Required = Required.Always)]
        public Vector4[] BoundingBox { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<TwinJoint> Joints { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<TwinExitPoint> ExitPoints { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Byte> JointIndices { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> RigidModelIds { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Matrix4> SkinInverseMatrices { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI Skin { get; set; }
        [JsonProperty(Required = Required.Always)]
        public LabURI BlendSkin { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Byte> BoundingBoxBuilderToJointIndex { get; set; }

        protected override void Dispose(Boolean disposing)
        {
            Joints.Clear();
            ExitPoints.Clear();
            JointIndices.Clear();
            RigidModelIds.Clear();
            SkinInverseMatrices.Clear();
            BoundingBoxBuilders.Clear();
            BoundingBoxBuilderToJointIndex.Clear();
        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinOGI ogi = GetTwinItem<ITwinOGI>();
            BoundingBox = CloneUtils.CloneArray(ogi.BoundingBox);
            JointIndices = CloneUtils.CloneList(ogi.JointIndices);
            RigidModelIds = new List<LabURI>();
            foreach (var model in ogi.RigidModelIds)
            {
                RigidModelIds.Add(AssetManager.Get().GetUri(package, typeof(RigidModel).Name, variant, model));
            }
            BoundingBoxBuilderToJointIndex = CloneUtils.CloneList(ogi.CollisionJointIndices);
            Joints = CloneUtils.DeepClone(ogi.Joints);
            ExitPoints = CloneUtils.DeepClone(ogi.ExitPoints);
            BoundingBoxBuilders = CloneUtils.DeepClone(ogi.Collisions);
            SkinInverseMatrices = CloneUtils.CloneListUnsafe(ogi.SkinInverseBindMatrices);
            Skin = ogi.SkinID != 0 ? AssetManager.Get().GetUri(package, typeof(Skin).Name, variant, ogi.SkinID) : LabURI.Empty;
            BlendSkin = ogi.BlendSkinID != 0 ? AssetManager.Get().GetUri(package, typeof(BlendSkin).Name, variant, ogi.BlendSkinID) : LabURI.Empty;
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

            writer.Write(JointIndices.Count);
            foreach (var jointIndex in JointIndices)
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
}
