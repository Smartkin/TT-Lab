using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GlmSharp;
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
    }
    
    public Mesh? CreateMesh(LabURI uri)
    {
        if (uri == LabURI.Empty)
        {
            return null;
        }
        
        var assetManager = AssetManager.Get();
        var asset = assetManager.GetAsset(uri);
        Debug.Assert(_constructors.ContainsKey(asset.GetType()), $"Unsupported mesh type {asset.GetType()}");
        return _constructors[asset.GetType()](asset.GetData<AbstractAssetData>());
    }

    private Mesh CreateUntexturedMesh(ModelData data)
    {
        var material = new MaterialData();
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.ON;
        material.Shaders[0].ShaderType = TwinShader.Type.UnlitGlossy;
        material.Shaders[0].TextureId = new LabURI("boat_guy", true);
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
        var material = new MaterialData();
        material.Shaders[0].ShaderType = TwinShader.Type.UnlitGlossy;
        List<ModelBuffer> buffers = [new(_renderContext,
            _meshBuilder.BuildRigidVaoFromVertexes(
                collisionData.Vectors.Select(v => new Vertex(new Vector4(-v.X, v.Y, v.Z, v.W))).ToList(),
                collisionData.Triangles.Select(t => t.Face).ToList(),
                i => CollisionSurface.DefaultColors[collisionData.Triangles[i].SurfaceIndex].GetVector()),
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
                    blendShape = new vec3(-blendModel.BlendShape.X,  blendModel.BlendShape.Y, blendModel.BlendShape.Z);
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