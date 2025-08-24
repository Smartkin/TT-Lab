using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using GlmSharp;
using SharpGLTF.Schema2;
using Silk.NET.Maths;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Collision;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Instance;

using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPosition;
using VERTEX_BUILDER = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPosition, SharpGLTF.Geometry.VertexTypes.VertexEmpty, SharpGLTF.Geometry.VertexTypes.VertexEmpty>;

[ReferencesAssets]
public class CollisionData : AbstractAssetData
{
    public CollisionData()
    {
        Vectors = new List<Vector4>();
    }

    public CollisionData(ITwinCollision collision) : this()
    {
        SetTwinItem(collision);
    }

    public List<CollisionTrigger> Triggers { get; set; } = new();
    public List<GroupInformation> Groups { get; set; } = new();
    public List<CollisionTriangle> Triangles { get; set; } = new();
    public List<Vector4> Vectors { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Triggers.Clear();
        Groups.Clear();
        Triangles.Clear();
        Vectors.Clear();
    }

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var scene = new SharpGLTF.Scenes.SceneBuilder("TwinsanityStaticCollision");
        var root = new SharpGLTF.Scenes.NodeBuilder("static_collision_root");
        scene.AddNode(root);
        var mesh = GetMesh(root);
        scene.AddRigidMesh(mesh.Mesh, root);

        var model = scene.ToGltf2();
        model.SaveGLB(dataPath);
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        Vectors.Clear();
        Triangles.Clear();

        var model = ModelRoot.Load(dataPath);
        var indexOffset = 0;
        var assetManager = AssetManager.Get();
        var surfaces = assetManager.GetAllAssetsOf<CollisionSurface>();
        var defaultSurface = surfaces.First();
        foreach (var mesh in model.LogicalMeshes)
        {
            foreach (var prim in mesh.Primitives)
            {
                var columns = prim.GetVertexColumns();
                foreach (var position in columns.Positions)
                {
                    Vectors.Add(position.ToTwin());
                }

                var materialName = prim.Material.Name;
                foreach (var (idx1, idx2, idx3) in prim.GetTriangleIndices())
                {
                    var triangle = new CollisionTriangle();
                    triangle.Face = new IndexedFace(idx1 + indexOffset, idx2 + indexOffset, idx3 + indexOffset);
                    var surfaceUri = surfaces.FirstOrDefault(surf => surf.Name == materialName, defaultSurface).URI;
                    triangle.Surface = surfaceUri;
                    Triangles.Add(triangle);
                }

                indexOffset += columns.Positions.Count;
            }
        }
    }

    public GltfGeometryWrapper GetMesh(SharpGLTF.Scenes.NodeBuilder root)
    {
        var materials = GetMaterials();
        var builder = new SharpGLTF.Geometry.MeshBuilder<VERTEX>();
        foreach (var collisionTriangle in Triangles)
        {
            var v1 = Vectors[collisionTriangle.Face.Indexes![0]];
            var v2 = Vectors[collisionTriangle.Face.Indexes[1]];
            var v3 = Vectors[collisionTriangle.Face.Indexes[2]];
            var primitive = builder.UsePrimitive(materials[collisionTriangle.Surface]);
            primitive.AddTriangle(new VERTEX_BUILDER(new VERTEX(v1.X, v1.Y, v1.Z)),
                new VERTEX_BUILDER(new VERTEX(v2.X, v2.Y, v2.Z)),
                new VERTEX_BUILDER(new VERTEX(v3.X, v3.Y, v3.Z)));
        }
        return new GltfGeometryWrapper(builder, [(root, Matrix4x4.Identity)]);
    }

    public void RebuildBvh()
    {
        Triggers.Clear();
        Groups.Clear();
        BvhBuilder.BuildBvh(this);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var collision = GetTwinItem<ITwinCollision>();
        foreach (var trigger in collision.Triggers)
        {
            Triggers.Add(new CollisionTrigger(trigger));
        }
        foreach (var group in collision.Groups)
        {
            Groups.Add(new GroupInformation(group));
        }
        var assetManager = AssetManager.Get();
        var surfaces = assetManager.GetAllAssetsOf<CollisionSurface>();
        foreach (var triangle in collision.Triangles)
        {
            Triangles.Add(new CollisionTriangle(triangle, surfaces));
        }
        // Clone the vectors instead of reference copying
        Vectors = CloneUtils.CloneList(collision.Vectors);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        RebuildBvh();
        
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(0xBB9); // Collision header
        writer.Write(Triggers.Count);
        writer.Write(Groups.Count);
        writer.Write(Triangles.Count);
        writer.Write(Vectors.Count);

        foreach (var trigger in Triggers)
        {
            trigger.V1.Write(writer);
            writer.Write(trigger.MinTriggerIndex);
            trigger.V2.Write(writer);
            writer.Write(trigger.MaxTriggerIndex);
        }

        foreach (var group in Groups)
        {
            writer.Write(group.Size);
            writer.Write(group.Offset);
        }

        var assetManager = AssetManager.Get();
        foreach (var tri in Triangles)
        {
            var twinTri = new TwinCollisionTriangle()
            {
                Vector1Index = tri.Face.Indexes![0],
                Vector2Index = tri.Face.Indexes[1],
                Vector3Index = tri.Face.Indexes[2],
                SurfaceIndex = (int)assetManager.GetAsset(tri.Surface).ID
            };
            twinTri.Write(writer);
        }

        foreach (var vec in Vectors)
        {
            vec.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateCollision(ms);
    }

    private Dictionary<LabURI, SharpGLTF.Materials.MaterialBuilder> GetMaterials()
    {
        var result = new Dictionary<LabURI, SharpGLTF.Materials.MaterialBuilder>();
        var assetManager = AssetManager.Get();
        var surfaces = assetManager.GetAllAssetsOf<CollisionSurface>();
        foreach (var surface in surfaces)
        {
            var surfColor = (Color)surface.Parameters["editor_surface_color"]!;
            var surfaceMaterial = new SharpGLTF.Materials.MaterialBuilder().WithDoubleSide(true)
                .WithBaseColor(new System.Numerics.Vector4(surfColor.R / 255.0f, surfColor.G / 255.0f, surfColor.B / 255.0f, surfColor.A / 255.0f));
            surfaceMaterial.Name = surface.Name;
            if (surfColor.A < 255)
            {
                surfaceMaterial.WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND);
            }
            result.Add(surface.URI, surfaceMaterial);
        }
        return result;
    }
}