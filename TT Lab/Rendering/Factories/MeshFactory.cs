using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GlmSharp;
using Newtonsoft.Json.Linq;
using Silk.NET.OpenGL;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using Twinsanity.TwinsanityInterchange.Common;
using BlendSkin = TT_Lab.Assets.Graphics.BlendSkin;
using Collision = TT_Lab.Assets.Instance.Collision;
using CollisionMesh = TT_Lab.Rendering.Objects.Collision;
using Model = TT_Lab.Assets.Graphics.Model;
using RigidModel = TT_Lab.Assets.Graphics.RigidModel;
using Skin = TT_Lab.Assets.Graphics.Skin;

namespace TT_Lab.Rendering.Factories;

public class MeshFactory
{
    private readonly RenderContext _renderContext;
    private readonly MeshBuilder _meshBuilder;
    private readonly MaterialFactory _materialFactory;
    private readonly Dictionary<Type, Func<object, Mesh>> _constructors = [];
    private readonly Dictionary<LabURI, Func<Mesh>> _builtInConstructors = [];

    public MeshFactory(RenderContext renderContext, MeshBuilder meshBuilder, MaterialFactory materialFactory)
    {
        _renderContext = renderContext;
        _meshBuilder = meshBuilder;
        _materialFactory = materialFactory;
        
        _constructors.Add(typeof(Model), obj => CreateUntexturedMesh((ModelData)obj));
        _constructors.Add(typeof(RigidModel), obj => CreateRigidMesh((RigidModelData)obj));
        _constructors.Add(typeof(Assets.Graphics.Mesh), obj => CreateRigidMesh((RigidModelData)obj));
        _constructors.Add(typeof(Skin), obj => CreateSkinnedMesh((SkinData)obj));
        _constructors.Add(typeof(BlendSkin), obj => CreateBlendSkinnedMesh((BlendSkinData)obj));
        _constructors.Add(typeof(Collision), obj => CreateCollisionMesh((CollisionData)obj));
        
        _builtInConstructors.Add(LabURI.Plane, CreatePlane);
        _builtInConstructors.Add(LabURI.Box, CreateCube);
        _builtInConstructors.Add(LabURI.Circle, CreateCircle);
    }
    
    public Mesh? CreateMesh(LabURI uri)
    {
        if (uri == LabURI.Empty)
        {
            return null;
        }

        if (uri.IsBuiltIn())
        {
            return _builtInConstructors.TryGetValue(uri, out var primitiveConstructor) ? primitiveConstructor() : null;
        }
        
        var assetManager = AssetManager.Get();
        var asset = assetManager.GetAsset(uri);
        Debug.Assert(_constructors.ContainsKey(asset.GetType()), $"Unsupported mesh type {asset.GetType()}");
        return _constructors[asset.GetType()](asset.GetData<AbstractAssetData>());
    }

    private Mesh CreateCube()
    {
        float[] cubeVertecies = {
            -1.0f,-1.0f,-1.0f,
            -1.0f,-1.0f, 1.0f,
            -1.0f, 1.0f, 1.0f,
            1.0f, 1.0f,-1.0f,
            -1.0f,-1.0f,-1.0f,
            -1.0f, 1.0f,-1.0f,
            1.0f,-1.0f, 1.0f,
            -1.0f,-1.0f,-1.0f,
            1.0f,-1.0f,-1.0f,
            1.0f, 1.0f,-1.0f,
            1.0f,-1.0f,-1.0f,
            -1.0f,-1.0f,-1.0f,
            -1.0f,-1.0f,-1.0f,
            -1.0f, 1.0f, 1.0f,
            -1.0f, 1.0f,-1.0f,
            1.0f,-1.0f, 1.0f,
            -1.0f,-1.0f, 1.0f,
            -1.0f,-1.0f,-1.0f,
            -1.0f, 1.0f, 1.0f,
            -1.0f,-1.0f, 1.0f,
            1.0f,-1.0f, 1.0f,
            1.0f, 1.0f, 1.0f,
            1.0f,-1.0f,-1.0f,
            1.0f, 1.0f,-1.0f,
            1.0f,-1.0f,-1.0f,
            1.0f, 1.0f, 1.0f,
            1.0f,-1.0f, 1.0f,
            1.0f, 1.0f, 1.0f,
            1.0f, 1.0f,-1.0f,
            -1.0f, 1.0f,-1.0f,
            1.0f, 1.0f, 1.0f,
            -1.0f, 1.0f,-1.0f,
            -1.0f, 1.0f, 1.0f,
            1.0f, 1.0f, 1.0f,
            -1.0f, 1.0f, 1.0f,
            1.0f,-1.0f, 1.0f
        };
        
        var vectors = new List<vec3>();
        var faces = new List<IndexedFace>();
        for (var i = 0; i < cubeVertecies.Length; i += 3)
        {
            vectors.Add(new vec3(cubeVertecies[i], cubeVertecies[i + 1], cubeVertecies[i + 2]));
        }
        for (var i = 0; i < vectors.Count; i += 3)
        {
            faces.Add(new IndexedFace { Indexes = [i + 2, i + 1, i] });
        }
        
        var material = new MaterialData();
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.OFF;
        material.Shaders[0].ShaderType = TwinShader.Type.ColorOnly;
        material.Shaders[0].ABlending = TwinShader.AlphaBlending.ON;
        var buffer = new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(vectors.Select((v, i) => new Vertex(new Vector4(v.x, v.y, v.z, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f))).ToList(), faces), _materialFactory, material);
        return new Mesh(_renderContext, [buffer]);
    }

    private Mesh CreateCircle()
    {
        var segmentPart = 1.0f;
        var thickness = 0.1f;
        var resolution = 16;
        var segment = 2 * System.Math.PI * segmentPart;
        List<vec3> vectors = new List<vec3>();
        var step = (2 * System.Math.PI) / resolution;
        var k = 1.0f - thickness;
        for (var i = 0; i <= resolution; ++i)
        {
            var step1 = i * step;
            if (step1 > segment)
            {
                break;
            }
            var step2 = System.Math.Min((i + 1) * step, segment);
            vectors.Add(new vec3((float)System.Math.Cos(step1), 0, (float)System.Math.Sin(step1)));
            vectors.Add(new vec3((float)System.Math.Cos(step1) * k, 0, (float)System.Math.Sin(step1) * k));
            vectors.Add(new vec3((float)System.Math.Cos(step2) * k, 0, (float)System.Math.Sin(step2)));
            vectors.Add(new vec3((float)System.Math.Cos(step1), 0, (float)System.Math.Sin(step1)));
            vectors.Add(new vec3((float)System.Math.Cos(step2) * k, 0, (float)System.Math.Sin(step2) * k));
            vectors.Add(new vec3((float)System.Math.Cos(step2), 0, (float)System.Math.Sin(step2)));
        }
        var faces = new List<IndexedFace>();
        for (var i = 0; i < vectors.Count; i += 3)
        {
            faces.Add(new IndexedFace { Indexes = [i + 2, i + 1, i] });
        }
        
        var material = new MaterialData();
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.OFF;
        material.Shaders[0].ShaderType = TwinShader.Type.ColorOnly;
        material.Shaders[0].ABlending = TwinShader.AlphaBlending.ON;
        var buffer = new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(vectors.Select((v, i) => new Vertex(new Vector4(v.x, v.y, v.z, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f))).ToList(), faces), _materialFactory, material);
        return new Mesh(_renderContext, [buffer]);
    }
    
    private Mesh CreatePlane()
    {
        var vertices = new float[] {
            -1, -1, 0,  // pos
            1, -1, 0,
            -1,  1, 0,
            -1,  1, 0 ,
            1,  -1, 0 ,
            1,  1, 0 ,
        };
        
        var vectors = new List<vec3>();
        var faces = new List<IndexedFace>();
        var uvs = new List<vec2>()
        {
            new vec2(0, 0),
            new vec2(1, 0),
            new vec2(0, 1),
            new vec2(0, 1),
            new vec2(1, 0),
            new vec2(1, 1),
        };
        for (var i = 0; i < vertices.Length; i += 3)
        {
            vectors.Add(new vec3(vertices[i], vertices[i + 1], vertices[i + 2]));
        }
        for (var i = 0; i < vectors.Count; i += 3)
        {
            faces.Add(new IndexedFace { Indexes = [i + 2, i + 1, i] });
        }

        var material = new MaterialData();
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.OFF;
        material.Shaders[0].ShaderType = TwinShader.Type.ColorOnly;
        material.Shaders[0].ABlending = TwinShader.AlphaBlending.ON;
        var buffer = new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(vectors.Select((v, i) => new Vertex(new Vector4(v.x, v.y, v.z, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector4(uvs[i].x, uvs[i].y, 0.0f, 0.0f))).ToList(), faces), _materialFactory, material);
        return new Mesh(_renderContext, [buffer]);
    }

    private Mesh CreateUntexturedMesh(ModelData data)
    {
        var material = new MaterialData();
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
        material.Shaders[0].ShaderType = TwinShader.Type.UnlitGlossy;
        material.Shaders[0].TextureId = LabURI.BoatGuy;
        var buffers = data.Vertexes.Select((t, i) => new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(t, data.Faces[i]), _materialFactory, material)).ToList();
        return new Mesh(_renderContext, buffers);
    }

    private Mesh CreateRigidMesh(RigidModelData rigidModelData)
    {
        var assetManager = AssetManager.Get();
        var materials = rigidModelData.Materials.Select(m =>
        {
            var material = assetManager.GetAssetData<MaterialData>(m);
            return material;
        }).ToList();
        var modelData = assetManager.GetAssetData<ModelData>(rigidModelData.Model);
        var buffers = modelData.Vertexes.Select((t, i) =>
            new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(t, modelData.Faces[i]), _materialFactory, materials[i])).ToList();
        return new Mesh(_renderContext, buffers);
    }

    private CollisionMesh CreateCollisionMesh(CollisionData collisionData)
    {
        var assetManager = AssetManager.Get();
        var material = new MaterialData();
        material.Shaders[0].ShaderType = TwinShader.Type.StandardUnlit;
        List<ModelBuffer> buffers = [new(_renderContext,
            _meshBuilder.BuildRigidVaoFromVertexes(
                collisionData.Vectors.Select(v => new Vertex(new Vector4(v.X, v.Y, v.Z, v.W))).ToList(),
                collisionData.Triangles.Select(t => t.Face).ToList(),
                i =>
                {
                    var surface = assetManager.GetAsset(collisionData.Triangles[i].Surface);

                    var surfColor = CollisionSurface.DefaultColor;
                    if (surface.Parameters["editor_surface_color"] is JObject colorJson)
                    {
                        surfColor = colorJson.ToObject<Color>();
                    }
                    return surfColor.GetVector();
                }),
            _materialFactory, material)];
        
        return new CollisionMesh(_renderContext, buffers);
    }

    private SkinnedMesh CreateSkinnedMesh(SkinData skin)
    {
        var assetManager = AssetManager.Get();
        var buffers = skin.SubSkins.Select(ss =>
        {
            var material = assetManager.GetAssetData<MaterialData>(ss.Material);
            return new ModelBuffer(_renderContext, _meshBuilder.BuildSkinnedVaoFromVertexes(ss.Vertexes, ss.Faces), _materialFactory, material);
        }).ToList();
        
        return new SkinnedMesh(_renderContext, buffers);
    }

    private BlendSkinnedMesh CreateBlendSkinnedMesh(BlendSkinData blendSkin)
    {
        var assetManager = AssetManager.Get();
        var buffers = new List<ModelBufferBlendSkin>();
        var offsetData = new byte[256 * 256 * 4];
        var blendShape = vec3.Ones;
        var offsetDataIndex = 0;
        var facesAmount = 0;
        foreach (var blend in blendSkin.Blends)
        {
            var material = assetManager.GetAssetData<MaterialData>(blend.Material);
            foreach (var blendModel in blend.Models)
            {
                var indices = new List<Int32>();
                foreach (var face in blendModel.Faces)
                {
                    indices.Add(face.Indexes![0]);
                    indices.Add(face.Indexes[1]);
                    indices.Add(face.Indexes[2]);
                }

                var shapeId = 0;
                var shapeOffset = 0;
                var shapeOffsets = new int[blendModel.BlendFaces.Count];
                var startOffset = offsetDataIndex / 4;
                foreach (var blendFace in blendModel.BlendFaces)
                {
                    shapeOffsets[shapeId++] = shapeOffset;
                    foreach (var converted in indices.Select(index => new VertexBlendShape
                             {
                                 Offset = blendFace.BlendShapes[index].Offset,
                                 BlendShape = blendModel.BlendShape
                             }).Select(twinVertex => twinVertex.GetVector4()))
                    {
                        offsetData[offsetDataIndex++] = (Byte)converted.GetBinaryX();
                        offsetData[offsetDataIndex++] = (Byte)converted.GetBinaryY();
                        offsetData[offsetDataIndex++] = (Byte)converted.GetBinaryZ();
                        offsetData[offsetDataIndex++] = (Byte)converted.GetBinaryW();
                    }
                    
                    shapeOffset += indices.Count;
                }
                if (blendShape == vec3.Ones)
                {
                    blendShape = new vec3(blendModel.BlendShape.X,  blendModel.BlendShape.Y, blendModel.BlendShape.Z);
                }

                if (facesAmount == 0)
                {
                    facesAmount = blendModel.BlendFaces.Count;
                }
                var skin = _meshBuilder.BuildSkinnedVaoFromVertexes(blendModel.Vertexes, blendModel.Faces);
                var shapeBuild = new BlendSkinShapeBuild(shapeOffsets, startOffset);
                var bufferBuild = new BlendSkinModelBufferBuild(skin, shapeBuild, blendShape);
                buffers.Add(new ModelBufferBlendSkin(_renderContext, bufferBuild, _materialFactory, material));
            }
        }
        
        var weights = new float[facesAmount];
        var vertexOffset = new TextureBuffer(_renderContext, offsetData, 256, 256, InternalFormat.Rgba8SNorm, PixelFormat.Rgba,
            PixelType.Byte);
        return new BlendSkinnedMesh(_renderContext, buffers, vertexOffset, blendShape, blendSkin.BlendsAmount, weights);
    }
}