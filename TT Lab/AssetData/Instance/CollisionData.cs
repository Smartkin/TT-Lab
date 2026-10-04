using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using GlmSharp;
using TT_Lab.AssetData.Code;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
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
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;

namespace TT_Lab.AssetData.Instance;


[ReferencesAssets]
public class CollisionData : AbstractAssetData
{
    public CollisionData(IAsset asset) : base(asset)
    {
        Vertexes = new List<Vector4>();
    }

    public CollisionData(IAsset asset, ITwinCollision collision) : this(asset)
    {
        SetTwinItem(collision);
    }

    public List<CollisionNode> Nodes { get; set; } = new();
    public List<CollisionGroup> Groups { get; set; } = new();
    public List<CollisionTriangle> Triangles { get; set; } = new();
    public List<Vector4> Vertexes { get; set; }

    private CollisionGeometry? _geometry;

    /// <summary>
    /// The vertexes and triangles as they are now. Setting it replaces them, edits set a new one so the history keeps the old
    /// </summary>
    public CollisionGeometry Geometry
    {
        get => _geometry ??= CollisionGeometry.Of(Vertexes, Triangles);
        set
        {
            Vertexes = value.Vertexes.Select(v => new Vector4(v.X, v.Y, v.Z, v.W)).ToList();
            Triangles = value.Triangles.Select(t => new CollisionTriangle { Face = new IndexedFace(t.A, t.B, t.C), Surface = t.Surface }).ToList();
            _geometry = value;
        }
    }

    protected override void Dispose(Boolean disposing)
    {
        _geometry = null;
        Nodes.Clear();
        Groups.Clear();
        Triangles.Clear();
        Vertexes.Clear();
    }

    public const string TlmAssetType = "Collision";
    public const string TlmKind = "collision";

    protected override void SaveInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        file.Root = WriteTlmNode(file);
        file.Save(dataPath);
    }

    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        var file = TlmFile.Load(dataPath);
        ReadTlmNodes(file, file.Root == null ? [] : TlmTreeNode.Of(file.Root).Traverse().Where(node => node.Kind == TlmKind).ToList());
        DisposedValue = false;
    }

    /// <summary>
    /// The collision as a node, the triangles of every surface as their own part
    /// </summary>
    public JsonObject WriteTlmNode(TlmFile file)
    {
        // Vertexes no triangle uses have nowhere else to go
        var used = Triangles.SelectMany(t => t.Face.Indexes!).ToHashSet();
        var unused = Enumerable.Range(0, Vertexes.Count).Where(i => !used.Contains(i)).ToList();
        var node = TlmNodes.Create(TlmKind, "Collision", new JsonObject
        {
            ["UnusedVertexes"] = TlmJson.ToJson(unused),
            ["UnusedPositions"] = TlmJson.ToJson(unused.SelectMany(i => new[] { Vertexes[i].X, Vertexes[i].Y, Vertexes[i].Z }))
        });
        var assetManager = AssetManager.Get();
        var parts = new JsonArray();
        foreach (var surfaceTriangles in Triangles.Select((triangle, index) => (Triangle: triangle, Index: index)).GroupBy(t => t.Triangle.Surface))
        {
            var positions = new List<Single>();
            var vectorIndexes = new List<Int32>();
            var indices = new List<UInt32>();
            var remap = new Dictionary<Int32, Int32>();
            foreach (var (triangle, _) in surfaceTriangles)
            {
                foreach (var index in triangle.Face.Indexes!)
                {
                    if (!remap.TryGetValue(index, out var local))
                    {
                        local = vectorIndexes.Count;
                        remap.Add(index, local);
                        var vector = Vertexes[index];
                        positions.AddRange([vector.X, vector.Y, vector.Z]);
                        vectorIndexes.Add(index);
                    }

                    indices.Add((UInt32)local);
                }
            }

            var surface = assetManager.DoesAssetExist(surfaceTriangles.Key) ? assetManager.GetAsset<CollisionSurface>(surfaceTriangles.Key) : null;
            var color = CollisionSurface.GetEditorColor(surface);
            // Where the triangles and vertexes were in the collision, which the game's tree gets built from
            parts.Add(new JsonObject
            {
                ["surface"] = surfaceTriangles.Key.ToString(),
                ["name"] = surface?.Name ?? "Surface",
                ["color"] = TlmJson.ToJson(new[] { color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, color.A / 255.0f }),
                ["vertices"] = vectorIndexes.Count,
                ["position"] = file.Write(positions),
                ["faces"] = file.Write(indices),
                ["triangles"] = file.Write(surfaceTriangles.Select(t => t.Index).ToList()),
                ["vertexes"] = file.Write(vectorIndexes)
            });
        }

        node["surfaces"] = parts;
        return node;
    }

    /// <summary>
    /// Reads the collision from the nodes. Surfaces are the ones their parts name, or the surface with the part's name
    /// </summary>
    public void ReadTlmNodes(TlmFile file, IEnumerable<TlmTreeNode> nodes)
    {
        _geometry = null;
        Vertexes.Clear();
        Triangles.Clear();
        var assetManager = AssetManager.Get();
        var surfaces = assetManager.GetRelatedAssetsOf<CollisionSurface>(Owner.Package);
        var parts = new List<CollisionPart>();
        var unused = new List<(Int32 Index, System.Numerics.Vector3 Position)>();
        foreach (var node in nodes)
        {
            var transform = node.GetBakedTransform();
            var data = node.Data;
            var unusedIndexes = data.GetInts("UnusedVertexes");
            var unusedPositions = data.GetFloats("UnusedPositions");
            for (var i = 0; i < unusedIndexes.Length && i * 3 + 2 < unusedPositions.Length; i++)
            {
                unused.Add((unusedIndexes[i], Transform(new System.Numerics.Vector3(unusedPositions[i * 3], unusedPositions[i * 3 + 1], unusedPositions[i * 3 + 2]), transform)));
            }

            foreach (var part in node.Json["surfaces"] as JsonArray ?? [])
            {
                if (part is not JsonObject json)
                {
                    continue;
                }

                var surfaceUri = json.GetString("surface") is { } uriText && uriText.StartsWith("res://") && assetManager.DoesAssetExist(new LabURI(uriText))
                    ? new LabURI(uriText)
                    : (surfaces.FirstOrDefault(surface => surface.Name == json.GetString("name")) ?? surfaces.First()).URI;
                var positions = file.Read<Single>(json["position"]);
                var faces = file.Read<UInt32>(json["faces"]);
                parts.Add(new CollisionPart(surfaceUri,
                    Enumerable.Range(0, positions.Length / 3).Select(i => Transform(new System.Numerics.Vector3(positions[i * 3], positions[i * 3 + 1], positions[i * 3 + 2]), transform)).ToList(),
                    Enumerable.Range(0, faces.Length / 3).Select(i => ((Int32)faces[i * 3], (Int32)faces[i * 3 + 1], (Int32)faces[i * 3 + 2])).ToList(),
                    file.Read<Int32>(json["triangles"]),
                    file.Read<Int32>(json["vertexes"])));
            }
        }

        if (RestoreOrder(parts, unused))
        {
            return;
        }

        var vertexIndexes = new Dictionary<(UInt32, UInt32, UInt32), Int32>();
        foreach (var part in parts)
        {
            var indexes = new Int32[part.Positions.Count];
            for (var i = 0; i < part.Positions.Count; i++)
            {
                var position = part.Positions[i];
                var key = (BitConverter.SingleToUInt32Bits(position.X), BitConverter.SingleToUInt32Bits(position.Y), BitConverter.SingleToUInt32Bits(position.Z));
                if (!vertexIndexes.TryGetValue(key, out var index))
                {
                    index = Vertexes.Count;
                    vertexIndexes.Add(key, index);
                    Vertexes.Add(new Vector4(position.X, position.Y, position.Z, 1.0f));
                }

                indexes[i] = index;
            }

            foreach (var (a, b, c) in part.Triangles)
            {
                Triangles.Add(new CollisionTriangle
                {
                    Face = new IndexedFace(indexes[a], indexes[b], indexes[c]),
                    Surface = part.Surface
                });
            }
        }
    }

    private sealed record CollisionPart(LabURI Surface, List<System.Numerics.Vector3> Positions, List<(Int32, Int32, Int32)> Triangles, Int32[] TriangleOrder, Int32[] VertexOrder);

    private static System.Numerics.Vector3 Transform(System.Numerics.Vector3 position, System.Numerics.Matrix4x4? transform)
    {
        return transform != null ? System.Numerics.Vector3.Transform(position, transform.Value) : position;
    }

    // The triangles and vertexes go back where they were when the mesh still has all of them, the game's tree comes out the same then
    private Boolean RestoreOrder(List<CollisionPart> parts, List<(Int32 Index, System.Numerics.Vector3 Position)> unused)
    {
        if (parts.Count == 0 || parts.Any(part => part.TriangleOrder.Length != part.Triangles.Count || part.VertexOrder.Length != part.Positions.Count))
        {
            return false;
        }

        var vectorsAmount = parts.SelectMany(part => part.VertexOrder).Concat(unused.Select(u => u.Index)).DefaultIfEmpty(-1).Max() + 1;
        var vectors = new System.Numerics.Vector3?[vectorsAmount];
        foreach (var (index, position) in parts.SelectMany(part => part.VertexOrder.Zip(part.Positions)).Concat(unused))
        {
            if (index < 0 || vectors[index] != null && vectors[index] != position)
            {
                return false;
            }

            vectors[index] = position;
        }

        var triangles = new CollisionTriangle?[parts.Sum(part => part.Triangles.Count)];
        foreach (var part in parts)
        {
            for (var i = 0; i < part.Triangles.Count; i++)
            {
                var slot = part.TriangleOrder[i];
                var (a, b, c) = part.Triangles[i];
                if (slot < 0 || slot >= triangles.Length || triangles[slot] != null || Math.Max(a, Math.Max(b, c)) >= part.VertexOrder.Length)
                {
                    return false;
                }

                triangles[slot] = new CollisionTriangle
                {
                    Face = new IndexedFace(part.VertexOrder[a], part.VertexOrder[b], part.VertexOrder[c]),
                    Surface = part.Surface
                };
            }
        }

        if (vectors.Any(v => v == null) || triangles.Any(t => t == null))
        {
            return false;
        }

        Vertexes = vectors.Select(v => new Vector4(v!.Value.X, v.Value.Y, v.Value.Z, 1.0f)).ToList();
        Triangles = triangles.Select(t => t!).ToList();
        return true;
    }

    public void RebuildBvh()
    {
        Nodes.Clear();
        Groups.Clear();
        BvhBuilder.BuildBvh(this);
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        _geometry = null;
        var collision = GetTwinItem<ITwinCollision>();
        foreach (var node in collision.Nodes)
        {
            Nodes.Add(new CollisionNode(node));
        }
        foreach (var group in collision.Groups)
        {
            Groups.Add(new CollisionGroup(group));
        }
        var assetManager = AssetManager.Get();
        var surfaces = assetManager.GetRelatedAssetsOf<CollisionSurface>(Owner.Package);
        foreach (var triangle in collision.Triangles)
        {
            Triangles.Add(new CollisionTriangle(triangle, surfaces));
        }
        // Clone the vectors instead of reference copying
        Vertexes = CloneUtils.CloneList(collision.Vertexes);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        // The tree puts the triangles in its own order, the data keeps its order so building it again comes out the same
        var triangles = Triangles;
        RebuildBvh();
        var treeTriangles = Triangles;
        Triangles = triangles;

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(PS2AnyCollisionData.GameVersion);
        writer.Write(Nodes.Count);
        writer.Write(Groups.Count);
        writer.Write(treeTriangles.Count);
        writer.Write(Vertexes.Count);

        foreach (var node in Nodes)
        {
            node.Min.Write(writer);
            writer.Write(node.FirstChild);
            node.Max.Write(writer);
            writer.Write(node.SecondChild);
        }

        foreach (var group in Groups)
        {
            writer.Write(group.Count);
            writer.Write(group.FirstTriangle);
        }

        var assetManager = AssetManager.Get();
        foreach (var tri in treeTriangles)
        {
            var twinTri = new TwinCollisionTriangle()
            {
                Vertex1Index = tri.Face.Indexes![0],
                Vertex2Index = tri.Face.Indexes[1],
                Vertex3Index = tri.Face.Indexes[2],
                SurfaceIndex = (int)assetManager.GetAsset(tri.Surface).ExportTwinID
            };
            twinTri.Write(writer);
        }

        foreach (var vec in Vertexes)
        {
            vec.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateCollision(ms);
    }
}
