using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using SharpGLTF.Animations;
using SharpGLTF.Schema2;
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
using Skin = TT_Lab.Assets.Graphics.Skin;

namespace TT_Lab.AssetData.Code
{
    [ReferencesAssets]
    public class OGIData : AbstractAssetData
    {
        private List<LabURI> _animationLinks = [];
        
        public OGIData()
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
            JointIndices = [0];
            RigidModelIds = [LabURI.Empty];
            SkinInverseMatrices = [mat4.Identity.ToTwin()];
            Skin = LabURI.Empty;
            BlendSkin = LabURI.Empty;
            BoundingBoxBuilders = [];
            BoundingBoxBuilderToJointIndex = [];
        }

        public OGIData(ITwinOGI ogi) : this()
        {
            SetTwinItem(ogi);
        }

        public void LinkAnimations(List<LabURI> animations)
        {
            _animationLinks = animations;
        }

        protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
        {
            base.SaveInternal(dataPath, settings);
            
            ExportGltf(dataPath + ".glb");
        }

        protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
        {
            base.LoadInternal(dataPath, settings);
            
            var model = ModelRoot.Load(dataPath + ".glb");
            
        }

        private void ExportGltf(string path)
        {
            var assetManager = AssetManager.Get();
            var animations = _animationLinks.Select(animLink => assetManager.GetAsset(animLink)).ToList();
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
                var skinData = assetManager.GetAssetData<SkinData>(Skin);
                var meshes = skinData.GetMeshes(root, nodeList);
                foreach (var mesh in meshes)
                {
                    scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.ToArray());
                }
            }
            
            if (BlendSkin != LabURI.Empty)
            {
                var blendSkinData = assetManager.GetAssetData<BlendSkinData>(BlendSkin);
                var meshes = blendSkinData.GetMeshes(root, nodeList);
                foreach (var mesh in meshes)
                {
                    var inst = scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.ToArray());
                    if (mesh.FacesSquashedOnExport)
                    {
                        continue;
                    }
                    
                    foreach (var animation in animations)
                    {
                        var animData = animation.GetData<AnimationData>();
                        if (animData.FacialAnimation.JointSettings.Count <= 0)
                        {
                            continue;
                        }
                        
                        var morphAnimations = animData.GetAnimationKeyframesForMorphAnimation();
                        inst.Content.UseMorphing().SetValue(new float[blendSkinData.BlendsAmount]);
                        var morphAnim = inst.Content.UseMorphing(animation.Name);
                        for (var i = 0; i < morphAnimations.Count; i++)
                        {
                            morphAnim.SetPoint(morphAnimations[i].Time, true, morphAnimations[i].Weights);
                        }
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

                var rigidModelData = assetManager.GetAssetData<RigidModelData>(rigidModelId);
                var model = assetManager.GetAssetData<ModelData>(rigidModelData.Model);
                var meshes = model.GetMeshes(jointNode.Item1, rigidModelData.Materials.Select(matUri => assetManager.GetAssetData<MaterialData>(matUri)).ToList());
                foreach (var mesh in meshes)
                {
                    scene.AddRigidMesh(mesh.Mesh, jointNode.Item1);
                }
            }
            
            foreach (var animation in animations)
            {
                var animData = animation.GetData<AnimationData>();
                var jointCount = Math.Min(nodeList.Count, animData.MainAnimation.JointSettings.Count);
                for (var i = 0; i < jointCount; i++)
                {
                    var node = nodeMap[i];
                    var keyframes = animData.GetAnimationKeyframesForMainAnimation(i, this);
                    node.Item1.SetTranslationTrack(animation.Name, keyframes.Select(key => key.Translation).CreateSampler());
                    node.Item1.SetRotationTrack(animation.Name, keyframes.Select(key => key.Rotation).CreateSampler());
                    node.Item1.SetScaleTrack(animation.Name, keyframes.Select(key => key.Scale).CreateSampler());
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
        public List<TwinBoundingBoxBuilder> BoundingBoxBuilders { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<Byte> BoundingBoxBuilderToJointIndex { get; set; }
        
        public List<Byte> JointIndices { get; set; }
        public List<LabURI> RigidModelIds { get; set; }
        public List<Matrix4> SkinInverseMatrices { get; set; }
        public LabURI Skin { get; set; }
        public LabURI BlendSkin { get; set; }

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
                RigidModelIds.Add(AssetManager.Get().GetUriByTwinId<RigidModel>(package, variant, model));
            }
            BoundingBoxBuilderToJointIndex = CloneUtils.CloneList(ogi.CollisionJointIndices);
            Joints = CloneUtils.DeepClone(ogi.Joints);
            ExitPoints = CloneUtils.DeepClone(ogi.ExitPoints);
            BoundingBoxBuilders = CloneUtils.DeepClone(ogi.Collisions);
            SkinInverseMatrices = CloneUtils.CloneListUnsafe(ogi.SkinInverseBindMatrices);
            Skin = ogi.SkinID != 0 ? AssetManager.Get().GetUriByTwinId<Skin>(package, variant, ogi.SkinID) : LabURI.Empty;
            BlendSkin = ogi.BlendSkinID != 0 ? AssetManager.Get().GetUriByTwinId<BlendSkin>(package, variant, ogi.BlendSkinID) : LabURI.Empty;
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
