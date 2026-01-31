using Newtonsoft.Json;
using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Caliburn.Micro;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using AlphaMode = SharpGLTF.Materials.AlphaMode;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Graphics;

using COLOR_EMIT_UV = VertexColor2Texture1WithAlpha;
using COLOR_UV = VertexColor1Texture1WithAlpha;
using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPosition;
using VERTEX_BUILDER_VCEU = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPosition, VertexColor2Texture1WithAlpha, SharpGLTF.Geometry.VertexTypes.VertexEmpty>;
using VERTEX_BUILDER_VCU = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPosition, VertexColor1Texture1WithAlpha, SharpGLTF.Geometry.VertexTypes.VertexEmpty>;
using VERTEX_BUILDER_VNCEU = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPositionNormal, VertexColor2Texture1WithAlpha, SharpGLTF.Geometry.VertexTypes.VertexEmpty>;
using VERTEX_BUILDER_VNCU = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPositionNormal, VertexColor1Texture1WithAlpha, SharpGLTF.Geometry.VertexTypes.VertexEmpty>;
using VERTEX_NORMAL = SharpGLTF.Geometry.VertexTypes.VertexPositionNormal;

public class ModelData : AbstractAssetData
{
    private class HasEmits
    {
        public bool Value { get; set; }
    }

    public ModelData(IAsset asset) : base(asset)
    {
        Vertexes = new List<List<Vertex>>();
        Faces = new List<List<IndexedFace>>();
        Meshes = new List<MeshProcessor.Mesh>();
    }

    public ModelData(IAsset asset, ITwinModel model) : this(asset)
    {
        SetTwinItem(model);
    }

    public List<List<Vertex>> Vertexes { get; set; }
    public List<List<IndexedFace>> Faces { get; set; }
    public List<MeshProcessor.Mesh> Meshes { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Vertexes.ForEach(vl => vl.Clear());
        Vertexes.Clear();
        Faces.ForEach(fl => fl.Clear());
        Faces.Clear();
        Meshes.ForEach(m => m.Clear());
        Meshes.Clear();
    }

    public List<GltfGeometryWrapper> GetMeshes(SharpGLTF.Scenes.NodeBuilder root, List<MaterialData>? material = null)
    {
        var meshes = new List<GltfGeometryWrapper>();
        var materials = GenerateMaterials(material);

        static VERTEX_BUILDER_VNCEU generateVertexFromTwinVertexVNCEU(Vertex vertex)
        {
            return new VERTEX_BUILDER_VNCEU(new VERTEX_NORMAL(
                    vertex.Position.X, vertex.Position.Y, vertex.Position.Z,
                    vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z),
                new COLOR_EMIT_UV(
                    new System.Numerics.Vector4(vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W),
                    new System.Numerics.Vector4(vertex.EmitColor.X, vertex.EmitColor.Y, vertex.EmitColor.Z,
                        vertex.EmitColor.W),
                    new System.Numerics.Vector2(vertex.UV.X, vertex.UV.Y),
                    vertex.AlphaBlendingBit
                ));
        }

        static VERTEX_BUILDER_VNCU generateVertexFromTwinVertexVNCU(Vertex vertex)
        {
            return new VERTEX_BUILDER_VNCU(new VERTEX_NORMAL(
                    vertex.Position.X, vertex.Position.Y, vertex.Position.Z,
                    vertex.Normal.X, vertex.Normal.Y, vertex.Normal.Z),
                new COLOR_UV(
                    new System.Numerics.Vector4(vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W),
                    new System.Numerics.Vector2(vertex.UV.X, vertex.UV.Y),
                    vertex.AlphaBlendingBit
                ));
        }

        static VERTEX_BUILDER_VCEU generateVertexFromTwinVertexVCEU(Vertex vertex)
        {
            return new VERTEX_BUILDER_VCEU(new VERTEX(
                    vertex.Position.X, vertex.Position.Y, vertex.Position.Z),
                new COLOR_EMIT_UV(
                    new System.Numerics.Vector4(vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W),
                    new System.Numerics.Vector4(vertex.EmitColor.X, vertex.EmitColor.Y, vertex.EmitColor.Z,
                        vertex.EmitColor.W),
                    new System.Numerics.Vector2(vertex.UV.X, vertex.UV.Y),
                    vertex.AlphaBlendingBit
                ));
        }

        static VERTEX_BUILDER_VCU generateVertexFromTwinVertexVCU(Vertex vertex)
        {
            return new VERTEX_BUILDER_VCU(new VERTEX(
                    vertex.Position.X, vertex.Position.Y, vertex.Position.Z),
                new COLOR_UV(
                    new System.Numerics.Vector4(vertex.Color.X, vertex.Color.Y, vertex.Color.Z, vertex.Color.W),
                    new System.Numerics.Vector2(vertex.UV.X, vertex.UV.Y),
                    vertex.AlphaBlendingBit
                ));
        }

        static SharpGLTF.Geometry.MeshBuilder<VERTEX_NORMAL, COLOR_EMIT_UV> getMeshBuilderVNCEU(int idx)
        {
            return new SharpGLTF.Geometry.MeshBuilder<VERTEX_NORMAL, COLOR_EMIT_UV>($"mesh_RIGIDIDPLACEHOLDER_{idx}")
            {
                Extras = System.Text.Json.JsonSerializer.SerializeToNode(new MeshExtraInfo { HasEmits = true, Type = MeshExportType.Rigid })
            };
        }

        static SharpGLTF.Geometry.MeshBuilder<VERTEX_NORMAL, COLOR_UV> getMeshBuilderVNCU(int idx)
        {
            return new SharpGLTF.Geometry.MeshBuilder<VERTEX_NORMAL, COLOR_UV>($"mesh_RIGIDIDPLACEHOLDER_{idx}")
            {
                Extras = System.Text.Json.JsonSerializer.SerializeToNode(new MeshExtraInfo { Type = MeshExportType.Rigid })
            };
        }

        static SharpGLTF.Geometry.MeshBuilder<VERTEX, COLOR_EMIT_UV> getMeshBuilderVCEU(int idx)
        {
            return new SharpGLTF.Geometry.MeshBuilder<VERTEX, COLOR_EMIT_UV>($"mesh_RIGIDIDPLACEHOLDER_{idx}")
            {
                Extras = System.Text.Json.JsonSerializer.SerializeToNode(new MeshExtraInfo { HasEmits = true, Type = MeshExportType.Rigid })
            };
        }

        static SharpGLTF.Geometry.MeshBuilder<VERTEX, COLOR_UV> getMeshBuilderVCU(int idx)
        {
            return new SharpGLTF.Geometry.MeshBuilder<VERTEX, COLOR_UV>($"mesh_RIGIDIDPLACEHOLDER_{idx}")
            {
                Extras = System.Text.Json.JsonSerializer.SerializeToNode(new MeshExtraInfo { Type = MeshExportType.Rigid })
            };
        }

        /// Generate mesh with positions, normals, colors, emission and UV coordinates
        void generateMeshWithVNCEU(int idx, List<IndexedFace> faces, List<Vertex> submodel)
        {
            var mesh = getMeshBuilderVNCEU(idx);
            var vertexGenerator = generateVertexFromTwinVertexVNCEU;
            foreach (var face in faces)
            {
                var ver1 = submodel[face.Indexes![0]];
                var ver2 = submodel[face.Indexes[1]];
                var ver3 = submodel[face.Indexes[2]];
                foreach (var materialBuilder in materials[idx].MaterialBuilders)
                {
                    var primitive = mesh.UsePrimitive(materialBuilder);
                    primitive.AddTriangle(vertexGenerator(ver1), vertexGenerator(ver2), vertexGenerator(ver3));
                }
            }

            meshes.Add(new GltfGeometryWrapper(mesh, [
                new GltfBone
                {
                    Node = root,
                    InverseBindMatrix = System.Numerics.Matrix4x4.Identity
                }
            ]));
        }

        /// Generate mesh with positions, normals, colors and UV coordinates
        void generateMeshWithVNCU(int idx, List<IndexedFace> faces, List<Vertex> submodel)
        {
            var mesh = getMeshBuilderVNCU(idx);
            var vertexGenerator = generateVertexFromTwinVertexVNCU;
            foreach (var face in faces)
            {
                var ver1 = submodel[face.Indexes![0]];
                var ver2 = submodel[face.Indexes[1]];
                var ver3 = submodel[face.Indexes[2]];
                foreach (var materialBuilder in materials[idx].MaterialBuilders)
                {
                    var primitive = mesh.UsePrimitive(materialBuilder);
                    primitive.AddTriangle(vertexGenerator(ver1), vertexGenerator(ver2), vertexGenerator(ver3));
                }
            }

            meshes.Add(new GltfGeometryWrapper(mesh, [
                new GltfBone
                {
                    Node = root,
                    InverseBindMatrix = System.Numerics.Matrix4x4.Identity
                }
            ]));
        }

        /// Generate mesh with positions, colors, emission and UV coordinates
        void generateMeshWithVCEU(int idx, List<IndexedFace> faces, List<Vertex> submodel)
        {
            var mesh = getMeshBuilderVCEU(idx);
            var vertexGenerator = generateVertexFromTwinVertexVCEU;
            foreach (var face in faces)
            {
                var ver1 = submodel[face.Indexes![0]];
                var ver2 = submodel[face.Indexes[1]];
                var ver3 = submodel[face.Indexes[2]];
                foreach (var materialBuilder in materials[idx].MaterialBuilders)
                {
                    var primitive = mesh.UsePrimitive(materialBuilder);
                    primitive.AddTriangle(vertexGenerator(ver1), vertexGenerator(ver2), vertexGenerator(ver3));
                }
            }

            meshes.Add(new GltfGeometryWrapper(mesh, [
                new GltfBone
                {
                    Node = root,
                    InverseBindMatrix = System.Numerics.Matrix4x4.Identity
                }
            ]));
        }

        /// Generate mesh with positions, colors and UV coordinates
        void generateMeshWithVCU(int idx, List<IndexedFace> faces, List<Vertex> submodel)
        {
            var mesh = getMeshBuilderVCU(idx);
            var vertexGenerator = generateVertexFromTwinVertexVCU;
            foreach (var face in faces)
            {
                var ver1 = submodel[face.Indexes![0]];
                var ver2 = submodel[face.Indexes[1]];
                var ver3 = submodel[face.Indexes[2]];
                foreach (var materialBuilder in materials[idx].MaterialBuilders)
                {
                    var primitive = mesh.UsePrimitive(materialBuilder);
                    primitive.AddTriangle(vertexGenerator(ver1), vertexGenerator(ver2), vertexGenerator(ver3));
                }
            }

            meshes.Add(new GltfGeometryWrapper(mesh, [
                new GltfBone
                {
                    Node = root,
                    InverseBindMatrix = System.Numerics.Matrix4x4.Identity
                }
            ]));
        }

        for (var i = 0; i < Vertexes.Count; i++)
        {
            var submodel = Vertexes[i];
            var hasNormals = submodel.Any(v => v.HasNormals);
            var hasEmits = submodel.Any(v => v.HasEmitColor);
            switch (hasNormals)
            {
                case true when hasEmits:
                    generateMeshWithVNCEU(i, Faces[i], submodel);
                    break;
                case false when hasEmits:
                    generateMeshWithVCEU(i, Faces[i], submodel);
                    break;
                case true when !hasEmits:
                    generateMeshWithVNCU(i, Faces[i], submodel);
                    break;
                default:
                    generateMeshWithVCU(i, Faces[i], submodel);
                    break;
            }
        }

        return meshes;
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var scene = new SharpGLTF.Scenes.SceneBuilder("TwinsanityMesh");
        var root = new SharpGLTF.Scenes.NodeBuilder("rigid_model_root");
        scene.AddNode(root);
        var meshes = GetMeshes(root);
        foreach (var mesh in meshes)
        {
            scene.AddRigidMesh(mesh.Mesh, root);
        }

        var model = scene.ToGltf2();
        model.SaveGLB(dataPath);
    }

    public void LoadFromGltfMeshes(IReadOnlyList<Mesh> meshes)
    {
        foreach (var mesh in meshes)
        {
            var submodel = new List<Vertex>();
            var faces = new List<IndexedFace>();
            var hasEmitsStored = mesh.Extras.Deserialize<MeshExtraInfo>()!.HasEmits;
            var attributeKey =
                hasEmitsStored ? COLOR_EMIT_UV.ALPHA_BLENDING_ATTRIBUTE : COLOR_UV.ALPHA_BLENDING_ATTRIBUTE;
            foreach (var primitive in mesh.Primitives)
            {
                var vertexes = primitive.GetVertexColumns();
                var hasAlphaBlendingBit = primitive.VertexAccessors.Keys.Any(a => a == attributeKey);
                var alphaBlendingBits = hasAlphaBlendingBit
                    ? primitive.GetVertexAccessor(attributeKey).AsVector4Array()
                    : null;
                for (var i = 0; i < vertexes.Positions.Count; i++)
                {
                    var pos = vertexes.Positions[i].ToTwin();
                    var ver = new Vertex(
                        pos,
                        vertexes.Colors0[i].ToTwin(),
                        vertexes.TexCoords0[i].ToTwin())
                    {
                        AlphaBlendingBit = false
                    };
                    ver.Color.StoresColorWithAlphaBlend = false;
                    // if (alphaBlendingBits != null)
                    // {
                    //     ver.AlphaBlendingBit = Math.Abs(alphaBlendingBits[i].X - 1.0f) < 0.00001f;
                    // }
                    if (vertexes.Normals != null)
                    {
                        ver.Normal = vertexes.Normals[i].ToTwin();
                    }

                    if (hasEmitsStored)
                    {
                        ver.EmitColor = vertexes.Colors1[i].ToTwin();
                        ver.EmitColor.StoresColorWithAlphaBlend = false;
                    }

                    submodel.Add(ver);
                }

                foreach (var (idx1, idx2, idx3) in primitive.GetTriangleIndices())
                {
                    faces.Add(new IndexedFace(idx1, idx2, idx3));
                }
            }

            Vertexes.Add(submodel);
            Faces.Add(faces);
        }
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        Vertexes.Clear();
        Faces.Clear();
        Meshes.Clear();
        var model = ModelRoot.Load(dataPath);

        LoadFromGltfMeshes(model.LogicalMeshes);

        for (var i = 0; i < Vertexes.Count; ++i)
        {
            var mesh = MeshProcessor.MeshProcessor.CreateMesh(Vertexes[i], Faces[i]);
            MeshProcessor.MeshProcessor.ProcessMesh(mesh);
            Meshes.Add(mesh);
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinModel model = GetTwinItem<ITwinModel>();
        Vertexes = new List<List<Vertex>>();
        Faces = new List<List<IndexedFace>>();
        foreach (var e in model.SubModels)
        {
            var vertList = new List<Vertex>();
            var faceList = new List<IndexedFace>();
            Int32 refIndex = 0;
            e.CalculateData();
            for (var j = 0; j < e.Vertexes.Count; ++j)
            {
                if (j < e.Vertexes.Count - 2)
                {
                    if (e.Connection[j + 2])
                    {
                        if (j % 2 == 0)
                        {
                            int[] triIndices = [refIndex, refIndex + 1, refIndex + 2];
                            faceList.Add(new IndexedFace(triIndices));
                        }
                        else
                        {
                            int[] triIndices = [refIndex + 1, refIndex, refIndex + 2];
                            faceList.Add(new IndexedFace(triIndices));
                        }
                    }

                    ++refIndex;
                }

                var ver = new Vertex(e.Vertexes[j], e.Colors[j], e.UVW[j]);
                if (e.EmitColor.Count == e.Vertexes.Count)
                {
                    ver.EmitColor = new Vector4(e.EmitColor[j].X, e.EmitColor[j].Y, e.EmitColor[j].Z, e.EmitColor[j].W);
                }

                if (e.Normals.Count == e.Vertexes.Count)
                {
                    ver.Normal = new Vector4(e.Normals[j].X, e.Normals[j].Y, e.Normals[j].Z, e.Normals[j].W);
                    ver.Normal.Normalize();
                }

                vertList.Add(ver);
            }

            Vertexes.Add(vertList);
            Faces.Add(faceList);
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        return factory.GenerateModel(Meshes);
    }

    private List<GltfMaterialBuilder> GenerateMaterials(List<MaterialData>? twinMaterials = null)
    {
        var materials = new List<GltfMaterialBuilder>();
        var materialIndex = 0;
        foreach (var submodel in Vertexes)
        {
            var hasEmits = submodel.Any(v => v.HasEmitColor);
            var gltfMaterial = new GltfMaterialBuilder([]);
            materials.Add(gltfMaterial);
            
            var twinMaterial = twinMaterials?[materialIndex];

            if (twinMaterial == null)
            {
                var material = new SharpGLTF.Materials.MaterialBuilder($"RIGID{GraphicsHelpers.MaterialTokenDivider}MATERIAL{GraphicsHelpers.MaterialTokenDivider}NULL{GraphicsHelpers.MaterialTokenDivider}{materialIndex++}")
                    .WithDoubleSide(true);
                material.WithBaseColor(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f));
                gltfMaterial.MaterialBuilders.Add(material);
                continue;
            }

            foreach (var shader in twinMaterial.Shaders)
            {
                var material = new SharpGLTF.Materials.MaterialBuilder($"RIGID{GraphicsHelpers.MaterialTokenDivider}MATERIAL{GraphicsHelpers.MaterialTokenDivider}{twinMaterial.Name}{GraphicsHelpers.MaterialTokenDivider}{materialIndex}{GraphicsHelpers.MaterialTokenDivider}{shader.ShaderType}")
                    .WithDoubleSide(true);
                var textureId = shader.TextureId;
                if (textureId == LabURI.Empty)
                {
                    material.WithBaseColor(new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1.0f));
                }
                else
                {
                    var texture = AssetManager.Get().GetAsset<Assets.Graphics.Texture>(textureId);
                    material.WithBaseColor(texture.FullDataPath);
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

                // if (hasEmits)
                // {
                //     var emissionTexture = GenerateEmissionTexture(submodel);
                //     material.WithEmissive(emissionTexture);
                // }
                
                gltfMaterial.MaterialBuilders.Add(material);
            }

            materialIndex++;
        }

        return materials;
    }

}