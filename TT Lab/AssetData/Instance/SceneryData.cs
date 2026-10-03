using GlmSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance.DynamicScenery;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Lighting;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using Material = TT_Lab.Assets.Graphics.Material;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Vector3 = System.Numerics.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Instance;

public class SceneryData : AbstractAssetData
{
    private static readonly Dictionary<ITwinScenery.SceneryType, Type> ScIndexToType = new();

    static SceneryData()
    {
        ScIndexToType.Add(ITwinScenery.SceneryType.Root, typeof(SceneryRootData));
        ScIndexToType.Add(ITwinScenery.SceneryType.Leaf, typeof(SceneryLeafData));
        ScIndexToType.Add(ITwinScenery.SceneryType.Node, typeof(SceneryNodeData));
    }

    [System.Text.Json.Serialization.JsonConstructor]
    private SceneryData() : base(null) { }

    public SceneryData(IAsset asset) : base(asset)
    {
        SkydomeID = LabURI.Empty;
        Collision = LabURI.Empty;
        DynamicScenery = LabURI.Empty;
        HasLighting = false;
        AmbientLights = new List<AmbientLight>();
        DirectionalLights = new List<DirectionalLight>();
        PointLights = new List<PointLight>();
        SpotLights = new List<SpotLight>();
        Sceneries = new List<SceneryBaseData>();
    }

    public SceneryData(IAsset asset, ITwinScenery scenery) : this(asset)
    {
        SetTwinItem(scenery);
    }
    
    public LabURI SkydomeID { get; set; }
    
    public LabURI DynamicScenery { get; set; }
    
    public LabURI Collision { get; set; }
    
    public UInt32 FogColor { get; set; }
    
    public Byte UnusedByte { get; set; }

    [Editable(Hint = "The box the game keeps the chunk's objects in, its scenery tree's root: an object outside of it has nothing under it, so it has to hold every place objects go. The viewport's Scenery bounds layer shows it, moved and sized by the handle on its top")]
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public SceneryBounds Bounds => new(this);

    [Editable(Hint = "Whether the scenery has lights. The game lights the objects whose materials are lit by them and draws them black without any")]
    public Boolean HasLighting { get; set; }

    /// <summary>
    /// Index and kind of every light in the order the scenery lists them, empty when they're listed kind by kind. Only the Xbox version
    /// lists them in other orders, which its tree nodes' lights refer to
    /// </summary>
    public List<Int32> LightOrder { get; set; } = [];

    [Editable(Caption = "Ambient Lights", Hint = "Light every object gets wherever it is, every light's color times its intensity added up")]
    [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Ambient")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxLights)]
    public List<AmbientLight> AmbientLights { get; set; }

    [Editable(Caption = "Directional Lights", Hint = "Light from one direction, the same everywhere. Of the directional, point and spot lights the game lights an object by the 3 strongest where it is")]
    [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Directional")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxLights)]
    public List<DirectionalLight> DirectionalLights { get; set; }

    [Editable(Caption = "Point Lights", Hint = "Light from a point, weaker further away")]
    [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Point")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxLights)]
    public List<PointLight> PointLights { get; set; }

    [Editable(Caption = "Spot Lights", Hint = "Light from a point shining within a cone, weaker further away")]
    [EditorParam(DocumentCollectionViewModel.ItemCaptionPrefix, "Spot")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxLights)]
    public List<SpotLight> SpotLights { get; set; }
    
    public List<SceneryBaseData> Sceneries { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        var assetManager = AssetManager.Get();
        if (DynamicScenery != LabURI.Empty)
        {
            assetManager.GetAsset(DynamicScenery).Delete();
        }

        if (Collision != LabURI.Empty)
        {
            assetManager.GetAsset(Collision).Delete();
        }

        AmbientLights.Clear();
        DirectionalLights.Clear();
        PointLights.Clear();
        SpotLights.Clear();
        Sceneries.Clear();
    }
    
    public const string TlmAssetType = "Scenery";
    public const string TlmKind = "scenery";
    public const string TreeNodeKind = "tree_node";
    public const string MeshInstanceKind = "scenery_mesh";
    public const string LodInstanceKind = "scenery_lod";
    public const string LodMeshKind = "lod_mesh";
    public const string LightsKind = "lights";
    public const string AmbientLightKind = "ambient_light";
    public const string DirectionalLightKind = "directional_light";
    public const string PointLightKind = "point_light";
    public const string SpotLightKind = "spot_light";

    /// <summary>
    /// Writes the scenery with the tree the game culls it by as a hierarchy of nodes, every placed mesh and LOD under the node that
    /// culls it. Placed meshes that leave their node's box or aren't under any node get the node they're in when it's read
    /// </summary>
    internal TlmFile WriteTlm()
    {
        var assetManager = AssetManager.Get();
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var materials = new TlmMaterials(file);
        var (parents, slots) = GetTreeLinks();
        var rootData = new JsonObject
        {
            ["FogColor"] = (Int32)FogColor,
            ["UnusedByte"] = (Int32)UnusedByte,
            ["HasLighting"] = HasLighting
        };
        if (LightOrder.Count > 0)
        {
            rootData["LightOrder"] = TlmJson.ToJson(LightOrder.ToArray());
        }

        var root = TlmNodes.Create(TlmKind, Owner.Name, rootData);
        var treeNodes = new JsonObject[Sceneries.Count];
        for (var treeIndex = 0; treeIndex < Sceneries.Count; treeIndex++)
        {
            var treeNode = Sceneries[treeIndex];
            var parent = parents[treeIndex];
            var kind = treeNode.GetSceneryType();
            var sceneryNode = TlmNodes.Create(TreeNodeKind, treeIndex == 0 ? "Scenery Tree" : $"{kind} {treeIndex}", WriteTreeNode(treeNode, slots[treeIndex]));
            (parent < 0 ? root : treeNodes[parent]).AddChild(sceneryNode);
            treeNodes[treeIndex] = sceneryNode;
            for (var i = 0; i < treeNode.MeshIDs.Count; i++)
            {
                var node = sceneryNode.AddChild(TlmNodes.Create(MeshInstanceKind, $"Mesh {treeIndex}.{i}", new JsonObject
                {
                    ["Order"] = i,
                    ["Matrix"] = TlmJson.ToJson(TlmNodes.ToArray(treeNode.MeshModelMatrices[i].ToSystem())),
                    ["BoundingBox"] = WriteBoundingBox(treeNode.BoundingBoxes.ElementAtOrDefault(i))
                }));
                node.SetTransform(treeNode.MeshModelMatrices[i].ToSystem());
                if (assetManager.DoesAssetExist(treeNode.MeshIDs[i]))
                {
                    node[TlmNodes.MeshKey] = assetManager.GetAssetData<MeshData>(treeNode.MeshIDs[i]).WriteTlmMesh(file, materials);
                }
            }

            for (var i = 0; i < treeNode.LodIDs.Count; i++)
            {
                var lodData = assetManager.GetAssetData<LodModelData>(treeNode.LodIDs[i]);
                var node = sceneryNode.AddChild(TlmNodes.Create(LodInstanceKind, $"LOD {treeIndex}.{i}", new JsonObject
                {
                    ["Order"] = i,
                    ["Matrix"] = TlmJson.ToJson(TlmNodes.ToArray(treeNode.LodModelMatrices[i].ToSystem())),
                    ["BoundingBox"] = WriteBoundingBox(treeNode.BoundingBoxes.ElementAtOrDefault(treeNode.MeshIDs.Count + i)),
                    ["LodType"] = lodData.Type.ToString(),
                    ["MinDrawDistance"] = lodData.MinDrawDistance,
                    ["MaxDrawDistance"] = lodData.MaxDrawDistance,
                    ["ModelsDrawDistances"] = TlmJson.ToJson(lodData.ModelsDrawDistances)
                }));
                node.SetTransform(treeNode.LodModelMatrices[i].ToSystem());
                for (var level = 0; level < lodData.Meshes.Count; level++)
                {
                    var levelNode = node.AddChild(TlmNodes.Create(LodMeshKind, $"LOD {treeIndex}.{i} Level {level}", new JsonObject { ["Level"] = level }));
                    levelNode[TlmNodes.MeshKey] = assetManager.GetAssetData<MeshData>(lodData.Meshes[level]).WriteTlmMesh(file, materials);
                }
            }
        }

        if (HasLighting)
        {
            var lights = root.AddChild(TlmNodes.Create(LightsKind, "Lights"));
            WriteLights(lights, AmbientLights, AmbientLightKind, (light, json) => { });
            WriteLights(lights, DirectionalLights, DirectionalLightKind, (light, json) =>
            {
                json["Leftover"] = (Int32)light.Leftover;
                json["Direction"] = TlmJson.ToJson(light.Direction);
            });
            WriteLights(lights, PointLights, PointLightKind, (light, json) => json["AttenuationPower"] = (Int32)light.AttenuationPower);
            WriteLights(lights, SpotLights, SpotLightKind, (light, json) =>
            {
                json["Direction"] = TlmJson.ToJson(light.Direction);
                json["ConeAngle"] = TlmJson.ToJson(light.ConeAngle);
                json["FalloffAngle"] = TlmJson.ToJson(light.FalloffAngle);
                json["InnerConeCosine"] = light.InnerConeCosine;
                json["OuterConeCosine"] = light.OuterConeCosine;
                json["AttenuationPower"] = (Int32)light.AttenuationPower;
                json["SpotExponent"] = (Int32)light.SpotExponent;
            });
        }

        if (Collision != LabURI.Empty && assetManager.DoesAssetExist(Collision))
        {
            root.AddChild(assetManager.GetAssetData<CollisionData>(Collision).WriteTlmNode(file));
        }

        if (DynamicScenery != LabURI.Empty && assetManager.DoesAssetExist(DynamicScenery))
        {
            root.AddChild(assetManager.GetAssetData<DynamicSceneryData>(DynamicScenery).WriteTlmNode(file, materials));
        }

        file.Root = root;
        return file;
    }

    // Parent and child slot of every node of the tree, which is stored with every node's children right after it
    private (Int32[] Parents, Int32[] Slots) GetTreeLinks()
    {
        var parents = Enumerable.Repeat(-1, Sceneries.Count).ToArray();
        var slots = new Int32[Sceneries.Count];
        var next = 1;
        Walk(0);
        return (parents, slots);

        void Walk(Int32 index)
        {
            if (index >= Sceneries.Count || Sceneries[index] is not SceneryNodeData node)
            {
                return;
            }

            for (var slot = 0; slot < node.SceneryTypes.Length && next < Sceneries.Count; slot++)
            {
                if (node.SceneryTypes[slot] != ITwinScenery.SceneryType.Node && node.SceneryTypes[slot] != ITwinScenery.SceneryType.Leaf)
                {
                    continue;
                }

                var child = next++;
                parents[child] = index;
                slots[child] = slot;
                Walk(child);
            }
        }
    }

    private static JsonObject WriteTreeNode(SceneryBaseData node, Int32 slot)
    {
        var json = new JsonObject
        {
            ["Kind"] = node.GetSceneryType().ToString(),
            ["Slot"] = slot,
            ["BoundsCenter"] = TlmJson.ToJson(node.BoundsCenter),
            ["BoundsMin"] = TlmJson.ToJson(node.BoundsMin),
            ["BoundsMax"] = TlmJson.ToJson(node.BoundsMax),
            ["BoundsHalfSize"] = TlmJson.ToJson(node.BoundsHalfSize),
            ["LightsEnabler"] = TlmJson.ToJson(node.LightsEnabler)
        };
        if (node is SceneryNodeData treeNode)
        {
            json["SceneryTypes"] = TlmJson.ToJson(treeNode.SceneryTypes.Select(t => (Int32)t));
        }

        if (node is SceneryRootData root)
        {
            json["TreeDepth"] = TlmJson.ToJson(root.TreeDepth);
        }

        return json;
    }

    private static JsonArray WriteBoundingBox(BoundingBox? box)
    {
        return box == null ? [] : TlmJson.ToJson(new[] { box.V1.X, box.V1.Y, box.V1.Z, box.V1.W, box.V2.X, box.V2.Y, box.V2.Z, box.V2.W });
    }

    private static void WriteLights<T>(JsonObject parent, List<T> lights, string kind, Action<T, JsonObject> writeExtra) where T : Light
    {
        for (var i = 0; i < lights.Count; i++)
        {
            var light = lights[i];
            var json = new JsonObject
            {
                ["Order"] = i,
                ["PositionW"] = light.Position.W,
                ["Enabled"] = light.Enabled,
                ["Intensity"] = light.Intensity,
                ["Color"] = TlmJson.ToJson(light.Color),
                ["BoundsMin"] = TlmJson.ToJson(light.BoundsMin),
                ["BoundsMax"] = TlmJson.ToJson(light.BoundsMax)
            };
            writeExtra(light, json);
            var node = parent.AddChild(TlmNodes.Create(kind, $"{kind} {i}", json));
            var direction = light switch
            {
                DirectionalLight directional => directional.Direction,
                SpotLight spot => spot.Direction,
                _ => null
            };
            var rotation = direction == null ? Quaternion.Identity : RotationTowards(new Vector3(direction.X, direction.Y, direction.Z));
            node.SetTransform(Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(light.Position.X, light.Position.Y, light.Position.Z));
        }
    }

    // A light's node points its Z axis along the light's direction (a directional light's points at where the light comes from, a
    // spot light's at where it shines), which Blender shows as the empty's arrow
    private static Quaternion RotationTowards(Vector3 direction)
    {
        if (direction.LengthSquared() < 1e-12f)
        {
            return Quaternion.Identity;
        }

        direction = Vector3.Normalize(direction);
        var dot = Vector3.Dot(Vector3.UnitZ, direction);
        if (dot > 0.999999f)
        {
            return Quaternion.Identity;
        }

        if (dot < -0.999999f)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        }

        var axis = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, direction));
        return Quaternion.CreateFromAxisAngle(axis, MathF.Acos(Math.Clamp(dot, -1, 1)));
    }

    // The stored direction is kept while the node still points along it, so unedited lights round-trip exactly
    private static Vector4 DirectionOf(TlmTreeNode node, JsonObject json)
    {
        Matrix4x4.Decompose(node.WorldMatrix, out _, out var rotation, out _);
        var pointed = Vector3.Transform(Vector3.UnitZ, rotation);
        if (json["Direction"] != null)
        {
            var stored = json.GetVector4("Direction");
            var storedDirection = new Vector3(stored.X, stored.Y, stored.Z);
            // A direction of no length (a light made in Blender, whose node the add-on writes with none) stays while the node isn't
            // turned, which is how a game file's would be written
            var along = storedDirection.LengthSquared() < 1e-12f ? Vector3.UnitZ : Vector3.Normalize(storedDirection);
            if (Vector3.Dot(along, pointed) > 0.99999f)
            {
                return stored;
            }
        }

        return new Vector4(pointed.X, pointed.Y, pointed.Z, 0);
    }

    /// <returns>Whether the file has to be written again: materials made in Blender became the project's</returns>
    internal Boolean ReadTlm(TlmFile file)
    {
        var root = TlmTreeNode.Of(file.Root ?? new JsonObject());
        var materials = new TlmMaterials(file, Owner);
        var data = root.Data;
        FogColor = (UInt32)data.GetInt("FogColor", (Int32)FogColor);
        UnusedByte = (Byte)data.GetInt("UnusedByte", UnusedByte);
        LightOrder = data.GetInts("LightOrder").ToList();
        var nodes = root.Traverse().Skip(1).ToList();
        var treeRoot = nodes.Where(node => node.Kind == TreeNodeKind).FirstOrDefault(node => FindAncestor(node.Parent, other => other.Kind == TreeNodeKind) == null);
        var rootKept = treeRoot?.Data["BoundsMin"] != null && treeRoot.Data["BoundsMax"] != null;
        var treeIndexes = ReadTree(nodes);
        ReadLights(nodes);
        HasLighting = data.GetBool("HasLighting", AmbientLights.Count + DirectionalLights.Count + PointLights.Count + SpotLights.Count > 0);

        var collisionNodes = nodes.Where(n => n.Kind == CollisionData.TlmKind).ToList();
        var collision = new Assets.Instance.Collision
        {
            Package = Owner.Package,
            Chunk = Owner.Chunk,
            InvariantName = $"{Owner.Chunk}_COLLISION",
            Alias = "Collision",
            IsInternal = true,
            InternalOwner = Owner
        };
        var collisionData = new CollisionData(collision);
        collisionData.ReadTlmNodes(file, collisionNodes);
        collision.SetData(collisionData);
        AssetManager.Get().AddAsset(collision);
        Collision = collision.URI;

        var dynamicModels = nodes.Where(n => n.Kind == DynamicSceneryModelData.TlmKind).ToList();
        if (dynamicModels.Count > 0 || nodes.Any(n => n.Kind == DynamicSceneryData.TlmKind))
        {
            var dynamicScenery = new Assets.Instance.DynamicScenery
            {
                Package = Owner.Package,
                Chunk = Owner.Chunk,
                InvariantName = $"{Owner.Chunk}_DYNAMIC_SCENERY",
                Alias = "Dynamic Scenery",
                IsInternal = true,
                InternalOwner = Owner
            };
            var dynamicSceneryData = new DynamicSceneryData(dynamicScenery);
            dynamicSceneryData.ReadTlmNodes(file, materials, dynamicModels);
            dynamicScenery.SetData(dynamicSceneryData);
            AssetManager.Get().AddAsset(dynamicScenery);
            DynamicScenery = dynamicScenery.URI;
        }
        else
        {
            DynamicScenery = LabURI.Empty;
        }

        ReadInstances(file, materials, nodes, collisionNodes, dynamicModels, treeIndexes);
        FixEmptyTreeNodes();
        WidenRoot(rootKept, collisionData);
        EnableEveryLight();
        return materials.AddedToProject;
    }

    // The root holds the chunk's objects (SceneryBounds): one the file left to what got placed in it (made in Blender) or one without room
    // (the flat ground's new chunks had) gets a box like the game's around the collision and what's placed
    private void WidenRoot(Boolean kept, CollisionData collision)
    {
        if (Sceneries.FirstOrDefault() is not { } root || (kept && SceneryBounds.HoldsAnything(root)))
        {
            return;
        }

        var min = new vec3(Single.PositiveInfinity);
        var max = new vec3(Single.NegativeInfinity);
        foreach (var vertex in UsedVertexes(collision))
        {
            min = vec3.Min(min, new vec3(vertex.X, vertex.Y, vertex.Z));
            max = vec3.Max(max, new vec3(vertex.X, vertex.Y, vertex.Z));
        }

        if (Single.IsFinite(root.BoundsMin.X) && Single.IsFinite(root.BoundsMax.X))
        {
            min = vec3.Min(min, new vec3(root.BoundsMin.X, root.BoundsMin.Y, root.BoundsMin.Z));
            max = vec3.Max(max, new vec3(root.BoundsMax.X, root.BoundsMax.Y, root.BoundsMax.Z));
        }

        SceneryBounds.Widen(root, min, max);
    }

    // The tools left vertexes no triangle uses in some collisions
    private static IEnumerable<Vector4> UsedVertexes(CollisionData collision)
    {
        return collision.Triangles.SelectMany(triangle => triangle.Face.Indexes ?? []).Distinct()
            .Where(index => index >= 0 && index < collision.Vertexes.Count)
            .Select(index => collision.Vertexes[index]);
    }

    // The tree is made of the nodes marked as tree nodes, each one a child of the tree node above it. Nodes the game's tree can't
    // hold (a second root, a ninth child) are left out, what's under them gets placed like anything outside of the tree
    private Dictionary<TlmTreeNode, Int32> ReadTree(List<TlmTreeNode> nodes)
    {
        Sceneries = [];
        var indexes = new Dictionary<TlmTreeNode, Int32>();
        var treeNodes = nodes.Where(node => node.Kind == TreeNodeKind).ToList();
        var isTreeNode = treeNodes.ToHashSet();
        var root = treeNodes.FirstOrDefault(node => FindAncestor(node.Parent, isTreeNode.Contains) == null);
        if (root == null)
        {
            Sceneries.Add(CreateTreeNode(null, new JsonObject(), ITwinScenery.SceneryType.Root, new Dictionary<Int32, ITwinScenery.SceneryType>()));
            return indexes;
        }

        var children = treeNodes.Where(node => node != root)
            .GroupBy(node => FindAncestor(node.Parent, isTreeNode.Contains))
            .Where(group => group.Key != null)
            .ToDictionary(group => group.Key!, group => AssignSlots(group.Key!, group.ToList()));
        Add(root, true);
        return indexes;

        void Add(TlmTreeNode node, Boolean isRoot)
        {
            var nodeChildren = children.GetValueOrDefault(node) ?? [];
            var childKinds = nodeChildren.ToDictionary(child => child.Value, child => KindOf(child.Key));
            indexes[node] = Sceneries.Count;
            Sceneries.Add(CreateTreeNode(node, node.Data, isRoot ? ITwinScenery.SceneryType.Root : KindOf(node), childKinds));
            foreach (var child in nodeChildren.OrderBy(child => child.Value))
            {
                Add(child.Key, false);
            }
        }

        ITwinScenery.SceneryType KindOf(TlmTreeNode node)
        {
            return children.GetValueOrDefault(node)?.Count > 0 || node.Data.GetEnum("Kind", ITwinScenery.SceneryType.Leaf) == ITwinScenery.SceneryType.Node
                ? ITwinScenery.SceneryType.Node
                : ITwinScenery.SceneryType.Leaf;
        }
    }

    private static TlmTreeNode? FindAncestor(TlmTreeNode? node, Func<TlmTreeNode, Boolean> predicate)
    {
        while (node != null && !predicate(node))
        {
            node = node.Parent;
        }

        return node;
    }

    // Children keep the slot they had, the others take the one of the octant they're in or the first free one
    private static Dictionary<TlmTreeNode, Int32> AssignSlots(TlmTreeNode parent, List<TlmTreeNode> children)
    {
        var slots = new Dictionary<TlmTreeNode, Int32>();
        var used = new Boolean[8];
        foreach (var child in children)
        {
            var slot = child.Data.GetInt("Slot", -1);
            if (slot is >= 0 and < 8 && !used[slot])
            {
                used[slot] = true;
                slots[child] = slot;
            }
        }

        var parentCenter = parent.Data.GetFloats("BoundsCenter");
        foreach (var child in children.Where(child => !slots.ContainsKey(child)))
        {
            var center = child.Data.GetFloats("BoundsCenter");
            var octant = parentCenter.Length >= 3 && center.Length >= 3
                ? (center[0] >= parentCenter[0] ? 1 : 0) | (center[1] >= parentCenter[1] ? 2 : 0) | (center[2] >= parentCenter[2] ? 4 : 0)
                : -1;
            var slot = octant >= 0 && !used[octant] ? octant : Array.IndexOf(used, false);
            if (slot < 0)
            {
                continue;
            }

            used[slot] = true;
            slots[child] = slot;
        }

        return slots;
    }

    private static SceneryBaseData CreateTreeNode(TlmTreeNode? node, JsonObject json, ITwinScenery.SceneryType kind, Dictionary<Int32, ITwinScenery.SceneryType> childKinds)
    {
        SceneryBaseData data = kind switch
        {
            ITwinScenery.SceneryType.Root => new SceneryRootData { TreeDepth = json.GetUInt("TreeDepth") },
            ITwinScenery.SceneryType.Node => new SceneryNodeData(),
            _ => new SceneryLeafData()
        };
        if (json["BoundsMin"] != null && json["BoundsMax"] != null)
        {
            data.BoundsCenter = json.GetVector4("BoundsCenter");
            data.BoundsMin = json.GetVector4("BoundsMin");
            data.BoundsMax = json.GetVector4("BoundsMax");
            data.BoundsHalfSize = json.GetVector4("BoundsHalfSize");
            var world = node?.WorldMatrix ?? Matrix4x4.Identity;
            if (!TlmNodes.IsIdentity(world))
            {
                // The node was moved with everything under it, its box goes along
                var corners = Corners(new Vector3(data.BoundsMin.X, data.BoundsMin.Y, data.BoundsMin.Z), new Vector3(data.BoundsMax.X, data.BoundsMax.Y, data.BoundsMax.Z))
                    .Select(corner => Vector3.Transform(corner, world)).ToList();
                SetTreeBounds(data, corners.Aggregate(Vector3.Min), corners.Aggregate(Vector3.Max));
            }
        }
        else
        {
            // Nodes made outside of TT Lab get the bounds of what gets placed in them
            data.BoundsCenter = new Vector4(0, 0, 0, 0);
            data.BoundsMin = new Vector4(Single.PositiveInfinity, Single.PositiveInfinity, Single.PositiveInfinity, 1);
            data.BoundsMax = new Vector4(Single.NegativeInfinity, Single.NegativeInfinity, Single.NegativeInfinity, 1);
            data.BoundsHalfSize = new Vector4(0, 0, 0, 0);
        }

        var lights = json.GetBools("LightsEnabler");
        data.LightsEnabler = lights.Length == 0 ? Enumerable.Repeat(true, 128).ToArray() : new Boolean[128];
        Array.Copy(lights, data.LightsEnabler, Math.Min(lights.Length, 128));
        if (data is SceneryNodeData treeNode)
        {
            var types = json.GetInts("SceneryTypes");
            treeNode.SceneryTypes = Enumerable.Range(0, 8).Select(slot => childKinds.TryGetValue(slot, out var childKind)
                ? childKind
                : slot < types.Length && types[slot] != (Int32)ITwinScenery.SceneryType.Node && types[slot] != (Int32)ITwinScenery.SceneryType.Leaf
                    ? (ITwinScenery.SceneryType)types[slot]
                    : ITwinScenery.SceneryType.None).ToArray();
        }

        data.MeshIDs = [];
        data.LodIDs = [];
        data.MeshModelMatrices = [];
        data.LodModelMatrices = [];
        data.BoundingBoxes = [];
        return data;
    }

    // Nodes that got nothing placed in them take a box at their parent's center
    private void FixEmptyTreeNodes()
    {
        var (parents, _) = GetTreeLinks();
        for (var index = 0; index < Sceneries.Count; index++)
        {
            var node = Sceneries[index];
            if (Single.IsFinite(node.BoundsMin.X))
            {
                continue;
            }

            var center = parents[index] >= 0 ? new Vector3(Sceneries[parents[index]].BoundsCenter.X, Sceneries[parents[index]].BoundsCenter.Y, Sceneries[parents[index]].BoundsCenter.Z) : Vector3.Zero;
            SetTreeBounds(node, center, center);
        }
    }

    private static void SetTreeBounds(SceneryBaseData node, Vector3 min, Vector3 max)
    {
        var half = (max - min) / 2;
        var center = (max + min) / 2;
        node.BoundsMin = new Vector4(min.X, min.Y, min.Z, node.BoundsMin.W);
        node.BoundsMax = new Vector4(max.X, max.Y, max.Z, node.BoundsMax.W);
        node.BoundsHalfSize = new Vector4(half.X, half.Y, half.Z, node.BoundsHalfSize.W);
        node.BoundsCenter = new Vector4(center.X, center.Y, center.Z, half.Length());
    }

    private static Vector3[] Corners(Vector3 min, Vector3 max)
    {
        return
        [
            new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, min.Y, max.Z),
            new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z)
        ];
    }

    private void ReadLights(List<TlmTreeNode> nodes)
    {
        AmbientLights = ReadLights(nodes, AmbientLightKind, (node, json) => new AmbientLight());
        PointLights = ReadLights(nodes, PointLightKind, (node, json) => new PointLight { AttenuationPower = (Int16)json.GetInt("AttenuationPower") });
        DirectionalLights = ReadLights(nodes, DirectionalLightKind, (node, json) => new DirectionalLight
        {
            Leftover = (Int16)json.GetInt("Leftover"),
            Direction = DirectionOf(node, json)
        });
        SpotLights = ReadLights(nodes, SpotLightKind, (node, json) =>
        {
            var light = new SpotLight
            {
                Direction = DirectionOf(node, json),
                AttenuationPower = (UInt16)json.GetInt("AttenuationPower"),
                SpotExponent = (UInt16)json.GetInt("SpotExponent"),
                ConeAngle = json.GetUInt("ConeAngle"),
                FalloffAngle = json.GetUInt("FalloffAngle")
            };
            // The cosines the game lights with are kept while they're still the angles', edited angles get new ones
            var (inner, outer) = SpotLight.ConeCosines(light.ConeAngle, light.FalloffAngle);
            var stored = (Inner: json.GetFloat("InnerConeCosine", Single.NaN), Outer: json.GetFloat("OuterConeCosine", Single.NaN));
            var kept = Math.Abs(stored.Inner - inner) < 0.005f && Math.Abs(stored.Outer - outer) < 0.005f;
            light.InnerConeCosine = kept ? stored.Inner : inner;
            light.OuterConeCosine = kept ? stored.Outer : outer;
            return light;
        });
    }

    private static List<T> ReadLights<T>(List<TlmTreeNode> nodes, string kind, Func<TlmTreeNode, JsonObject, T> create) where T : Light
    {
        return nodes.Where(n => n.Kind == kind)
            .Select((node, index) => (Node: node, Json: node.Data, Index: index))
            .OrderBy(n => n.Json.GetInt("Order", Int32.MaxValue)).ThenBy(n => n.Index)
            .Select(n =>
            {
                var light = create(n.Node, n.Json);
                var translation = n.Node.WorldMatrix.Translation;
                light.Position = new Vector4(translation.X, translation.Y, translation.Z, n.Json.GetFloat("PositionW", 1.0f));
                light.Enabled = n.Json.GetBool("Enabled", true);
                light.Intensity = n.Json.GetFloat("Intensity");
                light.Color = n.Json.GetVector4("Color", new Vector4(1, 1, 1, 1));
                if (n.Json["BoundsMin"] != null && n.Json["BoundsMax"] != null)
                {
                    light.BoundsMin = n.Json.GetVector4("BoundsMin");
                    light.BoundsMax = n.Json.GetVector4("BoundsMax");
                }
                else
                {
                    light.ComputeBounds();
                }

                return light;
            }).ToList();
    }

    // Meshes and LODs placed in the scenery. Meshes TT Lab doesn't know are placed meshes too, so new ones can simply be added.
    // Each stays in the tree node it's under while it wasn't moved or still is in that node's box, the others go to the deepest
    // node they're in
    private void ReadInstances(TlmFile file, TlmMaterials materials, List<TlmTreeNode> nodes, List<TlmTreeNode> collisionNodes, List<TlmTreeNode> dynamicModels,
        Dictionary<TlmTreeNode, Int32> treeIndexes)
    {
        var claimed = new HashSet<TlmTreeNode>(collisionNodes.Concat(dynamicModels).SelectMany(node => node.Traverse()));
        var instances = new List<(TlmTreeNode Node, JsonObject Json, Boolean IsLod, Int32 Index)>();
        foreach (var node in nodes)
        {
            if (claimed.Contains(node))
            {
                continue;
            }

            if (node.Kind == LodInstanceKind)
            {
                instances.Add((node, node.Data, true, instances.Count));
                claimed.UnionWith(node.Traverse());
            }
            else if (node.Kind == MeshInstanceKind || node.Mesh != null && node.Kind != LodMeshKind)
            {
                instances.Add((node, node.Data, false, instances.Count));
            }
        }

        foreach (var (node, json, isLod, index) in instances.OrderBy(i => i.Json.GetInt("Order", Int32.MaxValue)).ThenBy(i => i.Index))
        {
            var matrix = TlmNodes.KeepStored(json.GetFloats("Matrix"), GetInstanceMatrix(node), out var unmoved);
            LodModel? lod = null;
            Mesh? mesh = null;
            (Vector3 Min, Vector3 Max)? bounds;
            if (isLod)
            {
                (lod, bounds) = ReadLod(file, materials, node, json, index);
            }
            else
            {
                if (node.Mesh == null)
                {
                    continue;
                }

                mesh = RigidModelData.ReadTlm<Mesh>(Owner, file, node.Mesh, materials, null, $"SceneryMesh_{index}");
                bounds = GetBounds(((IAsset)mesh).GetData<MeshData>());
            }

            var box = GetInstanceBox(json, bounds);
            var corners = Corners(new Vector3(box.V1.X, box.V1.Y, box.V1.Z), new Vector3(box.V2.X, box.V2.Y, box.V2.Z)).Select(corner => Vector3.Transform(corner, matrix)).ToList();
            var (min, max) = (corners.Aggregate(Vector3.Min), corners.Aggregate(Vector3.Max));
            var owner = FindAncestor(node.Parent, treeIndexes.ContainsKey);
            var treeIndex = owner != null && (unmoved || !HasBounds(treeIndexes[owner]) || Holds(treeIndexes[owner], min, max)) ? treeIndexes[owner] : FindTreeNode(min, max);
            var treeNode = Sceneries[treeIndex];
            if (lod != null)
            {
                treeNode.LodIDs.Add(lod.URI);
                treeNode.LodModelMatrices.Add(matrix.ToTwin());
                treeNode.BoundingBoxes.Add(box);
            }
            else
            {
                treeNode.MeshIDs.Add(mesh!.URI);
                treeNode.MeshModelMatrices.Add(matrix.ToTwin());
                // Meshes come before LODs in the list of boxes
                treeNode.BoundingBoxes.Insert(treeNode.MeshIDs.Count - 1, box);
            }

            GrowTreeBounds(treeIndex, min, max);
        }
    }

    // Placed meshes keep the exact matrix they were written with while nothing above them moved
    private static Matrix4x4 GetInstanceMatrix(TlmTreeNode node)
    {
        var parent = node.Parent;
        while (parent != null && TlmNodes.IsIdentity(parent.LocalMatrix))
        {
            parent = parent.Parent;
        }

        return parent == null ? node.LocalMatrix : node.WorldMatrix;
    }

    private (LodModel Lod, (Vector3 Min, Vector3 Max)? Bounds) ReadLod(TlmFile file, TlmMaterials materials, TlmTreeNode node, JsonObject json, Int32 index)
    {
        var lod = new LodModel
        {
            Package = Owner.Package,
            InvariantName = $"{Owner.Name}_LOD_{index}",
            Alias = $"LOD {index}",
            IsInternal = true,
            InternalOwner = Owner
        };
        var lodData = new LodModelData(lod)
        {
            Type = json.GetEnum("LodType", Enums.LodType.COMPRESSED),
            MinDrawDistance = json.GetInt("MinDrawDistance"),
            MaxDrawDistance = json.GetInt("MaxDrawDistance", UInt16.MaxValue),
            Meshes = []
        };
        var distances = json.GetInts("ModelsDrawDistances");
        lodData.ModelsDrawDistances = distances.Length == 3 ? distances : new Int32[3];
        (Vector3 Min, Vector3 Max)? bounds = null;
        var levels = node.Traverse().Where(n => n != node && n.Mesh != null)
            .Select((levelNode, levelIndex) => (Node: levelNode, Level: levelNode.HasData ? levelNode.Data.GetInt("Level", Int32.MaxValue) : Int32.MaxValue, Index: levelIndex))
            .OrderBy(l => l.Level).ThenBy(l => l.Index);
        foreach (var (levelNode, _, levelIndex) in levels)
        {
            var mesh = RigidModelData.ReadTlm<Mesh>(Owner, file, levelNode.Mesh!, materials, levelNode.GetBakedTransform(node), $"LOD_{index}_{levelIndex}");
            lodData.Meshes.Add(mesh.URI);
            bounds ??= GetBounds(((IAsset)mesh).GetData<MeshData>());
        }

        lod.SetData(lodData);
        AssetManager.Get().TryAddAsset(lod);
        return (lod, bounds);
    }

    private static (Vector3 Min, Vector3 Max)? GetBounds(MeshData mesh)
    {
        var modelData = AssetManager.Get().GetAssetData<ModelData>(mesh.Model);
        var positions = modelData.Vertexes.SelectMany(v => v).Select(v => new Vector3(v.Position.X, v.Position.Y, v.Position.Z)).ToList();
        if (positions.Count == 0)
        {
            return null;
        }

        return (positions.Aggregate(Vector3.Min), positions.Aggregate(Vector3.Max));
    }

    // The instance's box is the mesh's box, kept as stored while it still holds the mesh
    private static BoundingBox GetInstanceBox(JsonObject json, (Vector3 Min, Vector3 Max)? meshBounds)
    {
        var stored = json.GetFloats("BoundingBox");
        if (stored.Length == 8 && (meshBounds == null || Contains(stored, meshBounds.Value)))
        {
            return new BoundingBox { V1 = new Vector4(stored[0], stored[1], stored[2], stored[3]), V2 = new Vector4(stored[4], stored[5], stored[6], stored[7]) };
        }

        var (min, max) = meshBounds ?? (Vector3.Zero, Vector3.Zero);
        var radius = Vector3.Max(Vector3.Abs(min), Vector3.Abs(max)).Length();
        return new BoundingBox { V1 = new Vector4(min.X, min.Y, min.Z, radius), V2 = new Vector4(max.X, max.Y, max.Z, 0) };
    }

    // The game's boxes can be a hair smaller than their meshes
    private static Boolean Contains(Single[] box, (Vector3 Min, Vector3 Max) bounds)
    {
        var min = new Vector3(box[0], box[1], box[2]);
        var max = new Vector3(box[4], box[5], box[6]);
        var tolerance = Vector3.Max(new Vector3(1e-4f), (max - min) * 1e-4f);
        return Vector3.Min(min, bounds.Min + tolerance) == min && Vector3.Max(max, bounds.Max - tolerance) == max;
    }

    private Boolean HasBounds(Int32 treeIndex) => Single.IsFinite(Sceneries[treeIndex].BoundsMin.X);

    // Whether the tree node's bounds hold the box, give or take the rounding of placing it again
    private Boolean Holds(Int32 treeIndex, Vector3 min, Vector3 max)
    {
        if (!HasBounds(treeIndex))
        {
            return false;
        }

        var node = Sceneries[treeIndex];
        var nodeMin = new Vector3(node.BoundsMin.X, node.BoundsMin.Y, node.BoundsMin.Z);
        var nodeMax = new Vector3(node.BoundsMax.X, node.BoundsMax.Y, node.BoundsMax.Z);

        var slack = Vector3.Max(new Vector3(1e-4f), Vector3.Max(Vector3.Abs(nodeMin), Vector3.Abs(nodeMax)) * 1e-5f);
        return Vector3.Min(nodeMin, min + slack) == nodeMin && Vector3.Max(nodeMax, max - slack) == nodeMax;
    }

    // BoundsMin and BoundsMax are a tree node's bounds, BoundsHalfSize its half size and BoundsCenter its center with the radius of its bounds.
    // Bounds that don't hold what the node draws anymore grow, up to the root, so nothing gets culled while it's on screen.
    // Placing the box again isn't exact, it has to stick out by more than that
    private void GrowTreeBounds(Int32 treeIndex, Vector3 min, Vector3 max)
    {
        var (parents, _) = GetTreeLinks();
        for (var index = treeIndex; index >= 0; index = parents[index])
        {
            var node = Sceneries[index];
            if (Holds(index, min, max))
            {
                return;
            }

            var oldMin = new Vector3(node.BoundsMin.X, node.BoundsMin.Y, node.BoundsMin.Z);
            var oldMax = new Vector3(node.BoundsMax.X, node.BoundsMax.Y, node.BoundsMax.Z);
            SetTreeBounds(node, Vector3.Min(oldMin, min), Vector3.Max(oldMax, max));
        }
    }

    // The deepest node of the tree whose bounds hold the box, or its center when no node holds all of it, the root when none do
    private Int32 FindTreeNode(Vector3 min, Vector3 max)
    {
        var (parents, _) = GetTreeLinks();
        var center = (min + max) / 2;
        var best = (Index: 0, Depth: -1, Whole: false);
        for (var index = 0; index < Sceneries.Count; index++)
        {
            var whole = Holds(index, min, max);
            if (!whole && !Holds(index, center, center))
            {
                continue;
            }

            var depth = 0;
            for (var parent = parents[index]; parent >= 0; parent = parents[parent])
            {
                depth++;
            }

            if (whole && !best.Whole || whole == best.Whole && depth > best.Depth)
            {
                best = (index, depth, whole);
            }
        }

        return best.Index;
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        WriteTlm().Save(dataPath);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        var addedMaterials = ReadTlm(TlmFile.Load(dataPath));
        DisposedValue = false;
        if (addedMaterials)
        {
            // Materials made in Blender are referred to from now on
            SaveInternal(dataPath, settings);
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var scenery = GetTwinItem<ITwinScenery>();
        FogColor = scenery.FogColor;
        UnusedByte = scenery.UnusedByte;
        
        HasLighting = scenery.HasLighting;
        if (HasLighting)
        {
            AmbientLights = CloneUtils.DeepClone(scenery.AmbientLights);
            DirectionalLights = CloneUtils.DeepClone(scenery.DirectionalLights);
            PointLights = CloneUtils.DeepClone(scenery.PointLights);
            SpotLights = CloneUtils.DeepClone(scenery.SpotLights);
            LightOrder = [..scenery.LightOrder];
        }
        
        Sceneries = new List<SceneryBaseData>();
        foreach (var sc in scenery.Sceneries)
        {
            Sceneries.Add((SceneryBaseData)Activator.CreateInstance(ScIndexToType[sc.GetObjectIndex()], Owner, sc)!);
        }

        EnableEveryLight();
    }

    /// <summary>
    /// How many lights the root tree node's flags can turn on
    /// </summary>
    public const Int32 MaxLights = 128;

    public Int32 LightCount => AmbientLights.Count + DirectionalLights.Count + PointLights.Count + SpotLights.Count;

    /// <summary>
    /// The game only gathers the lights whose bit the root tree node has (<c>FUN_001c7f50</c>, the scenery's order of lights), and every
    /// retail scenery's root has the bits of all of its lights and no others. Kept that way: a light put in lights the level, and
    /// sceneries made in Blender, whose nodes come without bits, aren't left dark
    /// </summary>
    internal void EnableEveryLight()
    {
        if (Sceneries.Count == 0)
        {
            return;
        }

        var lights = HasLighting ? LightCount : 0;
        var enabler = Sceneries[0].LightsEnabler;
        for (var i = 0; i < enabler.Length; i++)
        {
            enabler[i] = i < lights;
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        CheckCount("lights", HasLighting ? LightCount : 0, MaxLights);
        WarnAboutCollisionOutsideBounds();
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(factory.ChunkPath.Replace('/', '\\'));
        writer.Write(FogColor);
        writer.Write(UnusedByte);
        writer.Write(SkydomeID == LabURI.Empty ? 0 : assetManager.GetAsset(SkydomeID).ExportTwinID);
        writer.Write(HasLighting);
        if (HasLighting)
        {
            writer.Write(AmbientLights.Count);
            foreach (var ambient in AmbientLights)
            {
                ambient.Write(writer);
            }

            writer.Write(DirectionalLights.Count);
            foreach (var directional in DirectionalLights)
            {
                directional.Write(writer);
            }

            writer.Write(PointLights.Count);
            foreach (var point in PointLights)
            {
                point.Write(writer);
            }

            writer.Write(SpotLights.Count);
            foreach (var spot in SpotLights)
            {
                spot.Write(writer);
            }

            writer.Write(LightOrder.Count);
            foreach (var value in LightOrder)
            {
                writer.Write(value);
            }
        }
        EnableEveryLight();
        writer.Write(Sceneries.Count);
        foreach (var scenery in Sceneries)
        {
            writer.Write((Int32)scenery.GetSceneryType());
            scenery.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateScenery(ms);
    }

    private void WarnAboutCollisionOutsideBounds()
    {
        var assetManager = AssetManager.Get();
        if (Sceneries.FirstOrDefault() is not { } root || Collision == LabURI.Empty || !assetManager.DoesAssetExist(Collision))
        {
            return;
        }

        const Single play = 0.01f;
        var outside = UsedVertexes(assetManager.GetAssetData<CollisionData>(Collision))
            .Count(vertex => vertex.X < root.BoundsMin.X - play || vertex.Y < root.BoundsMin.Y - play || vertex.Z < root.BoundsMin.Z - play ||
                             vertex.X > root.BoundsMax.X + play || vertex.Y > root.BoundsMax.Y + play || vertex.Z > root.BoundsMax.Z + play);
        if (outside > 0)
        {
            Log.WriteLine($"{Owner.Chunk}'s scenery bounds leave {outside} of its collision's vertexes outside, objects there have nothing under them in the game: the scenery's Bounds set them, or the viewport's Scenery bounds layer",
                Log.LogType.Warning);
        }
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var graphicsSection = section.GetItem<ITwinSection>(Constants.SCENERY_GRAPHICS_SECTION);
        var skydomeSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_SKYDOMES_SECTION);
        if (SkydomeID != LabURI.Empty)
        {
            assetManager.GetAsset(SkydomeID).ResolveChunkResources(factory, skydomeSection);
        }

        if (DynamicScenery != LabURI.Empty)
        {
            assetManager.GetAssetData<DynamicSceneryData>(DynamicScenery).ResolveChunkResources(factory, section,
                Constants.SCENERY_DYNAMIC_SECENERY_ITEM, layoutId);
        }
        else
        {
            var dummyDynamicScenery = new DynamicSceneryData(null);
            dummyDynamicScenery.ResolveChunkResources(factory, section, Constants.SCENERY_DYNAMIC_SECENERY_ITEM, layoutId);
        }

        foreach (var scenery in Sceneries)
        {
            scenery.ResolveChunkResouces(factory, graphicsSection);
        }

        return base.ResolveChunkResources(factory, section, Constants.SCENERY_SECENERY_ITEM, layoutId);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var result = new List<ViewportObject>();
        var renderContext = viewportContext.RenderContext;
        // The chunk's lights light its objects, its strongest directional lights are what its environment mapped materials look up by
        void ApplyLights()
        {
            renderContext.Lights = SceneLights.Of(this);
            renderContext.EnvLights = Rendering.EnvLights.Of(DirectionalLights);
        }

        ApplyLights();
        var sceneryVisual = new Rendering.Objects.Scenery(renderContext, renderContext.MeshService, this);
        var editingObject = new EditableObject(renderContext, sceneryVisual, $"SCENERY_{Owner.FullDataPath}")
        {
            IsSelectable = false
        };
        PropertyNode?[] lighting = [property.Find($"[data].AssetData.{nameof(HasLighting)}"), property.Find($"[data].AssetData.{nameof(AmbientLights)}"),
            property.Find($"[data].AssetData.{nameof(DirectionalLights)}"), property.Find($"[data].AssetData.{nameof(PointLights)}"),
            property.Find($"[data].AssetData.{nameof(SpotLights)}")];
        result.Add(new ViewportObject(editingObject, $"SCENERY_{property.Path}", property)
        {
            Category = ViewportObjectCategory.Scenery,
            RenderDependencies = lighting.OfType<PropertyNode>().ToList(),
            Refresh = () =>
            {
                ApplyLights();
                return true;
            },
        });
        result.AddRange(GetLightObjects(viewportContext, property));
        if (GetBoundsObject(viewportContext, property) is { } bounds)
        {
            result.Add(bounds);
        }
        
        if (DynamicScenery != LabURI.Empty)
        {
            var dynamicSceneryVisual = new Rendering.Objects.DynamicScenery(viewportContext.RenderContext, viewportContext.RenderContext.MeshService, AssetManager.Get().GetAssetData<DynamicSceneryData>(DynamicScenery));
            var dynamicSceneryEditingObject = new EditableObject(viewportContext.RenderContext, dynamicSceneryVisual,
                $"DYNAMIC_SCENERY_{Owner.FullDataPath}")
            {
                IsSelectable = false
            };
            result.Add(new ViewportObject(dynamicSceneryEditingObject, $"DYNAMIC_SCENERY_{property.Path}", property) { Category = ViewportObjectCategory.DynamicScenery });
            var boundsEditingObject = new EditableObject(viewportContext.RenderContext, new Rendering.Objects.DynamicSceneryBounds(viewportContext.RenderContext, dynamicSceneryVisual),
                $"DYNAMIC_SCENERY_BOUNDS_{Owner.FullDataPath}")
            {
                IsSelectable = false
            };
            result.Add(new ViewportObject(boundsEditingObject, $"DYNAMIC_SCENERY_BOUNDS_{property.Path}", property) { Category = ViewportObjectCategory.DynamicSceneryBounds });
        }

        if (Collision != LabURI.Empty)
        {
            var collisionVisual = (Rendering.Objects.Collision)viewportContext.RenderContext.MeshService.GetMesh(Collision).Model!;
            var collisionEditing =
                new EditableObject(viewportContext.RenderContext, collisionVisual, $"COLLISION_{Owner.FullDataPath}")
                {
                    IsSelectable = false
                };
            result.Add(new ViewportObject(collisionEditing, $"COLLISION_{property.Path}", property, AssetManager.Get().GetAssetData(Collision)) { Category = ViewportObjectCategory.Collision });
            collisionEditing.IsVisible = false;
        }
        
        return result;
    }

    // The box the game keeps the chunk's objects in, picked and dragged by the handle on its top
    private ViewportObject? GetBoundsObject(ViewportContext viewportContext, PropertyNode property)
    {
        var bounds = property.Find($"[data].AssetData.{nameof(Bounds)}");
        var center = bounds?.FindChild($".{nameof(SceneryBounds.Center)}");
        var halfSize = bounds?.FindChild($".{nameof(SceneryBounds.HalfSize)}");
        if (bounds == null || center == null || halfSize == null || Sceneries.FirstOrDefault() is not { } root)
        {
            return null;
        }

        var top = new SceneryBoundsTop(halfSize);
        var handle = new SceneryBoundsHandle(viewportContext.RenderContext, $"{Owner.FullDataPath}_BOUNDS");
        handle.SetPosition(top.ToPosition(center.GetValue()));
        handle.SetScale(SceneryBounds.HalfSizeOf(root));
        _ = new SceneryBoundsVisual(viewportContext.RenderContext, handle);
        return new ViewportObject(handle, $"SCENERY_BOUNDS_{property.Path}", property)
        {
            Position = center,
            PositionConverter = top,
            Scale = halfSize,
            Category = ViewportObjectCategory.SceneryBounds,
            InspectorFocus = bounds,
        };
    }

    // Every light at its place with its icon, a directional or spot light turned the way of its direction
    private List<ViewportObject> GetLightObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var result = new List<ViewportObject>();
        var renderContext = viewportContext.RenderContext;
        (string List, IReadOnlyList<Light> Lights)[] kinds = [(nameof(AmbientLights), AmbientLights), (nameof(DirectionalLights), DirectionalLights),
            (nameof(PointLights), PointLights), (nameof(SpotLights), SpotLights)];
        foreach (var (list, lights) in kinds)
        {
            for (var i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                var lightNode = property.Find($"[data].AssetData.{list}[{i}]");
                var positionNode = lightNode?.FindChild($".{nameof(Light.Position)}");
                if (lightNode == null || positionNode == null)
                {
                    continue;
                }

                var billboard = viewportContext.EditingContext.CreateLightBillboard();
                var editableObject = new EditableObject(renderContext, billboard, $"{Owner.FullDataPath}_{list}{i}", -vec3.Ones, vec3.Ones * 2.0f);
                editableObject.SetPosition(new vec3(light.Position.X, light.Position.Y, light.Position.Z));
                var visual = new SceneryLightVisual(renderContext, editableObject, light);
                var directionNode = light is DirectionalLight or SpotLight ? lightNode.FindChild(".Direction") : null;
                var rotation = directionNode == null ? null : new LightDirectionRotation(directionNode);
                if (rotation != null)
                {
                    editableObject.SetRotation(rotation.ToRotation(directionNode!.GetValue()));
                }

                result.Add(new ViewportObject(editableObject, positionNode.Path, property)
                {
                    Position = positionNode,
                    Rotation = directionNode,
                    RotationConverter = rotation,
                    Category = ViewportObjectCategory.Lights,
                    InspectorFocus = lightNode,
                    DuplicatedElement = lightNode,
                    RenderDependencies = [lightNode],
                    Refresh = () =>
                    {
                        visual.Update(light);
                        return true;
                    },
                });
            }
        }

        return result;
    }
}
