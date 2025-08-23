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
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common.Collision;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Instance;

using VERTEX = SharpGLTF.Geometry.VertexTypes.VertexPosition;
using VERTEX_BUILDER = SharpGLTF.Geometry.VertexBuilder<SharpGLTF.Geometry.VertexTypes.VertexPosition, SharpGLTF.Geometry.VertexTypes.VertexEmpty, SharpGLTF.Geometry.VertexTypes.VertexEmpty>;

// TODO: Add [ReferencesAssets] attribute
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

    [JsonProperty(Required = Required.Always)]
    public UInt32 UnkInt { get; set; }
    
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
        base.SaveInternal(dataPath, settings);
        
        var scene = new SharpGLTF.Scenes.SceneBuilder("TwinsanityStaticCollision");
        var root = new SharpGLTF.Scenes.NodeBuilder("static_collision_root");
        scene.AddNode(root);
        var mesh = GetMesh(root);
        scene.AddRigidMesh(mesh.Mesh, root);

        var model = scene.ToGltf2();
        model.SaveGLB(dataPath + ".glb");
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        base.LoadInternal(dataPath, settings);
        
        Vectors.Clear();
        Triangles.Clear();

        var model = ModelRoot.Load(dataPath + ".glb");
        var mesh = model.LogicalMeshes[0];
        var indexOffset = 0;
        foreach (var prim in mesh.Primitives)
        {
            var columns = prim.GetVertexColumns();
            foreach (var position in columns.Positions)
            {
                Vectors.Add(position.ToTwin());
            }
            
            foreach (var (idx1, idx2, idx3) in prim.GetTriangleIndices())
            {
                var triangle = new CollisionTriangle();
                triangle.Face = new IndexedFace(idx1 + indexOffset, idx2 + indexOffset, idx3 + indexOffset);
                if (!Enum.TryParse(prim.Material.Name, out Enums.SurfaceType surfaceType))
                {
                    surfaceType = Enums.SurfaceType.SURF_DEFAULT;
                }
                triangle.SurfaceIndex = (int)surfaceType;
                Triangles.Add(triangle);
            }

            indexOffset += columns.Positions.Count;
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
            var primitive = builder.UsePrimitive(materials[collisionTriangle.SurfaceIndex]);
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
        ITwinCollision collision = GetTwinItem<ITwinCollision>();
        UnkInt = collision.UnkInt;
        foreach (var trigger in collision.Triggers)
        {
            Triggers.Add(new CollisionTrigger(trigger));
        }
        foreach (var group in collision.Groups)
        {
            Groups.Add(new GroupInformation(group));
        }
        foreach (var triangle in collision.Triangles)
        {
            Triangles.Add(new CollisionTriangle(triangle));
        }
        // Clone the vectors instead of reference copying
        Vectors = CloneUtils.CloneList(collision.Vectors);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        RebuildBvh();
        
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(UnkInt);
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

        foreach (var tri in Triangles)
        {
            var twinTri = new TwinCollisionTriangle()
            {
                Vector1Index = tri.Face.Indexes![0],
                Vector2Index = tri.Face.Indexes[1],
                Vector3Index = tri.Face.Indexes[2],
                SurfaceIndex = tri.SurfaceIndex
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

    private List<SharpGLTF.Materials.MaterialBuilder> GetMaterials()
    {
        var result = new List<SharpGLTF.Materials.MaterialBuilder>();
        var surfIndex = 0;
        foreach (var defaultColor in CollisionSurface.DefaultColors)
        {
            var surfaceMaterial = new SharpGLTF.Materials.MaterialBuilder().WithDoubleSide(true)
                .WithBaseColor(new System.Numerics.Vector4(defaultColor.R / 255.0f, defaultColor.G / 255.0f, defaultColor.B / 255.0f, defaultColor.A / 255.0f));
            surfaceMaterial.Name = ((Enums.SurfaceType)surfIndex).ToString();
            if (defaultColor.A < 255)
            {
                surfaceMaterial.WithAlpha(SharpGLTF.Materials.AlphaMode.BLEND);
            }
            result.Add(surfaceMaterial);
            surfIndex++;
        }
        return result;
    }
}