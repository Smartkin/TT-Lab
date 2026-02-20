using Newtonsoft.Json;
using SharpGLTF.Geometry;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Memory;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Project;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using AlphaMode = SharpGLTF.Materials.AlphaMode;
using Texture = TT_Lab.Assets.Graphics.Texture;

namespace TT_Lab.AssetData.Graphics;

using COLOR_UV = SharpGLTF.Geometry.VertexTypes.VertexColor2Texture2;
using JOINT_WEIGHT = SharpGLTF.Geometry.VertexTypes.VertexJoints4;
using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPosition;
using VERTEX_BUILDER = VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPosition, SharpGLTF.Geometry.VertexTypes.VertexColor2Texture2, SharpGLTF.Geometry.VertexTypes.VertexJoints4>;

[ReferencesAssets]
public class BlendSkinData : AbstractAssetData
{
    public BlendSkinData(IAsset asset) : base(asset)
    {
    }

    public BlendSkinData(IAsset asset, ITwinBlendSkin blendSkin) : this(asset)
    {
        SetTwinItem(blendSkin);
    }

    public UInt32? CompileScale { get; set; }
    public Int32 BlendsAmount { get; set; }
    public List<SubBlendData> Blends { get; set; } = [];

    public List<GltfGeometryWrapper> GetMeshes(SharpGLTF.Scenes.NodeBuilder root, List<GltfBone>? jointTree = null)
    {
        var meshes = new List<GltfGeometryWrapper>();
            
        static VERTEX_BUILDER generateVertexFromTwinVertex(Vertex vertex)
        {
            return new VERTEX_BUILDER(new VERTEX(vertex.Position.X, vertex.Position.Y, vertex.Position.Z),
                new COLOR_UV(
                    vertex.Color.ToSystem(),
                    new System.Numerics.Vector4(vertex.Position.W, vertex.Position.W, vertex.Position.W, 1.0f),
                    new System.Numerics.Vector2(vertex.UV.X, vertex.UV.Y),
                    new System.Numerics.Vector2(vertex.UV.Z, vertex.UV.W)),
                new JOINT_WEIGHT(
                    (vertex.JointInfo.JointIndex1, vertex.JointInfo.Weight1),
                    (vertex.JointInfo.JointIndex2, vertex.JointInfo.Weight2),
                    (vertex.JointInfo.JointIndex3, vertex.JointInfo.Weight3)));
        };

        var jointsAmount = 0;
        foreach (var ver in Blends.SelectMany(blend => blend.Models.SelectMany(blendModel => blendModel.Vertexes)))
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
        foreach (var blend in Blends)
        {
            var twinMaterial = AssetManager.Get().GetAssetData<MaterialData>(blend.Material);
            foreach (var shader in twinMaterial.Shaders)
            {
                var material = new SharpGLTF.Materials.MaterialBuilder($"BLEND_SKIN{GraphicsHelpers.MaterialTokenDivider}MATERIAL{GraphicsHelpers.MaterialTokenDivider}{twinMaterial.Name}{GraphicsHelpers.MaterialTokenDivider}{materialIndex}{GraphicsHelpers.MaterialTokenDivider}{shader.ShaderType}")
                    .WithDoubleSide(true);

                if (shader.TextureId == LabURI.Empty)
                {
                    material.WithBaseColor(new System.Numerics.Vector4(1, 1, 1, 1));
                }
                else
                {
                    var textureData = AssetManager.Get().GetAssetData<TextureData>(shader.TextureId);
                    using var ms = new MemoryStream();
                    textureData.Bitmap!.Save(ms, 100);
                    ms.Position = 0;
                    using var binaryReader = new BinaryReader(ms);
                    material.WithBaseColor(SharpGLTF.Materials.ImageBuilder.From(new MemoryImage(binaryReader.ReadBytes((int)ms.Length))));
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

                var index = 0;
                foreach (var blendModel in blend.Models)
                {
                    var mesh = new MeshBuilder<VERTEX, COLOR_UV, JOINT_WEIGHT>($"BLEND_FACE_SKINNED_MESH_{materialIndex}_{index++}")
                    {
                        Extras = System.Text.Json.JsonSerializer.SerializeToNode(new MeshExtraInfo
                        {
                            BlendShape = blendModel.BlendShape,
                            Type = MeshExportType.BlendSkinned
                        })
                    };

                    foreach (var face in blendModel.Faces)
                    {
                        var ver1 = blendModel.Vertexes[face.Indexes![0]];
                        var ver2 = blendModel.Vertexes[face.Indexes[1]];
                        var ver3 = blendModel.Vertexes[face.Indexes[2]];
                        var primitive = mesh.UsePrimitive(material);
                        primitive.AddTriangle(generateVertexFromTwinVertex(ver1), generateVertexFromTwinVertex(ver2), generateVertexFromTwinVertex(ver3));
                    }

                    int findVertexIndex(VERTEX vertex)
                    {
                        var idx = -1;
                        foreach (var ver in blendModel.Vertexes)
                        {
                            if (new VERTEX(ver.Position.X, ver.Position.Y, ver.Position.Z) == vertex.Position)
                            {
                                return idx + 1;
                            }

                            idx++;
                        }

                        return -1;
                    }

                    var totalDelta = 0.0f;
                    for (var i = 0; i < blendModel.BlendFaces.Count; i++)
                    {
                        var blendFace = blendModel.BlendFaces[i];
                        var morph = mesh.UseMorphTarget(i);
                        foreach (var vertex in morph.Vertices)
                        {
                            var newVer = vertex;
                            var shapeIndex = findVertexIndex(vertex);
                            var blendVec = blendFace.BlendShapes[shapeIndex].Offset;

                            newVer.Position += new System.Numerics.Vector3(blendVec.X, blendVec.Y, blendVec.Z);
                            totalDelta += blendVec.Length();
                            morph.SetVertex(vertex, newVer);
                        }
                    }

                    meshes.Add(new GltfGeometryWrapper(mesh, subSkinNodes, totalDelta == 0.0f));
                }
            }
            
            materialIndex++;
        }

        return meshes;
    }

    protected override void Dispose(Boolean disposing)
    {
        foreach (var blend in Blends)
        {
            blend.Dispose();
        }
        Blends.Clear();
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        var scene = new SharpGLTF.Scenes.SceneBuilder("TwinsanityBlendSkin");
        var root = new SharpGLTF.Scenes.NodeBuilder("blend_skin_root");
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

    public override String GetStringified()
    {
        using var stream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(stream);
        foreach (var blend in Blends)
        {
            foreach (var model in blend.Models)
            {
                foreach (var indexedFace in model.Faces)
                {
                    var v1 = model.Vertexes[indexedFace.Indexes![0]];
                    var v2 = model.Vertexes[indexedFace.Indexes![1]];
                    var v3 = model.Vertexes[indexedFace.Indexes![2]];
                    v1.WriteBinary(binaryWriter);
                    v2.WriteBinary(binaryWriter);
                    v3.WriteBinary(binaryWriter);
                }
            }
        }
        binaryWriter.Flush();
        
        stream.Position = 0;
        using var binaryReader = new BinaryReader(stream);
        return new String(binaryReader.ReadChars((int)stream.Length));
    }

    public void LoadFromGltf(Node materialDescs, IReadOnlyList<Mesh> gltfMeshes)
    {
        BlendsAmount = gltfMeshes.Max(m => m.Primitives.Max(prim => prim.GetVertexColumns().MorphTargets.Count));

        var assetManager = AssetManager.Get();
        var materialsGltf = gltfMeshes.SelectMany(m => m.Primitives).Select(prim => prim.Material).Distinct().ToList();
        var blendIndex = 0;
        while (true)
        {
            var materialDescNameMask =
                $"MATERIAL_DESC_FOR_BLEND_{blendIndex}_";
            var materialDesc =
                materialDescs.VisualChildren.FirstOrDefault(n =>
                    n.Name.StartsWith(materialDescNameMask));
            if (materialDesc == null)
            {
                break;
            }
            
            var materialName = materialDesc.Name.Replace(materialDescNameMask, "");
            var labMaterial = new Assets.Graphics.Material
            {
                Package = Owner.Package,
                InvariantName = $"{Owner.Name}_{materialName}_MATERIAL",
                Alias = $"{Owner.Name}_{materialName}_MATERIAL",
                IsInternal = true
            };

            var blendMaterialsGltf = materialsGltf.Where(m => m.Name.Contains($"BLEND_SKIN{GraphicsHelpers.MaterialTokenDivider}MATERIAL{GraphicsHelpers.MaterialTokenDivider}{materialName}{GraphicsHelpers.MaterialTokenDivider}{blendIndex}{GraphicsHelpers.MaterialTokenDivider}"))
                .ToList();
            var materialData = MaterialData.LoadFromGltf(labMaterial, materialDesc, blendMaterialsGltf);
            labMaterial.SetData(materialData);
            
            assetManager.TryAddAsset(labMaterial);
            
            var meshes = gltfMeshes.Where(m => m.Primitives.All(p => p.Material.LogicalIndex == blendMaterialsGltf[0].LogicalIndex)).DistinctBy(m => m.Name).ToList();
            Blends.Add(new SubBlendData(labMaterial.URI, meshes, BlendsAmount));

            blendIndex++;
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var blendSkin = GetTwinItem<ITwinBlendSkin>();
        BlendsAmount = blendSkin.BlendsAmount;
        foreach (var blend in blendSkin.SubBlends)
        {
            Blends.Add(new SubBlendData(Owner, blend));
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        return factory.GenerateBlendSkin(BlendsAmount, Blends, CompileScale);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, UInt32 id, Int32? layoutID = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetParent();
        var materialsSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MATERIALS_SECTION);
        foreach (var blend in Blends)
        {
            assetManager.GetAsset(blend.Material).ResolveChunkResources(factory, materialsSection);
        }
        return base.ResolveChunkResources(factory, section, id, layoutID);
    }
}