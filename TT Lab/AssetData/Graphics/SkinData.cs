using Newtonsoft.Json;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using SharpGLTF.Memory;
using TT_Lab.AssetData.Code;
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
using Material = TT_Lab.Assets.Graphics.Material;
using Texture = TT_Lab.Assets.Graphics.Texture;

namespace TT_Lab.AssetData.Graphics;

using COLOR_UV = SharpGLTF.Geometry.VertexTypes.VertexColor2Texture2;
using JOINT_WEIGHT = SharpGLTF.Geometry.VertexTypes.VertexJoints4;
using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPosition;
using VERTEX_BUILDER = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPosition, SharpGLTF.Geometry.VertexTypes.VertexColor2Texture2, SharpGLTF.Geometry.VertexTypes.VertexJoints4>;
    
[ReferencesAssets]
public class SkinData : AbstractAssetData
{
    public SkinData(IAsset asset) : base(asset)
    {
        SubSkins = new List<SubSkinData>();
    }

    public SkinData(IAsset asset, ITwinSkin skin) : this(asset)
    {
        SetTwinItem(skin);
    }

    public List<SubSkinData> SubSkins { get; set; }

    public List<GltfGeometryWrapper> GetMeshes(SharpGLTF.Scenes.NodeBuilder root, List<GltfBone>? jointTree = null)
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
        var subSkinNodes = jointTree ?? [];
        if (jointTree == null)
        {
            for (var i = 0; i < jointsAmount + 1; ++i)
            {
                var node = new SharpGLTF.Scenes.NodeBuilder($"joint_{i}");
                root.AddNode(node);
                subSkinNodes.Add(new GltfBone
                {
                    Node = node,
                    InverseBindMatrix = System.Numerics.Matrix4x4.Identity,
                    Parent = null
                });
            }
        }

        var materialIndex = 0;
        foreach (var subSkin in SubSkins)
        {
            var twinMaterial = AssetManager.Get().GetAssetData<MaterialData>(subSkin.Material);
            foreach (var shader in twinMaterial.Shaders)
            {
                var texture = shader.TextureId == LabURI.Empty ? null : AssetManager.Get().GetAsset<Texture>(shader.TextureId);
                var texturePath = texture?.FullDataPath;
                var material = new SharpGLTF.Materials.MaterialBuilder($"SKIN{GraphicsHelpers.MaterialTokenDivider}MATERIAL{GraphicsHelpers.MaterialTokenDivider}{twinMaterial.Name}{GraphicsHelpers.MaterialTokenDivider}{materialIndex}{GraphicsHelpers.MaterialTokenDivider}{shader.ShaderType}")
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
                if (shader.ABlending == TwinShader.AlphaBlending.ON)
                {
                    blendMode = AlphaMode.BLEND;
                }
                if (shader.ATest == TwinShader.AlphaTest.ON)
                {
                    blendMode = AlphaMode.MASK;
                }
                material.WithAlpha(blendMode);
                if (blendMode == AlphaMode.MASK)
                {
                    material.AlphaCutoff = shader.AlphaValueToBeComparedTo / 255.0f;
                }

                material.Extras = shader.GetJsonFormat();

                var mesh = new SharpGLTF.Geometry.MeshBuilder<VERTEX, COLOR_UV, JOINT_WEIGHT>($"SKINNED_MESH_{materialIndex}")
                    {
                        Extras = System.Text.Json.JsonSerializer.SerializeToNode(new MeshExtraInfo { Type = MeshExportType.Skinned })
                    };
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

            materialIndex++;
        }

        return meshes;
    }

    public override String GetStringified()
    {
        using var stream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(stream);
        foreach (var subSkin in SubSkins)
        {
            foreach (var indexedFace in subSkin.Faces)
            {
                var v1 = subSkin.Vertexes[indexedFace.Indexes![0]];
                var v2 = subSkin.Vertexes[indexedFace.Indexes![1]];
                var v3 = subSkin.Vertexes[indexedFace.Indexes![2]];
                v1.WriteBinary(binaryWriter);
                v2.WriteBinary(binaryWriter);
                v3.WriteBinary(binaryWriter);
            }
        }
        binaryWriter.Flush();
        
        stream.Position = 0;
        using var binaryReader = new BinaryReader(stream);
        return new String(binaryReader.ReadChars((int)stream.Length));
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
            scene.AddSkinnedMesh(mesh.Mesh, mesh.Joints.Select(j => (j.Node, j.InverseBindMatrix)).ToArray());
        }

        var model = scene.ToGltf2();
        model.SaveGLB(dataPath);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
    }

    public void LoadFromGltf(IReadOnlyList<Mesh> meshes, Node materialDescs)
    {
        var assetManager = AssetManager.Get();
        var materialsGltf = meshes.SelectMany(m => m.Primitives).Select(prim => prim.Material).Distinct().ToList();
        foreach (var mesh in meshes.DistinctBy(m => m.Name).ToList())
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
            
            var materialIndexToken = mesh.Name.Split('_')[2];
            var materialIndex = int.Parse(materialIndexToken);
            var materialDescNameMask =
                $"MATERIAL_DESC_FOR_SKIN_{materialIndex}_";
            var materialDesc =
                materialDescs.VisualChildren.FirstOrDefault(n =>
                    n.Name.StartsWith(materialDescNameMask))!;
            var materialName = materialDesc.Name.Replace(materialDescNameMask, "");
            
            var material = new Material
            {
                Package = Owner.Package,
                InvariantName = $"{mesh.Name}_{materialName}_MATERIAL",
                Alias = $"{mesh.Name}_{materialName}_MATERIAL",
                IsInternal = true
            };

            var materialData = MaterialData.LoadFromGltf(material, materialDesc,
                materialsGltf.Where(m => m.Name.Contains($"SKIN{GraphicsHelpers.MaterialTokenDivider}MATERIAL{GraphicsHelpers.MaterialTokenDivider}{materialName}{GraphicsHelpers.MaterialTokenDivider}{materialIndex}{GraphicsHelpers.MaterialTokenDivider}"))
                    .ToList());
            material.SetData(materialData);
            
            assetManager.TryAddAsset(material);
            var materialUri = material.URI;
            SubSkins.Add(new SubSkinData(materialUri, subskin, faces));
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var skin = GetTwinItem<ITwinSkin>();
        SubSkins = [];
        foreach (var e in skin.SubSkins)
        {
            SubSkins.Add(new SubSkinData(Owner, e));
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