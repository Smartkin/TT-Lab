using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GlmSharp;
using Silk.NET.OpenGL;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Services;
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
        _builtInConstructors.Add(LabURI.Volume, CreateVolume);
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
        
        var material = new MaterialData(null);
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.OFF;
        material.Shaders[0].ShaderType = TwinShader.Type.ColorOnly;
        material.Shaders[0].ABlending = TwinShader.AlphaBlending.ON;
        var buffer = new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(vectors.Select((v, i) => new Vertex(new Vector4(v.x, v.y, v.z, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f))).ToList(), faces), _materialFactory, material);
        return new Mesh(_renderContext, [buffer]);
    }

    // Culled by facing, so unlike the other built in meshes it's wound counter-clockwise seen from outside, like the primitive renderer's box
    private Mesh CreateVolume()
    {
        var box = new List<float>();
        PrimitiveMeshes.AddBox(box);
        var vertexes = box.Chunk(PrimitiveMeshes.VertexFloats)
            .Select(vertex => new Vertex(new Vector4(vertex[0], vertex[1], vertex[2], 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f))).ToList();
        var faces = Enumerable.Range(0, vertexes.Count / 3).Select(face => new IndexedFace { Indexes = [face * 3, face * 3 + 1, face * 3 + 2] }).ToList();
        var material = new MaterialData(null);
        material.Shaders.Add(new LabShader());
        foreach (var (shader, pass) in material.Shaders.Zip([PassService.VolumeInsidesPass, PassService.VolumeOutsidesPass]))
        {
            shader.TxtMapping = TwinShader.TextureMapping.OFF;
            shader.ShaderType = TwinShader.Type.ColorOnly;
            shader.ABlending = TwinShader.AlphaBlending.ON;
            shader.ZValueDrawingMask = TwinShader.ZValueDrawMask.NOT_UPDATE;
            shader.ForcedShaderName = pass;
        }

        var buffer = new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(vertexes, faces), _materialFactory, material);
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
        
        var material = new MaterialData(null);
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

        var material = new MaterialData(null);
        material.Shaders[0].TxtMapping = TwinShader.TextureMapping.OFF;
        material.Shaders[0].ShaderType = TwinShader.Type.ColorOnly;
        material.Shaders[0].ABlending = TwinShader.AlphaBlending.ON;
        var buffer = new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(vectors.Select((v, i) => new Vertex(new Vector4(v.x, v.y, v.z, 1.0f), new Vector4(1.0f, 1.0f, 1.0f, 1.0f), new Vector4(uvs[i].x, uvs[i].y, 0.0f, 0.0f))).ToList(), faces), _materialFactory, material);
        return new Mesh(_renderContext, [buffer]);
    }

    /// <summary>
    /// A sphere of radius 1 standing on the floor with the material on it, what the material editor shows it on. Lit materials double
    /// the vertexes' colors, so their sphere is half as bright
    /// </summary>
    public Mesh CreateMaterialPreview(MaterialData material)
    {
        const int rings = 24;
        const int segments = 48;
        var brightness = material.Shaders.Any(shader => MaterialFactory.IsLit(shader.ShaderType)) ? 0.5f : 1.0f;
        var color = new Vector4(brightness, brightness, brightness, 1.0f);
        var vertexes = new List<Vertex>();
        for (var ring = 0; ring <= rings; ring++)
        {
            var v = ring / (float)rings;
            var polar = v * MathF.PI;
            for (var segment = 0; segment <= segments; segment++)
            {
                var u = segment / (float)segments;
                var azimuth = u * MathF.PI * 2.0f;
                var normal = new vec3(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
                vertexes.Add(new Vertex(new Vector4(normal.x, normal.y + 1.0f, normal.z, 1.0f), color, new Vector4(u, v, 0.0f, 0.0f))
                {
                    Normal = new Vector4(normal.x, normal.y, normal.z, 0.0f)
                });
            }
        }

        var faces = new List<IndexedFace>();
        for (var ring = 0; ring < rings; ring++)
        {
            for (var segment = 0; segment < segments; segment++)
            {
                var corner = ring * (segments + 1) + segment;
                var below = corner + segments + 1;
                faces.Add(new IndexedFace { Indexes = [corner, corner + 1, below] });
                faces.Add(new IndexedFace { Indexes = [corner + 1, below + 1, below] });
            }
        }

        var buffer = new ModelBuffer(_renderContext, _meshBuilder.BuildRigidVaoFromVertexes(vertexes, faces), _materialFactory, material);
        return new Mesh(_renderContext, [buffer]);
    }

    private Mesh CreateUntexturedMesh(ModelData data)
    {
        var material = new MaterialData(null);
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
            if (m == LabURI.Empty)
            {
                return MaterialData.GetEmptyMaterial();
            }
            
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
        var material = new MaterialData(null);
        material.Shaders[0].ShaderType = TwinShader.Type.StandardUnlit;
        List<ModelBuffer> buffers = [new(_renderContext,
            _meshBuilder.BuildRigidVaoFromVertexes(
                collisionData.Vertexes.Select(v => new Vertex(new Vector4(v.X, v.Y, v.Z, v.W))).ToList(),
                collisionData.Triangles.Select(t => t.Face).ToList(),
                i =>
                {
                    var surface = assetManager.GetAsset(collisionData.Triangles[i].Surface);
                    return CollisionSurface.GetEditorColor(surface).GetVector();
                }),
            _materialFactory, material) { EditorShading = true }];
        
        return new CollisionMesh(_renderContext, buffers);
    }

    private SkinnedMesh CreateSkinnedMesh(SkinData skin)
    {
        var assetManager = AssetManager.Get();
        var buffers = skin.SubSkins.Select(ss =>
        {
            var material = ss.Material == LabURI.Empty ? MaterialData.GetEmptyMaterial() : assetManager.GetAssetData<MaterialData>(ss.Material);
            return new ModelBuffer(_renderContext, _meshBuilder.BuildSkinnedVaoFromVertexes(ss.Vertexes, ss.Faces), _materialFactory, material);
        }).ToList();
        
        return new SkinnedMesh(_renderContext, buffers);
    }

    // Every triangle corner gets the offset of its vertex for every shape in a float texture the vertex shader fetches them from
    private BlendSkinnedMesh CreateBlendSkinnedMesh(BlendSkinData blendSkin)
    {
        const int offsetTextureWidth = 1024;
        var assetManager = AssetManager.Get();
        var buffers = new List<ModelBufferBlendSkin>();
        var offsets = new List<float>();
        var shapesAmount = blendSkin.BlendsAmount;
        foreach (var blend in blendSkin.Blends)
        {
            var material = blend.Material == LabURI.Empty ? MaterialData.GetEmptyMaterial() : assetManager.GetAssetData<MaterialData>(blend.Material);
            var corners = blend.Faces.SelectMany(face => face.Indexes!).ToList();
            var shapeStart = offsets.Count / 4;
            var shapeOffsets = new int[shapesAmount];
            for (var shape = 0; shape < shapesAmount; shape++)
            {
                shapeOffsets[shape] = shape * corners.Count;
                var shapeVertexes = shape < blend.ShapeOffsets.Count ? blend.ShapeOffsets[shape] : null;
                foreach (var corner in corners)
                {
                    var offset = shapeVertexes != null ? shapeVertexes[corner] : new Vector4();
                    offsets.AddRange([offset.X, offset.Y, offset.Z, 0]);
                }
            }

            var skin = _meshBuilder.BuildSkinnedVaoFromVertexes(blend.Vertexes, blend.Faces);
            var bufferBuild = new BlendSkinModelBufferBuild(skin, new BlendSkinShapeBuild(shapeOffsets, shapeStart), vec3.Ones);
            buffers.Add(new ModelBufferBlendSkin(_renderContext, bufferBuild, _materialFactory, material));
        }

        var height = Math.Max(1, (offsets.Count / 4 + offsetTextureWidth - 1) / offsetTextureWidth);
        var texels = new float[offsetTextureWidth * height * 4];
        offsets.CopyTo(texels);
        var vertexOffsets = new TextureBuffer(_renderContext, System.Runtime.InteropServices.MemoryMarshal.AsBytes(texels.AsSpan()).ToArray(), offsetTextureWidth, (uint)height,
            InternalFormat.Rgba32f, PixelFormat.Rgba, PixelType.Float);
        return new BlendSkinnedMesh(_renderContext, buffers, vertexOffsets, vec3.Ones, shapesAmount, new float[shapesAmount]);
    }
}
