using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets.Code.Resolvers.Compiler;
using TT_Lab.Util;
using Twinsanity.PS2Hardware;
using Twinsanity.AgentLab;
using Twinsanity.AgentLab.AgentLabObjectDescs;
using Twinsanity.AgentLab.AgentLabObjectDescs.PS2;
using Twinsanity.AgentLab.Resolvers.Compiler;
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
        // A packet's DMA tag and the padding after its last batch, a word per missing byte at most
        private const Int32 PacketStartBytes = 80;

        public Package GlobalPackage { get; set; }
        public bool IsDefaultResolution { get; set; }

        /// <summary>
        /// Behaviour commands the platform's scripts are compiled against
        /// </summary>
        protected virtual String ActionDefinitionsFile => "ActionDefinitionsPs2.lab";

        // The platform's class of an item read from a stream, the Xbox version's items mostly extend the PS2 ones
        protected virtual T Create<T>() where T : ITwinItem, new()
        {
            return new T();
        }

        protected virtual BaseTwinSection CreateSection<T>() where T : BaseTwinSection, new()
        {
            return new T();
        }

        protected virtual ITwinBehaviourCommandPack CreateCommandPack() => new PS2BehaviourCommandPack();
        protected virtual CommandDesc NewCommandDesc() => new PS2CommandDesc();
        protected virtual CommandPackDesc NewCommandPackDesc() => new PS2CommandPackDesc();
        protected virtual CommandsSequenceDesc NewCommandsSequenceDesc() => new PS2CommandsSequenceDesc();
        protected virtual StateDesc NewStateDesc() => new PS2StateDesc();
        protected virtual StateBodyDesc NewStateBodyDesc() => new PS2StateBodyDesc();
        protected virtual GraphDesc NewGraphDesc() => new PS2GraphDesc();
        
        public ITwinAIPath GenerateAIPath(Stream stream)
        {
            var aiPath = Create<PS2AnyAIPath>();
            using var reader = new BinaryReader(stream);
            aiPath.Read(reader, (Int32)stream.Length);
            return aiPath;
        }

        public ITwinAIPosition GenerateAIPosition(Stream stream)
        {
            var aiPosition = Create<PS2AnyAIPosition>();
            using var reader = new BinaryReader(stream);
            aiPosition.Read(reader, (Int32)stream.Length);
            return aiPosition;
        }

        public ITwinAnimation GenerateAnimation(Stream stream)
        {
            var animation = Create<PS2AnyAnimation>();
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
            using var reader = new StreamReader(stream);
            var script = reader.ReadToEnd();
            var compilerOptions = new AgentLabCompiler.CompilerOptions
            {
                CommandsSequence = NewCommandsSequenceDesc(),
                CommandPack = NewCommandPackDesc(),
                Command = NewCommandDesc(),
                ActionDefinitionsFile = ActionDefinitionsFile,
                Resolver = new LabCompilerResolver(new DefaultGlobalObjectIdResolver(), new DefaultGraphResolver())
            };
            var compilerResult = AgentLabCompiler.Compile(script, compilerOptions);
            return compilerResult.Get<ITwinBehaviourCommandsSequence>();
        }

        public AgentLabCompiler.CompilerResult GenerateBehaviourGraph(Stream stream, IAsset? requester = null)
        {
            using var binaryReader = new BinaryReader(stream);
            var graphId = binaryReader.ReadInt32();
            using var reader = new StreamReader(stream);
            var script = reader.ReadToEnd();
            var compilerOptions = new AgentLabCompiler.CompilerOptions
            {
                Command = NewCommandDesc(),
                CommandPack = NewCommandPackDesc(),
                State = NewStateDesc(),
                StateBody = NewStateBodyDesc(),
                Graph = NewGraphDesc(),
                ActionDefinitionsFile = ActionDefinitionsFile,
                Resolver = new LabCompilerResolver(graphId, requester)
            };
            return AgentLabCompiler.Compile(script, compilerOptions);
        }

        public ITwinBehaviourCommandPack GenerateBehaviourCommandPack(Stream stream)
        {
            using var reader = new StreamReader(stream);
            var script = reader.ReadToEnd();
            var compilerOptions = new AgentLabCompiler.CompilerOptions
            {
                Command = NewCommandDesc(),
                CommandPack = NewCommandPackDesc(),
                ActionDefinitionsFile = ActionDefinitionsFile
            };
            return AgentLabCompiler.CompileCommands(script, compilerOptions).Get<ITwinBehaviourCommandPack>();
        }

        public string ChunkPath { get; set; }
        public IReadOnlyDictionary<(Type, UInt32), LabURI>? ChunkVersions { get; set; }
        public ChunkOverrides? Overrides { get; set; }
        public ChunkResolution Resolution { get; } = new();
        public ConcurrentDictionary<(LabURI Graph, Int32 Id, String Script), AgentLabCompiler.CompilerResult> CompiledBehaviours { get; protected init; } = new();
        public IReadOnlySet<LabURI> ExcludedChunks { get; set; } = new HashSet<LabURI>();
        public List<LabURI> LinkedChunks { get; } = new();

        public virtual ITwinItemFactory ForChunk()
        {
            return new PS2ItemFactory { GlobalPackage = GlobalPackage, CompiledBehaviours = CompiledBehaviours, ExcludedChunks = ExcludedChunks };
        }

        public virtual ITwinBlendSkin GenerateBlendSkin(Int32 blendsAmount, List<BlendPartExport> parts)
        {
            var blendSkin = new PS2AnyBlendSkin
            {
                BlendsAmount = blendsAmount
            };
            // Parts whose packing doesn't fit anymore share new settings so vertexes shared by the game's models stay in the same place
            var sharedCompression = parts.Any(p => GetFittingCompression(p.Compression, p.Vertexes) == null)
                ? TwinSkinCompression.FitTo(parts.SelectMany(p => p.Vertexes).Select(v => v.Position))
                : null;
            foreach (var part in parts)
            {
                var subBlend = new PS2SubBlendSkin(blendsAmount)
                {
                    Material = part.Material
                };
                var compression = GetFittingCompression(part.Compression, part.Vertexes) ?? sharedCompression;
                foreach (var batch in part.Layout.Batches)
                {
                    var blendShape = GetFittingBlendShape(batch, part.ShapeOffsets, blendsAmount);
                    var model = new PS2BlendSkinModel(blendsAmount)
                    {
                        Vertexes = [],
                        UVW = [],
                        Colors = [],
                        SkinJoints = [],
                        GroupSizes = [batch.Vertexes.Count],
                        Faces = [],
                        BlendShape = blendShape,
                        Compression = compression?.Clone(),
                        Padding = part.Layout.Padding
                    };
                    AddSkinVertexes(batch, part.Vertexes, model.Vertexes, model.UVW, model.Colors, model.SkinJoints);
                    for (var shape = 0; shape < blendsAmount; shape++)
                    {
                        var offsets = shape < part.ShapeOffsets.Count ? part.ShapeOffsets[shape] : null;
                        model.Faces.Add(new PS2BlendSkinFace(blendShape)
                        {
                            Vertices = batch.Vertexes.Select(v => new VertexBlendShape
                            {
                                BlendShape = blendShape,
                                Offset = offsets != null ? new Vector4(offsets[v.Index]) : new Vector4()
                            }).ToList()
                        });
                    }

                    subBlend.Models.Add(model);
                }

                blendSkin.SubBlends.Add(subBlend);
            }

            return blendSkin;
        }

        // The stored scale while the batch's offsets still fit into signed bytes with it, the smallest scale fitting them otherwise
        private static Vector3 GetFittingBlendShape(MeshProcessor.StripBatch batch, List<List<Vector4>> shapeOffsets, Int32 blendsAmount)
        {
            var maximum = new Single[3];
            foreach (var offsets in shapeOffsets.Take(blendsAmount))
            {
                foreach (var vertex in batch.Vertexes)
                {
                    var offset = offsets[vertex.Index];
                    maximum[0] = Math.Max(maximum[0], Math.Abs(offset.X));
                    maximum[1] = Math.Max(maximum[1], Math.Abs(offset.Y));
                    maximum[2] = Math.Max(maximum[2], Math.Abs(offset.Z));
                }
            }

            var stored = batch.BlendShape;
            var result = new Single[3];
            for (var axis = 0; axis < 3; axis++)
            {
                var storedScale = stored == null ? 0 : axis switch { 0 => stored.X, 1 => stored.Y, _ => stored.Z };
                if (storedScale > 0 && maximum[axis] / storedScale <= SByte.MaxValue + 0.49f)
                {
                    result[axis] = storedScale;
                    continue;
                }

                result[axis] = maximum[axis] > 0 ? maximum[axis] / SByte.MaxValue : storedScale > 0 ? storedScale : 1.0f / 1024.0f;
            }

            return new Vector3(result[0], result[1], result[2]);
        }

        private static TwinSkinCompression? GetFittingCompression(TwinSkinCompression? compression, List<AssetData.Graphics.SubModels.Vertex> vertexes)
        {
            if (compression == null || !compression.Fits(vertexes.Select(v => v.Position), vertexes.Select(v => v.UV)))
            {
                return null;
            }

            return compression;
        }

        private static void AddSkinVertexes(MeshProcessor.StripBatch batch, List<AssetData.Graphics.SubModels.Vertex> vertexes, List<Vector4> positions, List<Vector4> uvs,
            List<Vector4> colors, List<VertexJointInfo> joints)
        {
            foreach (var stripVertex in batch.Vertexes)
            {
                var vertex = vertexes[stripVertex.Index];
                positions.Add(new Vector4(vertex.Position));
                uvs.Add(new Vector4(vertex.UV));
                colors.Add(new Vector4(vertex.Color));
                joints.Add(new VertexJointInfo
                {
                    JointIndex1 = vertex.JointInfo.JointIndex1,
                    JointIndex2 = vertex.JointInfo.JointIndex2,
                    JointIndex3 = vertex.JointInfo.JointIndex3,
                    Weight1 = vertex.JointInfo.Weight1,
                    Weight2 = vertex.JointInfo.Weight2,
                    Weight3 = vertex.JointInfo.Weight3,
                    WeightsAmount = vertex.JointInfo.WeightsAmount,
                    Connection = stripVertex.Draws
                });
            }
        }

        public ITwinCamera GenerateCamera(Stream stream)
        {
            var camera = Create<PS2AnyCamera>();
            using var reader = new BinaryReader(stream);
            camera.Read(reader, (Int32)stream.Length);
            return camera;
        }

        public ITwinCollision GenerateCollision(Stream stream)
        {
            var collision = Create<PS2AnyCollisionData>();
            using var reader = new BinaryReader(stream);
            collision.Read(reader, (Int32)stream.Length);
            return collision;
        }

        public ITwinDynamicScenery GenerateDynamicScenery(Stream stream)
        {
            var dynamicScenery = Create<PS2AnyDynamicScenery>();
            using var reader = new BinaryReader(stream);
            dynamicScenery.Read(reader, (Int32)stream.Length);
            return dynamicScenery;
        }

        public ITwinInstance GenerateInstance(Stream stream)
        {
            var instance = Create<PS2AnyInstance>();
            using var reader = new BinaryReader(stream);
            instance.Read(reader, (Int32)stream.Length);
            return instance;
        }

        public ITwinLink GenerateLink(Stream stream)
        {
            var link = Create<PS2AnyLink>();
            using var reader = new BinaryReader(stream);
            var linkAmount = reader.ReadInt32();
            for (var i = 0; i < linkAmount; i++)
            {
                TwinChunkLink chunkLink = new();
                chunkLink.LoadsWithoutPlayer = reader.ReadBoolean();
                chunkLink.Path = reader.ReadString();
                chunkLink.Visibility = (ChunkLinkVisibility)reader.ReadByte();
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
                for (var j = 0; j < buildersAmount; j++)
                {
                    var builder = new TwinChunkLinkHull();
                    builder.Read(reader, (Int32)stream.Length);
                    chunkLink.ChunkLinksCollisionData.Add(builder);
                }
                link.LinksList.Add(chunkLink);
            }
            return link;
        }

        public ITwinLOD GenerateLOD(Stream stream)
        {
            var lod = Create<PS2AnyLOD>();
            using var reader = new BinaryReader(stream);
            lod.Read(reader, (Int32)stream.Length);
            return lod;
        }

        public ITwinMaterial GenerateMaterial(Stream stream)
        {
            var material = Create<PS2AnyMaterial>();
            using var reader = new BinaryReader(stream);
            material.Read(reader, (Int32)stream.Length);
            return material;
        }

        public ITwinMesh GenerateMesh(Stream stream)
        {
            var mesh = Create<PS2AnyMesh>();
            using var reader = new BinaryReader(stream);
            mesh.Read(reader, (Int32)stream.Length);
            return mesh;
        }

        public virtual ITwinModel GenerateModel(List<RigidPartExport> parts)
        {
            var model = new PS2AnyModel();
            foreach (var part in parts)
            {
                var hasNormals = part.Vertexes.Any(v => v.HasNormals);
                var hasEmits = part.Vertexes.Any(v => v.HasEmitColor);
                PS2SubModel NewSubModel() => new()
                {
                    UnusedBlob = Array.Empty<Byte>(),
                    Vertexes = [],
                    UVW = [],
                    Colors = [],
                    EmitColor = [],
                    Normals = [],
                    Connection = [],
                    GroupSizes = [],
                    Padding = part.Layout.Padding
                };

                // Like a skin's sub skins, a sub model's packet can't go past what one DMA tag sends
                var subModel = NewSubModel();
                var packetBytes = PacketStartBytes;
                foreach (var batch in part.Layout.Batches)
                {
                    var batchBytes = TwinVIFCompiler.RigidBatchBytes(batch.Vertexes.Count, hasNormals, hasEmits);
                    if (subModel.GroupSizes.Count > 0 && packetBytes + batchBytes > TwinVIFCompiler.MaxPacketBytes)
                    {
                        model.SubModels.Add(subModel);
                        subModel = NewSubModel();
                        packetBytes = PacketStartBytes;
                    }

                    packetBytes += batchBytes;
                    subModel.GroupSizes.Add(batch.Vertexes.Count);
                    foreach (var stripVertex in batch.Vertexes)
                    {
                        var vertex = part.Vertexes[stripVertex.Index];
                        subModel.Vertexes.Add(new Vector4(vertex.Position));
                        subModel.UVW.Add(new Vector4(vertex.UV));
                        subModel.Colors.Add(new Vector4(vertex.Color) { StoresColorWithAlphaBlend = vertex.Color.StoresColorWithAlphaBlend });
                        if (hasNormals)
                        {
                            subModel.Normals.Add(vertex.HasNormals ? new Vector4(vertex.Normal) : vertex.GetUnitNormal());
                        }

                        if (hasEmits)
                        {
                            subModel.EmitColor.Add(vertex.HasEmitColor
                                ? new Vector4(vertex.EmitColor) { StoresColorWithAlphaBlend = vertex.EmitColor.StoresColorWithAlphaBlend }
                                : new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
                        }

                        subModel.Connection.Add(stripVertex.Draws);
                    }
                }

                model.SubModels.Add(subModel);
            }

            return model;
        }

        public ITwinObject GenerateObject(Stream stream)
        {
            var gameObject = Create<PS2AnyObject>();
            using var reader = new BinaryReader(stream);
            gameObject.Type = (ITwinObject.ObjectType)reader.ReadInt32();
            gameObject.SubType = reader.ReadByte();
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
                // Both versions of the game set every bit above the caller index
                triggerBehaviour.UpperBits = 0x7F;
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

            fillList(gameObject.TaggedProperties, reader.ReadUInt32);
            fillList(gameObject.FloatProperties, reader.ReadSingle);
            fillList(gameObject.IntProperties, reader.ReadInt32);

            fillList(gameObject.RefObjects, reader.ReadUInt16);
            fillList(gameObject.RefOGIs, reader.ReadUInt16);
            fillList(gameObject.RefAnimations, reader.ReadUInt16);
            fillList(gameObject.RefCodeModels, reader.ReadUInt16);
            fillList(gameObject.RefBehaviours, reader.ReadUInt16);
            fillList(gameObject.RefUnused, reader.ReadUInt16);
            fillList(gameObject.RefSounds, reader.ReadUInt16);

            gameObject.BehaviourPack = CreateCommandPack();
            gameObject.BehaviourPack.Read(reader, (Int32)stream.Length);

            return gameObject;
        }

        public ITwinOGI GenerateOGI(Stream stream)
        {
            var ogi = Create<PS2AnyOGI>();
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

            var hulls = reader.ReadInt32();
            for (Int32 i = 0; i < hulls; i++)
            {
                var hull = new TwinCollisionHull();
                hull.Read(reader, (Int32)stream.Length);
                ogi.CollisionHulls.Add(hull);
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
            var particles = Create<PS2AnyParticleData>();
            using var reader = new BinaryReader(stream);
            particles.Read(reader, (Int32)stream.Length);
            return particles;
        }

        public ITwinDefaultParticle GenerateDefaultParticle(Stream stream)
        {
            var particles = Create<PS2DefaultParticleData>();
            using var reader = new BinaryReader(stream);
            particles.Read(reader, (Int32)stream.Length);
            return particles;
        }

        public ITwinPath GeneratePath(Stream stream)
        {
            var path = Create<PS2AnyPath>();
            using var reader = new BinaryReader(stream);
            path.Read(reader, (Int32)stream.Length);
            return path;
        }

        public ITwinPosition GeneratePosition(Stream stream)
        {
            var position = Create<PS2AnyPosition>();
            using var reader = new BinaryReader(stream);
            position.Read(reader, (Int32)stream.Length);
            return position;
        }

        public ITwinRigidModel GenerateRigidModel(Stream stream)
        {
            var rigidModel = Create<PS2AnyRigidModel>();
            using var reader = new BinaryReader(stream);
            rigidModel.Read(reader, (Int32)stream.Length);
            return rigidModel;
        }

        public ITwinScenery GenerateScenery(Stream stream)
        {
            var scenery = Create<PS2AnyScenery>();
            using var reader = new BinaryReader(stream);
            scenery.Name = reader.ReadString();
            scenery.FogColor = reader.ReadUInt32();
            scenery.UnusedByte = reader.ReadByte();
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

                var spotLights = reader.ReadInt32();
                for (var i = 0; i < spotLights; ++i)
                {
                    var spot = new SpotLight();
                    spot.Read(reader, spot.GetLength());
                    scenery.SpotLights.Add(spot);
                }

                var lightOrder = reader.ReadInt32();
                for (var i = 0; i < lightOrder; ++i)
                {
                    scenery.LightOrder.Add(reader.ReadInt32());
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

        public virtual ITwinSkin GenerateSkin(List<SkinPartExport> parts)
        {
            var skin = new PS2AnySkin();
            foreach (var part in parts)
            {
                var compression = GetFittingCompression(part.Compression, part.Vertexes);
                PS2SubSkin NewSubSkin() => new()
                {
                    Vertexes = [],
                    UVW = [],
                    Colors = [],
                    SkinJoints = [],
                    Material = part.Material,
                    GroupSizes = [],
                    Compression = compression?.Clone(),
                    Padding = part.Layout.Padding
                };

                // A sub skin's packet goes out with one DMA tag, which sends 65535 quad words at most. Meshes made in Blender can have
                // many times the vertexes of the game's, their batches are spread over several sub skins of the same material
                var subSkin = NewSubSkin();
                var packetBytes = PacketStartBytes;
                foreach (var batch in part.Layout.Batches)
                {
                    var batchBytes = TwinVIFCompiler.SkinBatchBytes(batch.Vertexes.Count);
                    if (subSkin.GroupSizes.Count > 0 && packetBytes + batchBytes > TwinVIFCompiler.MaxPacketBytes)
                    {
                        skin.SubSkins.Add(subSkin);
                        subSkin = NewSubSkin();
                        packetBytes = PacketStartBytes;
                    }

                    subSkin.GroupSizes.Add(batch.Vertexes.Count);
                    AddSkinVertexes(batch, part.Vertexes, subSkin.Vertexes, subSkin.UVW, subSkin.Colors, subSkin.SkinJoints);
                    packetBytes += batchBytes;
                }

                skin.SubSkins.Add(subSkin);
            }

            return skin;
        }

        public ITwinSkydome GenerateSkydome(Stream stream)
        {
            var skydome = Create<PS2AnySkydome>();
            using var reader = new BinaryReader(stream);
            skydome.Read(reader, (Int32)stream.Length);
            return skydome;
        }

        public virtual ITwinSound GenerateSound()
        {
            return new PS2AnySound();
        }

        public ITwinSurface GenerateSurface(Stream stream)
        {
            var surface = Create<PS2AnyCollisionSurface>();
            using var reader = new BinaryReader(stream);
            surface.Read(reader, (Int32)stream.Length);
            return surface;
        }

        public ITwinTemplate GenerateTemplate(Stream stream)
        {
            var template = Create<PS2AnyTemplate>();
            using var reader = new BinaryReader(stream);
            template.Read(reader, (Int32)stream.Length);
            return template;
        }

        public virtual ITwinTexture GenerateTexture()
        {
            return new PS2AnyTexture();
        }

        public ITwinTrigger GenerateTrigger(Stream stream)
        {
            var trigger = Create<PS2AnyTrigger>();
            using var reader = new BinaryReader(stream);
            trigger.Read(reader, (Int32)stream.Length);
            return trigger;
        }

        public virtual ITwinSection GenerateFrontend(List<ITwinSound> sounds)
        {
            var frontend = new PS2Frontend();

            return frontend;
        }

        public virtual ITwinPSF GenerateFont(List<ITwinPTC> pages, List<VectorCharacterData> characterData, Int32 spaceIdentifier)
        {
            var font = new PS2PSF();

            foreach (var page in pages)
            {
                font.FontPages.Add(page);
            }

            font.CharacterData = CloneUtils.CloneList(characterData);
            font.SpaceIdentifier = spaceIdentifier;

            return font;
        }

        public virtual ITwinPTC GeneratePTC(UInt32 texID, UInt32 matID, ITwinTexture texture, ITwinMaterial material)
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

        public virtual ITwinPSM GeneratePSM(List<ITwinPTC> ptcs)
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
            var @default = CreateSection<PS2Default>();
            @default.SetRoot(@default);

            var graphics = CreateSection<PS2AnyGraphicsSection>();
            graphics.SetID(Constants.LEVEL_GRAPHICS_SECTION);
            FillGraphicsSection(graphics, @default);

            var code = CreateSection<PS2AnyCodeSection>();
            FillCodeSection(code, @default);

            var layout1 = CreateSection<PS2AnyLayoutSection>();
            layout1.SetID(Constants.LEVEL_LAYOUT_1_SECTION);
            FillLayoutSection(layout1, @default);

            var layout2 = CreateSection<PS2AnyLayoutSection>();
            layout2.SetID(Constants.LEVEL_LAYOUT_2_SECTION);
            layout2.SetRoot(@default);
            layout2.SetParent(@default);

            var layout3 = CreateSection<PS2AnyLayoutSection>();
            layout3.SetID(Constants.LEVEL_LAYOUT_3_SECTION);
            layout3.SetRoot(@default);
            layout3.SetParent(@default);

            var layout4 = CreateSection<PS2AnyLayoutSection>();
            layout4.SetID(Constants.LEVEL_LAYOUT_4_SECTION);
            layout4.SetRoot(@default);
            layout4.SetParent(@default);

            var layout5 = CreateSection<PS2AnyLayoutSection>();
            layout5.SetID(Constants.LEVEL_LAYOUT_5_SECTION);
            layout5.SetRoot(@default);
            layout5.SetParent(@default);

            var layout6 = CreateSection<PS2AnyLayoutSection>();
            layout6.SetID(Constants.LEVEL_LAYOUT_6_SECTION);
            layout6.SetRoot(@default);
            layout6.SetParent(@default);

            var layout7 = CreateSection<PS2AnyLayoutSection>();
            layout7.SetID(Constants.LEVEL_LAYOUT_7_SECTION);
            layout7.SetRoot(@default);
            layout7.SetParent(@default);

            var layout8 = CreateSection<PS2AnyLayoutSection>();
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
            var rm2 = CreateSection<PS2AnyTwinsanityRM2>();
            var graphics = CreateSection<PS2AnyGraphicsSection>();
            graphics.SetID(Constants.LEVEL_GRAPHICS_SECTION);
            FillGraphicsSection(graphics, rm2);

            var code = CreateSection<PS2AnyCodeSection>();
            FillCodeSection(code, rm2);

            rm2.AddItem(graphics);
            rm2.AddItem(code);

            for (UInt32 i = 0; i < 7; ++i)
            {
                var layout = CreateSection<PS2AnyLayoutSection>();
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
            var sm2 = CreateSection<PS2AnyTwinsanitySM2>();
            var graphics = CreateSection<PS2AnyGraphicsSection>();
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

        private void FillLayoutSection(BaseTwinSection layout, ITwinSection root)
        {
            layout.SetRoot(root);
            layout.SetParent(root);
            {
                var templates = CreateSection<PS2AnyTemplatesSection>();
                templates.SetID(Constants.LAYOUT_TEMPLATES_SECTION);
                templates.SetRoot(root);
                templates.SetParent(layout);
                var aiPositions = CreateSection<PS2AnyAIPositionsSection>();
                aiPositions.SetID(Constants.LAYOUT_AI_POSITIONS_SECTION);
                aiPositions.SetRoot(root);
                aiPositions.SetParent(layout);
                var aiPaths = CreateSection<PS2AnyAIPathsSection>();
                aiPaths.SetID(Constants.LAYOUT_AI_PATHS_SECTION);
                aiPaths.SetRoot(root);
                aiPaths.SetParent(layout);
                var positions = CreateSection<PS2AnyPositionsSection>();
                positions.SetID(Constants.LAYOUT_POSITIONS_SECTION);
                positions.SetRoot(root);
                positions.SetParent(layout);
                var paths = CreateSection<PS2AnyPathsSection>();
                paths.SetID(Constants.LAYOUT_PATHS_SECTION);
                paths.SetRoot(root);
                paths.SetParent(layout);
                var surfaces = CreateSection<PS2AnySurfacesSection>();
                surfaces.SetID(Constants.LAYOUT_SURFACES_SECTION);
                surfaces.SetRoot(root);
                surfaces.SetParent(layout);
                var instances = CreateSection<PS2AnyInstancesSection>();
                instances.SetID(Constants.LAYOUT_INSTANCES_SECTION);
                instances.SetRoot(root);
                instances.SetParent(layout);
                var triggers = CreateSection<PS2AnyTriggersSection>();
                triggers.SetID(Constants.LAYOUT_TRIGGERS_SECTION);
                triggers.SetRoot(root);
                triggers.SetParent(layout);
                var cameras = CreateSection<PS2AnyCamerasSection>();
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

        private void FillGraphicsSection(BaseTwinSection graphics, ITwinSection root)
        {
            graphics.SetRoot(root);
            graphics.SetParent(root);
            {
                var textures = CreateSection<PS2AnyTexturesSection>();
                textures.SetID(Constants.GRAPHICS_TEXTURES_SECTION);
                textures.SetRoot(root);
                textures.SetParent(graphics);
                var materials = CreateSection<PS2AnyMaterialsSection>();
                materials.SetID(Constants.GRAPHICS_MATERIALS_SECTION);
                materials.SetRoot(root);
                materials.SetParent(graphics);
                var models = CreateSection<PS2AnyModelsSection>();
                models.SetID(Constants.GRAPHICS_MODELS_SECTION);
                models.SetRoot(root);
                models.SetParent(graphics);
                var rigids = CreateSection<PS2AnyRigidModelsSection>();
                rigids.SetID(Constants.GRAPHICS_RIGID_MODELS_SECTION);
                rigids.SetRoot(root);
                rigids.SetParent(graphics);
                var skins = CreateSection<PS2AnySkinsSection>();
                skins.SetID(Constants.GRAPHICS_SKINS_SECTION);
                skins.SetRoot(root);
                skins.SetParent(graphics);
                var blendSkins = CreateSection<PS2AnyBlendSkinsSection>();
                blendSkins.SetID(Constants.GRAPHICS_BLEND_SKINS_SECTION);
                blendSkins.SetRoot(root);
                blendSkins.SetParent(graphics);
                var meshes = CreateSection<PS2AnyMeshesSection>();
                meshes.SetID(Constants.GRAPHICS_MESHES_SECTION);
                meshes.SetRoot(root);
                meshes.SetParent(graphics);
                var lods = CreateSection<PS2AnyLODsSection>();
                lods.SetID(Constants.GRAPHICS_LODS_SECTION);
                lods.SetRoot(root);
                lods.SetParent(graphics);
                var skydomes = CreateSection<PS2AnySkydomesSection>();
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

        private void FillCodeSection(BaseTwinSection code, ITwinSection root)
        {
            code.SetID(Constants.LEVEL_CODE_SECTION);
            code.SetRoot(root);
            code.SetParent(root);
            {
                var objects = CreateSection<PS2AnyGameObjectsSection>();
                objects.SetID(Constants.CODE_GAME_OBJECTS_SECTION);
                objects.SetRoot(root);
                objects.SetParent(code);
                var behaviours = CreateSection<PS2AnyBehavioursSection>();
                behaviours.SetID(Constants.CODE_BEHAVIOURS_SECTION);
                behaviours.SetRoot(root);
                behaviours.SetParent(code);
                var animations = CreateSection<PS2AnyAnimationsSection>();
                animations.SetID(Constants.CODE_ANIMATIONS_SECTION);
                animations.SetRoot(root);
                animations.SetParent(code);
                var ogis = CreateSection<PS2AnyOGIsSection>();
                ogis.SetID(Constants.CODE_OGIS_SECTION);
                ogis.SetRoot(root);
                ogis.SetParent(code);
                var behaviourSequences = CreateSection<PS2AnyBehaviourCommandsSequencesSection>();
                behaviourSequences.SetID(Constants.CODE_BEHAVIOUR_COMMANDS_SEQUENCES_SECTION);
                behaviourSequences.SetRoot(root);
                behaviourSequences.SetParent(code);
                var unknowns = new BaseTwinItem();
                unknowns.SetID(Constants.CODE_UNUSED_SECTION);
                unknowns.SetRoot(root);
                unknowns.SetParent(code);
                var sfxs = CreateSection<PS2AnySoundsSection>();
                sfxs.SetID(Constants.CODE_SOUND_EFFECTS_SECTION);
                sfxs.SetRoot(root);
                sfxs.SetParent(code);
                var sfxsEn = CreateSection<PS2AnySoundsSection>();
                sfxsEn.SetID(Constants.CODE_LANG_ENG_SECTION);
                sfxsEn.SetRoot(root);
                sfxsEn.SetParent(code);
                var sfxsFr = CreateSection<PS2AnySoundsSection>();
                sfxsFr.SetID(Constants.CODE_LANG_FRE_SECTION);
                sfxsFr.SetRoot(root);
                sfxsFr.SetParent(code);
                var sfxsGr = CreateSection<PS2AnySoundsSection>();
                sfxsGr.SetID(Constants.CODE_LANG_GER_SECTION);
                sfxsGr.SetRoot(root);
                sfxsGr.SetParent(code);
                var sfxsSp = CreateSection<PS2AnySoundsSection>();
                sfxsSp.SetID(Constants.CODE_LANG_SPA_SECTION);
                sfxsSp.SetRoot(root);
                sfxsSp.SetParent(code);
                var sfxsIt = CreateSection<PS2AnySoundsSection>();
                sfxsIt.SetID(Constants.CODE_LANG_ITA_SECTION);
                sfxsIt.SetRoot(root);
                sfxsIt.SetParent(code);
                var sfxsJp = CreateSection<PS2AnySoundsSection>();
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
