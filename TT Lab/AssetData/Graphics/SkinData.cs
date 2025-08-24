using Newtonsoft.Json;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Caliburn.Micro;
using SharpGLTF.Memory;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Project;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using AlphaMode = SharpGLTF.Materials.AlphaMode;
using Texture = TT_Lab.Assets.Graphics.Texture;

namespace TT_Lab.AssetData.Graphics
{
    using COLOR_UV = SharpGLTF.Geometry.VertexTypes.VertexColor2Texture2;
    using JOINT_WEIGHT = SharpGLTF.Geometry.VertexTypes.VertexJoints4;
    using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPosition;
    using VERTEX_BUILDER = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPosition, SharpGLTF.Geometry.VertexTypes.VertexColor2Texture2, SharpGLTF.Geometry.VertexTypes.VertexJoints4>;
    
    public struct GltfGeometryWrapper(
        SharpGLTF.Geometry.IMeshBuilder<SharpGLTF.Materials.MaterialBuilder> mesh,
        List<(SharpGLTF.Scenes.NodeBuilder, System.Numerics.Matrix4x4)> joints,
        bool facesSquashedOnExport = false)
    {
        public readonly SharpGLTF.Geometry.IMeshBuilder<SharpGLTF.Materials.MaterialBuilder> Mesh = mesh;
        public readonly List<(SharpGLTF.Scenes.NodeBuilder, System.Numerics.Matrix4x4)> Joints = joints;
        public readonly bool FacesSquashedOnExport = facesSquashedOnExport;
    }
    
    [ReferencesAssets]
    public class SkinData : AbstractAssetData
    {
        public SkinData()
        {
            SubSkins = new List<SubSkinData>();
        }

        public SkinData(ITwinSkin skin) : this()
        {
            SetTwinItem(skin);
        }

        public List<SubSkinData> SubSkins { get; set; }

        public List<GltfGeometryWrapper> GetMeshes(SharpGLTF.Scenes.NodeBuilder root, List<(SharpGLTF.Scenes.NodeBuilder, System.Numerics.Matrix4x4)>? jointTree = null)
        {
            var meshes = new List<GltfGeometryWrapper>();

            static VERTEX_BUILDER generateVertexFromTwinVertex(Vertex vertex)
            {
                return new VERTEX_BUILDER(new VERTEX(vertex.Position.X, vertex.Position.Y, vertex.Position.Z),
                        new COLOR_UV(
                            new System.Numerics.Vector4(vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W),
                            new System.Numerics.Vector4(vertex.Position.W, vertex.Position.W, vertex.Position.W, 1.0f),
                            new System.Numerics.Vector2(vertex.UV.X, vertex.UV.Y),
                            new System.Numerics.Vector2(vertex.UV.Z, vertex.UV.W)),
                        new JOINT_WEIGHT(
                            (vertex.JointInfo.JointIndex1, vertex.JointInfo.Weight1),
                            (vertex.JointInfo.JointIndex2, vertex.JointInfo.Weight2),
                            (vertex.JointInfo.JointIndex3, vertex.JointInfo.Weight3)));
            };

            var jointsAmount = 0;
            foreach (var subSkin in SubSkins)
            {
                foreach (var ver in subSkin.Vertexes)
                {
                    if (ver.JointInfo.JointIndex1 > jointsAmount)
                    {
                        jointsAmount = ver.JointInfo.JointIndex1;
                    }
                    if (ver.JointInfo.JointIndex2 > jointsAmount)
                    {
                        jointsAmount = ver.JointInfo.JointIndex2;
                    }
                    if (ver.JointInfo.JointIndex3 > jointsAmount)
                    {
                        jointsAmount = ver.JointInfo.JointIndex3;
                    }
                }
            }

            // Create all the joint nodes
            var subSkinNodes = jointTree ?? new List<(SharpGLTF.Scenes.NodeBuilder, System.Numerics.Matrix4x4)>();
            if (jointTree == null)
            {
                for (var i = 0; i < jointsAmount + 1; ++i)
                {
                    var node = new SharpGLTF.Scenes.NodeBuilder($"joint_{i}");
                    root.AddNode(node);
                    subSkinNodes.Add((node, System.Numerics.Matrix4x4.Identity));
                }
            }

            var index = 0;
            foreach (var subSkin in SubSkins)
            {
                var twinMaterial = AssetManager.Get().GetAssetData<MaterialData>(subSkin.Material);
                var texture = twinMaterial.Shaders[0].TextureId == LabURI.Empty ? null : AssetManager.Get().GetAsset<Texture>(twinMaterial.Shaders[0].TextureId);
                var texturePath = texture == null ? null : $"{IoC.Get<ProjectManager>().OpenedProject!.ProjectPath}/assets/{nameof(Texture)}/{texture.Data}";
                var material = new SharpGLTF.Materials.MaterialBuilder($"Material_{index}")
                    .WithDoubleSide(true);
                if (texturePath == null)
                {
                    material.WithBaseColor(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1));
                }
                else
                {
                    material.WithBaseColor(texturePath);
                }

                var blendMode = AlphaMode.OPAQUE;
                if (twinMaterial.Shaders[0].ABlending == TwinShader.AlphaBlending.ON)
                {
                    blendMode = AlphaMode.BLEND;
                }
                if (twinMaterial.Shaders[0].ATest == TwinShader.AlphaTest.ON)
                {
                    blendMode = AlphaMode.MASK;
                }
                material.WithAlpha(blendMode);
                if (blendMode == AlphaMode.MASK)
                {
                    material.AlphaCutoff = twinMaterial.Shaders[0].AlphaValueToBeComparedTo / 255.0f;
                }

                var mesh = new SharpGLTF.Geometry.MeshBuilder<VERTEX, COLOR_UV, JOINT_WEIGHT>($"subskin_{index++}");
                foreach (var face in subSkin.Faces)
                {
                    var ver1 = subSkin.Vertexes[face.Indexes![0]];
                    var ver2 = subSkin.Vertexes[face.Indexes[1]];
                    var ver3 = subSkin.Vertexes[face.Indexes[2]];
                    var primitive = mesh.UsePrimitive(material);
                    primitive.AddTriangle(generateVertexFromTwinVertex(ver1), generateVertexFromTwinVertex(ver2), generateVertexFromTwinVertex(ver3));
                }

                meshes.Add(new GltfGeometryWrapper(mesh, subSkinNodes));
            }

            return meshes;
        }

        protected override void Dispose(Boolean disposing)
        {
            SubSkins.ForEach(s => s.Dispose());
            SubSkins.Clear();
        }

        protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
        {
            var scene = new SharpGLTF.Scenes.SceneBuilder("TwinsanitySkin");
            var root = new SharpGLTF.Scenes.NodeBuilder("skin_root");
            scene.AddNode(root);
            var meshes = GetMeshes(root);
            foreach (var mesh in meshes)
            {
                scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.ToArray());
            }

            var model = scene.ToGltf2();
            model.SaveGLB(dataPath);

            var materialsUri = SubSkins.Select(subSkin => AssetManager.Get().GetAsset(subSkin.Material).URI).ToList();
            using System.IO.FileStream fs = new(dataPath + ".meta", System.IO.FileMode.Create, System.IO.FileAccess.Write);
            using System.IO.BinaryWriter writer = new(fs);
            writer.Write(JsonConvert.SerializeObject(materialsUri, Formatting.Indented, settings).ToCharArray());
        }

        protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
        {
            var materialsUri = new List<LabURI>();
            var skin = ModelRoot.Load(dataPath);

            using System.IO.FileStream fs = new(dataPath + ".meta", System.IO.FileMode.Open, System.IO.FileAccess.Read);
            using System.IO.StreamReader reader = new(fs);
            JsonConvert.PopulateObject(value: reader.ReadToEnd(), target: materialsUri, settings);

            var materialIndex = 0;
            foreach (var mesh in skin.LogicalMeshes)
            {
                var subskin = new List<Vertex>();
                var faces = new List<IndexedFace>();
                foreach (var primitive in mesh.Primitives)
                {
                    var vertexes = primitive.GetVertexColumns();
                    for (var i = 0; i < vertexes.Positions.Count; i++)
                    {
                        var pos = vertexes.Positions[i].ToTwin();
                        pos.W = vertexes.Colors1[i].X;
                        var ver = new Vertex(
                            pos,
                            vertexes.Colors0[i].ToTwin(),
                            vertexes.TexCoords0[i].ToTwin());
                        ver.UV.Z = vertexes.TexCoords1[i].X;
                        ver.UV.W = vertexes.TexCoords1[i].Y;
                        ver.Color = new Vector4(ver.Color.X, ver.Color.Y, ver.Color.Z, ver.Color.W);
                        ver.JointInfo.JointIndex1 = (Int32)vertexes.Joints0[i].X;
                        ver.JointInfo.JointIndex2 = (Int32)vertexes.Joints0[i].Y;
                        ver.JointInfo.JointIndex3 = (Int32)vertexes.Joints0[i].Z;
                        ver.JointInfo.Weight1 = vertexes.Weights0[i].X;
                        ver.JointInfo.Weight2 = vertexes.Weights0[i].Y;
                        ver.JointInfo.Weight3 = vertexes.Weights0[i].Z;

                        subskin.Add(ver);
                    }

                    foreach (var (idx1, idx2, idx3) in primitive.GetTriangleIndices())
                    {
                        faces.Add(new IndexedFace(idx1, idx2, idx3));
                    }
                }

                var material = materialsUri[materialIndex++];
                SubSkins.Add(new SubSkinData(material, subskin, faces));
            }

        }

        public override void Import(LabURI package, String? variant, Int32? layoutId)
        {
            ITwinSkin skin = GetTwinItem<ITwinSkin>();
            SubSkins = new List<SubSkinData>();
            foreach (var e in skin.SubSkins)
            {
                SubSkins.Add(new SubSkinData(package, variant, e));
            }
        }

        public override ITwinItem Export(ITwinItemFactory factory)
        {
            return factory.GenerateSkin(SubSkins);
        }

        public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
        {
            var assetManager = AssetManager.Get();
            var graphicsSection = section.GetRoot().GetItem<ITwinSection>(Constants.LEVEL_GRAPHICS_SECTION);
            var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
            foreach (var subSkin in SubSkins)
            {
                assetManager.GetAsset(subSkin.Material).ResolveChunkResources(factory, materialsSection);
            }
            return base.ResolveChunkResources(factory, section, id, layoutID);
        }
    }
}
