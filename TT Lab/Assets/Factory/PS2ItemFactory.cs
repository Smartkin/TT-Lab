using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.Base;
using Twinsanity.TwinsanityInterchange.Implementations.PS2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.Graphics;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Sections.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using static Twinsanity.TwinsanityInterchange.Common.AgentLab.TwinBehaviourAssigner;

namespace TT_Lab.Assets.Factory
{
    public class PS2ItemFactory : ITwinItemFactory
    {
        public ITwinAIPath GenerateAIPath(Stream stream)
        {
            var aiPath = new PS2AnyAIPath();
            using var reader = new BinaryReader(stream);
            aiPath.Read(reader, (Int32)stream.Length);
            return aiPath;
        }

        public ITwinAIPosition GenerateAIPosition(Stream stream)
        {
            var aiPosition = new PS2AnyAIPosition();
            using var reader = new BinaryReader(stream);
            aiPosition.Read(reader, (Int32)stream.Length);
            return aiPosition;
        }

        public ITwinAnimation GenerateAnimation(Stream stream)
        {
            var animation = new PS2AnyAnimation();
            using var reader = new BinaryReader(stream);
            animation.TotalFrames = reader.ReadUInt16();
            animation.DefaultFPS = reader.ReadByte();
            animation.MainAnimation = new Twinsanity.TwinsanityInterchange.Common.Animation.TwinAnimation();
            animation.MainAnimation.Read(reader, (Int32)stream.Length);
            animation.FacialAnimation = new Twinsanity.TwinsanityInterchange.Common.Animation.TwinMorphAnimation();
            animation.FacialAnimation.Read(reader, (Int32)stream.Length);
            animation.HasAnimationData = true;
            animation.HasFacialAnimationData = animation.FacialAnimation.TotalFrames != 0;
            return animation;
        }

        public ITwinBehaviourCommandsSequence GenerateBehaviourCommandsSequence(Stream stream)
        {
            var sequence = new PS2BehaviourCommandsSequence();
            using var reader = new StreamReader(stream);
            sequence.ReadText(reader);
            return sequence;
        }

        public ITwinBehaviourGraph GenerateBehaviourGraph(Stream stream)
        {
            var graph = new PS2BehaviourGraph();
            using var reader = new StreamReader(stream);
            graph.ReadText(reader);
            return graph;
        }

        public TwinBehaviourStarter GenerateBehaviourStarter(Stream stream)
        {
            var starter = new TwinBehaviourStarter();
            using var reader = new BinaryReader(stream);
            // HACK: Read the internal TwinBehaviourWrapper
            starter.Read(reader, (Int32)stream.Length);
            stream.Position = 4;

            starter.Assigners.Clear();
            var assignersAmount = reader.ReadUInt32();
            for (Int32 i = 0; i < assignersAmount; i++)
            {
                var assigner = new TwinBehaviourAssigner();
                assigner.Behaviour = reader.ReadInt32();
                assigner.GlobalObjectId = reader.ReadUInt16();
                assigner.AssignType = (AssignTypeID)reader.ReadUInt32();
                assigner.AssignLocality = (AssignLocalityID)reader.ReadUInt32();
                assigner.AssignStatus = (AssignStatusID)reader.ReadUInt32();
                assigner.AssignPreference = (AssignPreferenceID)reader.ReadUInt32();
                starter.Assigners.Add(assigner);
            }
            return starter;
        }

        public ITwinBlendSkin GenerateBlendSkin(Int32 blendsAmount, List<SubBlendData> blends, UInt32? compileScale)
        {
            var assetManager = AssetManager.Get();
            var blendSkin = new PS2AnyBlendSkin
            {
                BlendsAmount = blendsAmount,
            };
            if (compileScale.HasValue)
            {
                blendSkin.CompileScale = compileScale.Value;
            }

            foreach (var blend in blends)
            {
                var subBlend = new PS2SubBlendSkin(blendsAmount)
                {
                    Material = assetManager.GetAsset(blend.Material).ID,
                };

                foreach (var model in blend.Models)
                {
                    foreach (var meshlet in model.Mesh.Meshlets)
                    {
                        var submodel = new PS2BlendSkinModel(blendsAmount)
                        {
                            Vertexes = new(),
                            UVW = new(),
                            Colors = new(),
                            SkinJoints = new(),
                            Faces = new(),
                            BlendShape = new Vector3(model.BlendShape.X, model.BlendShape.Y, model.BlendShape.Z),
                            GroupSizes = new()
                        };

                        var j = 0;
                        var groupCount = 0;
                        foreach (Int32 idx in meshlet.Strip.Select(v => (Int32)v))
                        {
                            // Reset index encountered. Reset the strip
                            if (idx == 0xFFFF)
                            {
                                j = 0;
                                continue;
                            }

                            submodel.Vertexes.Add(new Vector4(meshlet.Vertexes[idx].Position));
                            submodel.UVW.Add(new Vector4(meshlet.Vertexes[idx].UV));
                            submodel.Colors.Add(meshlet.Vertexes[idx].Color);
                            var modelJointInfo = meshlet.Vertexes[idx].JointInfo;
                            var jointInfo = new VertexJointInfo()
                            {
                                JointIndex1 = modelJointInfo.JointIndex1,
                                JointIndex2 = modelJointInfo.JointIndex2,
                                JointIndex3 = modelJointInfo.JointIndex3,
                                Weight1 = modelJointInfo.Weight1,
                                Weight2 = modelJointInfo.Weight2,
                                Weight3 = modelJointInfo.Weight3,
                                Connection = j > 1
                            };
                            submodel.SkinJoints.Add(jointInfo);

                            groupCount++;
                            j++;
                        }

                        var faces = new List<PS2BlendSkinFace>();
                        foreach (var blendFace in meshlet.BlendFaces!)
                        {
                            var ps2BlendFace = new PS2BlendSkinFace(submodel.BlendShape)
                            {
                                VertexesAmount = (UInt32)submodel.Vertexes.Count,
                                Vertices = new()
                            };

                            foreach (Int32 idx in meshlet.Strip.Select(v => (Int32)v))
                            {
                                if (idx == 0xFFFF)
                                {
                                    continue;
                                }

                                var blendShape = blendFace.BlendShapes[idx];
                                ps2BlendFace.Vertices.Add(new VertexBlendShape
                                {
                                    BlendShape = submodel.BlendShape,
                                    Offset = blendShape.Offset
                                });
                            }

                            submodel.Faces.Add(ps2BlendFace);
                        }

                        submodel.GroupSizes.Add(groupCount);
                        subBlend.Models.Add(submodel);
                    }
                }
                blendSkin.SubBlends.Add(subBlend);
            }

            return blendSkin;
        }

        public ITwinCamera GenerateCamera(Stream stream)
        {
            var camera = new PS2AnyCamera();
            using var reader = new BinaryReader(stream);
            camera.Read(reader, (Int32)stream.Length);
            return camera;
        }

        public ITwinCollision GenerateCollision(Stream stream)
        {
            var collision = new PS2AnyCollisionData();
            using var reader = new BinaryReader(stream);
            collision.Read(reader, (Int32)stream.Length);
            return collision;
        }

        public ITwinDynamicScenery GenerateDynamicScenery(Stream stream)
        {
            var dynamicScenery = new PS2AnyDynamicScenery();
            using var reader = new BinaryReader(stream);
            dynamicScenery.Read(reader, (Int32)stream.Length);
            return dynamicScenery;
        }

        public ITwinInstance GenerateInstance(Stream stream)
        {
            var instance = new PS2AnyInstance();
            using var reader = new BinaryReader(stream);
            instance.Read(reader, (Int32)stream.Length);
            return instance;
        }

        public ITwinLink GenerateLink(Stream stream)
        {
            var link = new PS2AnyLink();
            using var reader = new BinaryReader(stream);
            var linkAmount = reader.ReadInt32();
            for (Int32 i = 0; i < linkAmount; i++)
            {
                TwinChunkLink chunkLink = new();
                chunkLink.UnkFlag = reader.ReadBoolean();
                chunkLink.Path = reader.ReadString();
                chunkLink.IsRendered = reader.ReadBoolean();
                chunkLink.UnkNum = reader.ReadByte();
                chunkLink.IsLoadWallActive = reader.ReadBoolean();
                chunkLink.KeepLoaded = reader.ReadBoolean();
                chunkLink.ObjectMatrix.Read(reader, Constants.SIZE_MATRIX4);
                chunkLink.ChunkMatrix.Read(reader, Constants.SIZE_MATRIX4);
                if (reader.ReadBoolean())
                {
                    chunkLink.LoadingWall = new Matrix4();
                    chunkLink.LoadingWall.Read(reader, Constants.SIZE_MATRIX4);
                }
                var buildersAmount = reader.ReadInt32();
                for (Int32 j = 0; j < buildersAmount; j++)
                {
                    var builder = new TwinChunkLinkBoundingBoxBuilder();
                    builder.Read(reader, (Int32)stream.Length);
                    chunkLink.ChunkLinksCollisionData.Add(builder);
                }
                link.LinksList.Add(chunkLink);
            }
            return link;
        }

        public ITwinLOD GenerateLOD(Stream stream)
        {
            var lod = new PS2AnyLOD();
            using var reader = new BinaryReader(stream);
            lod.Read(reader, (Int32)stream.Length);
            return lod;
        }

        public ITwinMaterial GenerateMaterial(Stream stream)
        {
            var material = new PS2AnyMaterial();
            using var reader = new BinaryReader(stream);
            material.Read(reader, (Int32)stream.Length);
            return material;
        }

        public ITwinMesh GenerateMesh(Stream stream)
        {
            var mesh = new PS2AnyMesh();
            using var reader = new BinaryReader(stream);
            mesh.Read(reader, (Int32)stream.Length);
            return mesh;
        }

        public ITwinModel GenerateModel(List<MeshProcessor.Mesh> meshes)
        {
            var model = new PS2AnyModel();
            foreach (var mesh in meshes)
            {
                var submodel = new PS2SubModel
                {
                    UnusedBlob = Array.Empty<Byte>(),
                    Vertexes = new(),
                    UVW = new(),
                    Colors = new(),
                    EmitColor = new(),
                    Normals = new(),
                    Connection = new(),
                    GroupSizes = new()
                };

                var groups = new List<List<Int32>>();
                foreach (var strip in mesh.Meshlets)
                {
                    groups.Add(new());
                    foreach (var node in strip.Strip)
                    {
                        groups[^1].Add((Int32)node);
                    }
                }

                var groupIndex = 0;
                foreach (var group in groups)
                {
                    var j = 0;
                    var groupCount = 0;
                    var rawModel = mesh.Meshlets[groupIndex];
                    foreach (var idx in group)
                    {
                        // Reset index encountered. Reset the strip
                        if (idx == 0xFFFF)
                        {
                            j = 0;
                            continue;
                        }

                        submodel.Vertexes.Add(new Vector4(rawModel.Vertexes[idx].Position));
                        submodel.UVW.Add(new Vector4(rawModel.Vertexes[idx].UV));
                        submodel.Colors.Add(rawModel.Vertexes[idx].Color);
                        if (rawModel.Vertexes[idx].HasEmitColor)
                        {
                            submodel.EmitColor.Add(rawModel.Vertexes[idx].EmitColor);
                        }
                        if (rawModel.Vertexes[idx].HasNormals)
                        {
                            submodel.Normals.Add(rawModel.Vertexes[idx].Normal);
                        }
                        submodel.Connection.Add(j > 1);
                        j++;
                        groupCount++;
                    }

                    submodel.GroupSizes.Add(groupCount);
                    groupIndex++;
                }

                model.SubModels.Add(submodel);
            }

            return model;
        }

        public ITwinObject GenerateObject(Stream stream)
        {
            var gameObject = new PS2AnyObject();
            using var reader = new BinaryReader(stream);
            gameObject.Type = (ITwinObject.ObjectType)reader.ReadInt32();
            gameObject.UnkTypeValue = reader.ReadByte();
            gameObject.ReactJointAmount = reader.ReadByte();
            gameObject.ExitPointAmount = reader.ReadByte();
            gameObject.Name = reader.ReadString();

            var triggerBehaviours = reader.ReadInt32();
            for (Int32 i = 0; i < triggerBehaviours; i++)
            {
                var triggerBehaviour = new TwinObjectTriggerBehaviour();
                triggerBehaviour.TriggerBehaviour = reader.ReadUInt16();
                triggerBehaviour.MessageID = reader.ReadUInt16();
                triggerBehaviour.BehaviourCallerIndex = reader.ReadByte();
                gameObject.TriggerBehaviours.Add(triggerBehaviour);
            }

            void fillList<T>(IList<T> list, Func<T> readerFunc)
            {
                var amount = reader.ReadInt32();
                for (Int32 i = 0; i < amount; i++)
                {
                    list.Add(readerFunc());
                }
            }
            fillList(gameObject.OGISlots, reader.ReadUInt16);
            fillList(gameObject.AnimationSlots, reader.ReadUInt16);
            fillList(gameObject.BehaviourSlots, reader.ReadUInt16);
            fillList(gameObject.ObjectSlots, reader.ReadUInt16);
            fillList(gameObject.SoundSlots, reader.ReadUInt16);

            gameObject.InstanceStateFlags = (Enums.InstanceState)reader.ReadUInt32();

            fillList(gameObject.InstFlags, reader.ReadUInt32);
            fillList(gameObject.InstFloats, reader.ReadSingle);
            fillList(gameObject.InstIntegers, reader.ReadUInt32);

            fillList(gameObject.RefObjects, reader.ReadUInt16);
            fillList(gameObject.RefOGIs, reader.ReadUInt16);
            fillList(gameObject.RefAnimations, reader.ReadUInt16);
            fillList(gameObject.RefCodeModels, reader.ReadUInt16);
            fillList(gameObject.RefBehaviours, reader.ReadUInt16);
            fillList(gameObject.RefUnknowns, reader.ReadUInt16);
            fillList(gameObject.RefSounds, reader.ReadUInt16);

            gameObject.BehaviourPack = new PS2BehaviourCommandPack();
            gameObject.BehaviourPack.Read(reader, (Int32)stream.Length);

            return gameObject;
        }

        public ITwinOGI GenerateOGI(Stream stream)
        {
            var ogi = new PS2AnyOGI();
            using var reader = new BinaryReader(stream);
            ogi.BoundingBox[0].Read(reader, Constants.SIZE_VECTOR4);
            ogi.BoundingBox[1].Read(reader, Constants.SIZE_VECTOR4);

            var jointIndices = reader.ReadInt32();
            for (Int32 i = 0; i < jointIndices; i++)
            {
                ogi.JointIndices.Add(reader.ReadByte());
            }

            var joints = reader.ReadInt32();
            for (Int32 i = 0; i < joints; i++)
            {
                var joint = new TwinJoint();
                joint.Read(reader, Constants.SIZE_JOINT);
                ogi.Joints.Add(joint);
            }

            var rigidModels = reader.ReadInt32();
            for (Int32 i = 0; i < rigidModels; i++)
            {
                ogi.RigidModelIds.Add(reader.ReadUInt32());
            }

            var exitPoints = reader.ReadInt32();
            for (Int32 i = 0; i < exitPoints; i++)
            {
                var exitPoint = new TwinExitPoint();
                exitPoint.Read(reader, Constants.SIZE_EXIT_POINT);
                ogi.ExitPoints.Add(exitPoint);
            }

            var matrices = reader.ReadInt32();
            for (Int32 i = 0; i < matrices; i++)
            {
                var mat = new Matrix4();
                mat.Read(reader, Constants.SIZE_MATRIX4);
                ogi.SkinInverseBindMatrices.Add(mat);
            }

            var builders = reader.ReadInt32();
            for (Int32 i = 0; i < builders; i++)
            {
                var builder = new TwinBoundingBoxBuilder();
                builder.Read(reader, (Int32)stream.Length);
                ogi.Collisions.Add(builder);
            }

            var jointToBuilders = reader.ReadInt32();
            for (Int32 i = 0; i < jointToBuilders; i++)
            {
                ogi.CollisionJointIndices.Add(reader.ReadByte());
            }

            ogi.SkinID = reader.ReadUInt32();
            ogi.BlendSkinID = reader.ReadUInt32();

            return ogi;
        }

        public ITwinParticle GenerateParticle(Stream stream)
        {
            var particles = new PS2AnyParticleData();
            using var reader = new BinaryReader(stream);
            particles.Read(reader, (Int32)stream.Length);
            return particles;
        }

        public ITwinDefaultParticle GenerateDefaultParticle(Stream stream)
        {
            var particles = new PS2DefaultParticleData();
            using var reader = new BinaryReader(stream);
            particles.Read(reader, (Int32)stream.Length);
            return particles;
        }

        public ITwinPath GeneratePath(Stream stream)
        {
            var path = new PS2AnyPath();
            using var reader = new BinaryReader(stream);
            path.Read(reader, (Int32)stream.Length);
            return path;
        }

        public ITwinPosition GeneratePosition(Stream stream)
        {
            var position = new PS2AnyPosition();
            using var reader = new BinaryReader(stream);
            position.Read(reader, (Int32)stream.Length);
            return position;
        }

        public ITwinRigidModel GenerateRigidModel(Stream stream)
        {
            var rigidModel = new PS2AnyRigidModel();
            using var reader = new BinaryReader(stream);
            rigidModel.Read(reader, (Int32)stream.Length);
            return rigidModel;
        }

        public ITwinScenery GenerateScenery(Stream stream)
        {
            var scenery = new PS2AnyScenery();
            using var reader = new BinaryReader(stream);
            scenery.Name = reader.ReadString();
            scenery.FogColor = reader.ReadUInt32();
            scenery.UnkByte = reader.ReadByte();
            scenery.SkydomeID = reader.ReadUInt32();
            scenery.HasLighting = reader.ReadBoolean();
            if (scenery.HasLighting)
            {
                var ambientLights = reader.ReadInt32();
                for (var i = 0; i < ambientLights; ++i)
                {
                    var ambient = new AmbientLight();
                    ambient.Read(reader, ambient.GetLength());
                    scenery.AmbientLights.Add(ambient);
                }

                var dirLights = reader.ReadInt32();
                for (var i = 0; i < dirLights; ++i)
                {
                    var directional = new DirectionalLight();
                    directional.Read(reader, directional.GetLength());
                    scenery.DirectionalLights.Add(directional);
                }

                var pointLights = reader.ReadInt32();
                for (var i = 0; i < pointLights; ++i)
                {
                    var point = new PointLight();
                    point.Read(reader, point.GetLength());
                    scenery.PointLights.Add(point);
                }

                var negativeLights = reader.ReadInt32();
                for (var i = 0; i < negativeLights; ++i)
                {
                    var negative = new NegativeLight();
                    negative.Read(reader, negative.GetLength());
                    scenery.NegativeLights.Add(negative);
                }
            }

            var sceneries = reader.ReadInt32();
            for (Int32 i = 0; i < sceneries; i++)
            {
                var type = (ITwinScenery.SceneryType)reader.ReadInt32();
                TwinSceneryBaseType sceneryNode = new TwinSceneryLeaf();
                switch (type)
                {
                    case ITwinScenery.SceneryType.Root:
                        sceneryNode = new TwinSceneryRoot();
                        break;
                    case ITwinScenery.SceneryType.Node:
                        sceneryNode = new TwinSceneryNode();
                        break;
                }
                sceneryNode.Read(reader, 0);
                scenery.Sceneries.Add(sceneryNode);
            }

            return scenery;
        }

        public ITwinSkin GenerateSkin(List<SubSkinData> subskins)
        {
            PS2AnySkin skin = new();

            foreach (var subskin in subskins)
            {
                var submodel = new PS2SubSkin
                {
                    Vertexes = new(),
                    UVW = new(),
                    Colors = new(),
                    SkinJoints = new(),
                    Material = AssetManager.Get().GetAsset(subskin.Material).ID,
                    GroupSizes = new()
                };

                var mesh = subskin.Mesh;
                var groups = new List<List<Int32>>();
                foreach (var strip in mesh.Meshlets)
                {
                    groups.Add(new());
                    foreach (var node in strip.Strip)
                    {
                        groups[^1].Add((Int32)node);
                    }
                }

                var groupIndex = 0;
                foreach (var group in groups)
                {
                    var j = 0;
                    var groupCount = 0;
                    var rawModel = mesh.Meshlets[groupIndex];
                    foreach (var idx in group)
                    {
                        // Reset index encountered. Reset the strip
                        if (idx == 0xFFFF)
                        {
                            j = 0;
                            continue;
                        }

                        submodel.Vertexes.Add(new Vector4(rawModel.Vertexes[idx].Position));
                        submodel.UVW.Add(new Vector4(rawModel.Vertexes[idx].UV));
                        submodel.Colors.Add(rawModel.Vertexes[idx].Color);
                        var modelJointInfo = rawModel.Vertexes[idx].JointInfo;
                        var jointInfo = new VertexJointInfo()
                        {
                            JointIndex1 = modelJointInfo.JointIndex1,
                            JointIndex2 = modelJointInfo.JointIndex2,
                            JointIndex3 = modelJointInfo.JointIndex3,
                            Weight1 = modelJointInfo.Weight1,
                            Weight2 = modelJointInfo.Weight2,
                            Weight3 = modelJointInfo.Weight3,
                            Connection = j > 1
                        };
                        submodel.SkinJoints.Add(jointInfo);
                        groupCount++;
                        j++;
                    }

                    submodel.GroupSizes.Add(groupCount);
                    groupIndex++;
                }

                skin.SubSkins.Add(submodel);
            }
            return skin;
        }

        public ITwinSkydome GenerateSkydome(Stream stream)
        {
            var skydome = new PS2AnySkydome();
            using var reader = new BinaryReader(stream);
            skydome.Read(reader, (Int32)stream.Length);
            return skydome;
        }

        public ITwinSound GenerateSound()
        {
            return new PS2AnySound();
        }

        public ITwinSurface GenerateSurface(Stream stream)
        {
            var surface = new PS2AnyCollisionSurface();
            using var reader = new BinaryReader(stream);
            surface.Read(reader, (Int32)stream.Length);
            return surface;
        }

        public ITwinTemplate GenerateTemplate(Stream stream)
        {
            var template = new PS2AnyTemplate();
            using var reader = new BinaryReader(stream);
            template.Read(reader, (Int32)stream.Length);
            return template;
        }

        public ITwinTexture GenerateTexture()
        {
            return new PS2AnyTexture();
        }

        public ITwinTrigger GenerateTrigger(Stream stream)
        {
            var trigger = new PS2AnyTrigger();
            using var reader = new BinaryReader(stream);
            trigger.Read(reader, (Int32)stream.Length);
            return trigger;
        }

        public ITwinSection GenerateFrontend(List<ITwinSound> sounds)
        {
            var frontend = new PS2Frontend();

            foreach (var sound in sounds)
            {
                frontend.AddItem(sound);
            }

            return frontend;
        }

        public ITwinPSF GenerateFont(List<ITwinPTC> pages, List<Vector4> unkVecs, Int32 unkInt)
        {
            var font = new PS2PSF();

            foreach (var page in pages)
            {
                font.FontPages.Add(page);
            }

            font.UnkVecs = CloneUtils.CloneList(unkVecs);
            font.UnkInt = unkInt;

            return font;
        }

        public ITwinPTC GeneratePTC(UInt32 texID, UInt32 matID, ITwinTexture texture, ITwinMaterial material)
        {
            var ptc = new PS2PTC
            {
                TexID = texID,
                MatID = matID,
                Texture = texture,
                Material = material
            };
            return ptc;
        }

        public ITwinPSM GeneratePSM(List<ITwinPTC> ptcs)
        {
            var psm = new PS2PSM();
            foreach (var ptc in ptcs)
            {
                psm.PTCs.Add(ptc);
            }

            return psm;
        }

        public ITwinSection GenerateDefault()
        {
            var @default = new PS2Default();
            @default.SetRoot(@default);
            @default.SetParent(@default);

            var graphics = new PS2AnyGraphicsSection();
            graphics.SetID(Constants.LEVEL_GRAPHICS_SECTION);
            FillGraphicsSection(graphics, @default);

            var code = new PS2AnyCodeSection();
            FillCodeSection(code, @default);

            var layout1 = new PS2AnyLayoutSection();
            layout1.SetID(Constants.LEVEL_LAYOUT_1_SECTION);
            FillLayoutSection(layout1, @default);

            var layout2 = new PS2AnyLayoutSection();
            layout2.SetID(Constants.LEVEL_LAYOUT_2_SECTION);
            layout2.SetRoot(@default);
            layout2.SetParent(@default);

            var layout3 = new PS2AnyLayoutSection();
            layout3.SetID(Constants.LEVEL_LAYOUT_3_SECTION);
            layout3.SetRoot(@default);
            layout3.SetParent(@default);

            var layout4 = new PS2AnyLayoutSection();
            layout4.SetID(Constants.LEVEL_LAYOUT_4_SECTION);
            layout4.SetRoot(@default);
            layout4.SetParent(@default);

            var layout5 = new PS2AnyLayoutSection();
            layout5.SetID(Constants.LEVEL_LAYOUT_5_SECTION);
            layout5.SetRoot(@default);
            layout5.SetParent(@default);

            var layout6 = new PS2AnyLayoutSection();
            layout6.SetID(Constants.LEVEL_LAYOUT_6_SECTION);
            layout6.SetRoot(@default);
            layout6.SetParent(@default);

            var layout7 = new PS2AnyLayoutSection();
            layout7.SetID(Constants.LEVEL_LAYOUT_7_SECTION);
            layout7.SetRoot(@default);
            layout7.SetParent(@default);

            var layout8 = new PS2AnyLayoutSection();
            layout8.SetID(Constants.LEVEL_LAYOUT_8_SECTION);
            FillLayoutSection(layout8, @default);

            var collision = new BaseTwinItem();
            collision.SetID(Constants.LEVEL_COLLISION_ITEM);
            collision.SetRoot(@default);
            collision.SetParent(@default);

            // Particles are generated and injected later

            @default.AddItem(graphics);
            @default.AddItem(code);
            @default.AddItem(collision);
            @default.AddItem(layout1);
            @default.AddItem(layout2);
            @default.AddItem(layout3);
            @default.AddItem(layout4);
            @default.AddItem(layout5);
            @default.AddItem(layout6);
            @default.AddItem(layout7);
            @default.AddItem(layout8);
            return @default;
        }

        public ITwinSection GenerateRM()
        {
            var rm2 = new PS2AnyTwinsanityRM2();
            var graphics = new PS2AnyGraphicsSection();
            graphics.SetID(Constants.LEVEL_GRAPHICS_SECTION);
            FillGraphicsSection(graphics, rm2);

            var code = new PS2AnyCodeSection();
            FillCodeSection(code, rm2);

            rm2.AddItem(graphics);
            rm2.AddItem(code);

            for (UInt32 i = 0; i < 7; ++i)
            {
                var layout = new PS2AnyLayoutSection();
                layout.SetID(Constants.LEVEL_LAYOUT_1_SECTION + i);
                FillLayoutSection(layout, rm2);
                rm2.AddItem(layout);
            }

            var layout8 = new BaseTwinItem();
            layout8.SetID(Constants.LEVEL_LAYOUT_8_SECTION);
            layout8.SetRoot(rm2);
            layout8.SetParent(rm2);
            rm2.AddItem(layout8);

            // Collision and particles are generated and injected later

            return rm2;
        }

        public ITwinSection GenerateSM()
        {
            var sm2 = new PS2AnyTwinsanitySM2();
            var graphics = new PS2AnyGraphicsSection();
            graphics.SetID(Constants.SCENERY_GRAPHICS_SECTION);
            FillGraphicsSection(graphics, sm2);

            var unk1 = new BaseTwinItem();
            unk1.SetID(Constants.SCENERY_UNK_1_ITEM);
            unk1.SetRoot(sm2);
            unk1.SetParent(sm2);
            var unk2 = new BaseTwinItem();
            unk2.SetID(Constants.SCENERY_UNK_2_ITEM);
            unk2.SetRoot(sm2);
            unk2.SetParent(sm2);
            var unk3 = new BaseTwinItem();
            unk3.SetID(Constants.SCENERY_UNK_3_ITEM);
            unk3.SetRoot(sm2);
            unk3.SetParent(sm2);

            // Scenery, dynamic scenery and chunk links are generated and injected later

            sm2.AddItem(graphics);
            sm2.AddItem(unk1);
            sm2.AddItem(unk2);
            sm2.AddItem(unk3);
            return sm2;
        }

        private static void FillLayoutSection(PS2AnyLayoutSection layout, ITwinSection root)
        {
            layout.SetRoot(root);
            layout.SetParent(root);
            {
                var templates = new PS2AnyTemplatesSection();
                templates.SetID(Constants.LAYOUT_TEMPLATES_SECTION);
                templates.SetRoot(root);
                templates.SetParent(layout);
                var aiPositions = new PS2AnyAIPositionsSection();
                aiPositions.SetID(Constants.LAYOUT_AI_POSITIONS_SECTION);
                aiPositions.SetRoot(root);
                aiPositions.SetParent(layout);
                var aiPaths = new PS2AnyAIPathsSection();
                aiPaths.SetID(Constants.LAYOUT_AI_PATHS_SECTION);
                aiPaths.SetRoot(root);
                aiPaths.SetParent(layout);
                var positions = new PS2AnyPositionsSection();
                positions.SetID(Constants.LAYOUT_POSITIONS_SECTION);
                positions.SetRoot(root);
                positions.SetParent(layout);
                var paths = new PS2AnyPathsSection();
                paths.SetID(Constants.LAYOUT_PATHS_SECTION);
                paths.SetRoot(root);
                paths.SetParent(layout);
                var surfaces = new PS2AnySurfacesSection();
                surfaces.SetID(Constants.LAYOUT_SURFACES_SECTION);
                surfaces.SetRoot(root);
                surfaces.SetParent(layout);
                var instances = new PS2AnyInstancesSection();
                instances.SetID(Constants.LAYOUT_INSTANCES_SECTION);
                instances.SetRoot(root);
                instances.SetParent(layout);
                var triggers = new PS2AnyTriggersSection();
                triggers.SetID(Constants.LAYOUT_TRIGGERS_SECTION);
                triggers.SetRoot(root);
                triggers.SetParent(layout);
                var cameras = new PS2AnyCamerasSection();
                cameras.SetID(Constants.LAYOUT_CAMERAS_SECTION);
                cameras.SetRoot(root);
                cameras.SetParent(layout);

                layout.AddItem(templates);
                layout.AddItem(aiPositions);
                layout.AddItem(aiPaths);
                layout.AddItem(positions);
                layout.AddItem(paths);
                layout.AddItem(surfaces);
                layout.AddItem(instances);
                layout.AddItem(triggers);
                layout.AddItem(cameras);
            }
        }

        private static void FillGraphicsSection(PS2AnyGraphicsSection graphics, ITwinSection root)
        {
            graphics.SetRoot(root);
            graphics.SetParent(root);
            {
                var textures = new PS2AnyTexturesSection();
                textures.SetID(Constants.GRAPHICS_TEXTURES_SECTION);
                textures.SetRoot(root);
                textures.SetParent(graphics);
                var materials = new PS2AnyMaterialsSection();
                materials.SetID(Constants.GRAPHICS_MATERIALS_SECTION);
                materials.SetRoot(root);
                materials.SetParent(graphics);
                var models = new PS2AnyModelsSection();
                models.SetID(Constants.GRAPHICS_MODELS_SECTION);
                models.SetRoot(root);
                models.SetParent(graphics);
                var rigids = new PS2AnyRigidModelsSection();
                rigids.SetID(Constants.GRAPHICS_RIGID_MODELS_SECTION);
                rigids.SetRoot(root);
                rigids.SetParent(graphics);
                var skins = new PS2AnySkinsSection();
                skins.SetID(Constants.GRAPHICS_SKINS_SECTION);
                skins.SetRoot(root);
                skins.SetParent(graphics);
                var blendSkins = new PS2AnyBlendSkinsSection();
                blendSkins.SetID(Constants.GRAPHICS_BLEND_SKINS_SECTION);
                blendSkins.SetRoot(root);
                blendSkins.SetParent(graphics);
                var meshes = new PS2AnyMeshesSection();
                meshes.SetID(Constants.GRAPHICS_MESHES_SECTION);
                meshes.SetRoot(root);
                meshes.SetParent(graphics);
                var lods = new PS2AnyLODsSection();
                lods.SetID(Constants.GRAPHICS_LODS_SECTION);
                lods.SetRoot(root);
                lods.SetParent(graphics);
                var skydomes = new PS2AnySkydomesSection();
                skydomes.SetID(Constants.GRAPHICS_SKYDOMES_SECTION);
                skydomes.SetRoot(root);
                skydomes.SetParent(graphics);

                graphics.AddItem(textures);
                graphics.AddItem(materials);
                graphics.AddItem(models);
                graphics.AddItem(rigids);
                graphics.AddItem(skins);
                graphics.AddItem(blendSkins);
                graphics.AddItem(meshes);
                graphics.AddItem(lods);
                graphics.AddItem(skydomes);
            }
        }

        private static void FillCodeSection(PS2AnyCodeSection code, ITwinSection root)
        {
            code.SetID(Constants.LEVEL_CODE_SECTION);
            code.SetRoot(root);
            code.SetParent(root);
            {
                var objects = new PS2AnyGameObjectsSection();
                objects.SetID(Constants.CODE_GAME_OBJECTS_SECTION);
                objects.SetRoot(root);
                objects.SetParent(code);
                var behaviours = new PS2AnyBehavioursSection();
                behaviours.SetID(Constants.CODE_BEHAVIOURS_SECTION);
                behaviours.SetRoot(root);
                behaviours.SetParent(code);
                var animations = new PS2AnyAnimationsSection();
                animations.SetID(Constants.CODE_ANIMATIONS_SECTION);
                animations.SetRoot(root);
                animations.SetParent(code);
                var ogis = new PS2AnyOGIsSection();
                ogis.SetID(Constants.CODE_OGIS_SECTION);
                ogis.SetRoot(root);
                ogis.SetParent(code);
                var behaviourSequences = new PS2AnyBehaviourCommandsSequencesSection();
                behaviourSequences.SetID(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
                behaviourSequences.SetRoot(root);
                behaviourSequences.SetParent(code);
                var unknowns = new BaseTwinItem();
                unknowns.SetID(Constants.CODE_UNK_ITEM);
                unknowns.SetRoot(root);
                unknowns.SetParent(code);
                var sfxs = new PS2AnySoundsSection();
                sfxs.SetID(Constants.CODE_SOUND_EFFECTS_SECTION);
                sfxs.SetRoot(root);
                sfxs.SetParent(code);
                var sfxsEn = new PS2AnySoundsSection();
                sfxsEn.SetID(Constants.CODE_LANG_ENG_SECTION);
                sfxsEn.SetRoot(root);
                sfxsEn.SetParent(code);
                var sfxsFr = new PS2AnySoundsSection();
                sfxsFr.SetID(Constants.CODE_LANG_FRE_SECTION);
                sfxsFr.SetRoot(root);
                sfxsFr.SetParent(code);
                var sfxsGr = new PS2AnySoundsSection();
                sfxsGr.SetID(Constants.CODE_LANG_GER_SECTION);
                sfxsGr.SetRoot(root);
                sfxsGr.SetParent(code);
                var sfxsSp = new PS2AnySoundsSection();
                sfxsSp.SetID(Constants.CODE_LANG_SPA_SECTION);
                sfxsSp.SetRoot(root);
                sfxsSp.SetParent(code);
                var sfxsIt = new PS2AnySoundsSection();
                sfxsIt.SetID(Constants.CODE_LANG_ITA_SECTION);
                sfxsIt.SetRoot(root);
                sfxsIt.SetParent(code);
                var sfxsJp = new PS2AnySoundsSection();
                sfxsJp.SetID(Constants.CODE_LANG_JPN_SECTION);
                sfxsJp.SetRoot(root);
                sfxsJp.SetParent(code);

                code.AddItem(objects);
                code.AddItem(behaviours);
                code.AddItem(animations);
                code.AddItem(ogis);
                code.AddItem(behaviourSequences);
                code.AddItem(unknowns);
                code.AddItem(sfxs);
                code.AddItem(sfxsEn);
                code.AddItem(sfxsFr);
                code.AddItem(sfxsGr);
                code.AddItem(sfxsSp);
                code.AddItem(sfxsIt);
                code.AddItem(sfxsJp);
            }
        }
    }
}
