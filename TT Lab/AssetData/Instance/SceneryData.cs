using GlmSharp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.DynamicScenery;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Extensions;
using TT_Lab.Project.Migration;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Lighting;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Vector3 = System.Numerics.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Instance;

public class SceneryData : AbstractAssetData
{
    [System.Text.Json.Serialization.JsonConstructor]
    private SceneryData() : base(null) { }

    public SceneryData(IAsset asset) : base(asset)
    {
        SkydomeID = LabURI.Empty;
        Collision = LabURI.Empty;
        DynamicScenery = LabURI.Empty;
        AmbientLights = new List<AmbientLight>();
        DirectionalLights = new List<DirectionalLight>();
        PointLights = new List<PointLight>();
        SpotLights = new List<SpotLight>();
        SceneryBounds.SetCell(this, vec3.Zero, SceneryBounds.DefaultHalfSize);
    }

    public SceneryData(IAsset asset, ITwinScenery scenery) : this(asset)
    {
        SetTwinItem(scenery);
    }
    
    public LabURI SkydomeID { get; set; }
    
    public LabURI DynamicScenery { get; set; }
    
    public LabURI Collision { get; set; }

    [Editable(Caption = "Fog", EditorDescType = typeof(FogColorEditorDesc), Hint = "The fog the game mixes into the distance while the player is in the chunk, up to 30% of its color far away and on the sky. Hover a choice for its colors")]
    public UInt32 FogColor { get; set; }
    
    public Byte UnusedByte { get; set; }

    // A value of the corners, its middle and half size set by setting it whole: the history keeps the corners, undo puts them back exactly
    [Editable(IsComputed = true, Hint = "The box the game keeps the chunk's objects in, its scenery tree's root: an object outside of it has nothing under it, so it has to hold every place objects go. The viewport's Scenery bounds layer shows it, moved and sized by the handle on its top")]
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public SceneryBounds Bounds
    {
        get => new(BoundsMin, BoundsMax);
        set => (BoundsMin, BoundsMax) = (value.Min, value.Max);
    }

    [Editable(Caption = "Tree Depth", Hint = "How many levels below its root the tree the game culls the scenery's meshes with goes, which the build makes from where they are. The game's chunks have 2 to 5, a deeper tree culls in smaller pieces")]
    [EditorParam(TextFieldViewModel.TextFieldNumberRange, new[] { 1, 8 })]
    public UInt32 TreeDepth { get; set; } = SceneryTree.DefaultDepth;

    /// <summary>
    /// Whether the game lights the chunk's objects, which it does by the scenery's lights: without any its lit materials are black
    /// </summary>
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public Boolean HasLighting => LightCount > 0;

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

    /// <summary>
    /// The meshes and LODs placed in the scenery, in the order the game's tree lists them. Edited in the viewport's scenery mode, the
    /// inspector shows the one selected there
    /// </summary>
    [Editable]
    [EditorHidden]
    public List<SceneryPlacement> Placements { get; set; } = [];

    /// <summary>
    /// The collision's vertexes and triangles, which the viewport's scenery mode edits. One value for the history, the triangles would be
    /// tens of thousands of nodes
    /// </summary>
    [Editable]
    [EditorHidden]
    [JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public CollisionGeometry? CollisionShape
    {
        get => CollisionDataOrNull()?.Geometry;
        set
        {
            if (value != null && CollisionDataOrNull() is { } collision)
            {
                collision.Geometry = value;
            }
        }
    }

    internal CollisionData? CollisionDataOrNull()
    {
        var assetManager = AssetManager.Get();
        return Collision != LabURI.Empty && assetManager.DoesAssetExist(Collision) ? assetManager.GetAssetData<CollisionData>(Collision) : null;
    }

    /// <summary>
    /// The smallest corner of the root's cell, the box the game keeps the chunk's objects in (<see cref="Bounds"/>)
    /// </summary>
    public Vector3 BoundsMin { get; set; }

    public Vector3 BoundsMax { get; set; }

    /// <summary>
    /// The game's values of the tree nodes the build doesn't work out to the bit
    /// </summary>
    public List<SceneryTreeNode> TreeNodes { get; set; } = [];

    internal SceneryTree.Box RootCell => new(BoundsMin, BoundsMax);

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
        Placements.Clear();
        TreeNodes.Clear();
    }
    
    public const string TlmAssetType = "Scenery";
    public const string TlmKind = "scenery";
    public const string MeshInstanceKind = "scenery_mesh";
    public const string LodInstanceKind = "scenery_lod";
    public const string MeshesKind = "scenery_meshes";
    public const string LodsKind = "scenery_lods";
    public const string LodMeshKind = "lod_mesh";
    public const string LightsKind = "lights";
    public const string AmbientLightKind = "ambient_light";
    public const string DirectionalLightKind = "directional_light";
    public const string PointLightKind = "point_light";
    public const string SpotLightKind = "spot_light";

    /// <summary>
    /// Writes the scenery with its placed meshes under its root's Meshes and its LODs under LODs, each with the tree node the build puts
    /// it in, so it stays in the node the game had it in while that node still holds it
    /// </summary>
    internal TlmFile WriteTlm()
    {
        var assetManager = AssetManager.Get();
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var materials = new TlmMaterials(file);
        var (paths, kept) = TreeToWrite();
        var rootData = new JsonObject
        {
            ["FogColor"] = (Int32)FogColor,
            ["UnusedByte"] = (Int32)UnusedByte,
            ["TreeDepth"] = TlmJson.ToJson(TreeDepth),
            ["BoundsMin"] = TlmJson.ToJson([BoundsMin.X, BoundsMin.Y, BoundsMin.Z]),
            ["BoundsMax"] = TlmJson.ToJson([BoundsMax.X, BoundsMax.Y, BoundsMax.Z])
        };
        if (LightOrder.Count > 0)
        {
            rootData["LightOrder"] = TlmJson.ToJson(LightOrder.ToArray());
        }

        var treeNodes = WriteTreeNodes(kept);
        if (treeNodes.Count > 0)
        {
            rootData["TreeNodes"] = treeNodes;
        }

        var root = TlmNodes.Create(TlmKind, Owner.Name, rootData);
        AddPlacementNodes(file, materials, root, Placements, i => Placements[i].Matrix.ToSystem(), i => paths[i]);

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

    // The placed meshes and LODs in two groups, the way Blender shows them, each with its place among all of them
    private static void AddPlacementNodes(TlmFile file, TlmMaterials materials, JsonObject root, IReadOnlyList<SceneryPlacement> placements,
        Func<Int32, Matrix4x4> matrixOf, Func<Int32, String?> pathOf)
    {
        var meshes = root.AddChild(TlmNodes.Create(MeshesKind, "Meshes"));
        var lods = root.AddChild(TlmNodes.Create(LodsKind, "LODs"));
        var meshCount = 0;
        var lodCount = 0;
        for (var i = 0; i < placements.Count; i++)
        {
            var number = placements[i].IsLod ? lodCount++ : meshCount++;
            (placements[i].IsLod ? lods : meshes).AddChild(WritePlacementNode(file, materials, placements[i], matrixOf(i), i, pathOf(i), number));
        }
    }

    // A placed mesh or LOD as a node with its mesh, or its levels' meshes, in it, named by its number in its group
    private static JsonObject WritePlacementNode(TlmFile file, TlmMaterials materials, SceneryPlacement placement, Matrix4x4 matrix, Int32 order, String? path, Int32 number)
    {
        var assetManager = AssetManager.Get();
        var json = new JsonObject { ["Order"] = order };
        if (path != null)
        {
            json["Node"] = path;
        }

        json["Matrix"] = TlmJson.ToJson(TlmNodes.ToArray(matrix));
        json["BoundingBox"] = WriteBoundingBox(placement.Box);

        if (!placement.IsLod)
        {
            var node = TlmNodes.Create(MeshInstanceKind, $"Mesh {number}", json);
            node.SetTransform(matrix);
            if (assetManager.DoesAssetExist(placement.Model))
            {
                node[TlmNodes.MeshKey] = assetManager.GetAssetData<MeshData>(placement.Model).WriteTlmMesh(file, materials);
            }

            return node;
        }

        var lodData = assetManager.GetAssetData<LodModelData>(placement.Model);
        json["LodType"] = lodData.Type.ToString();
        json["MinDrawDistance"] = lodData.MinDrawDistance;
        json["MaxDrawDistance"] = lodData.MaxDrawDistance;
        json["ModelsDrawDistances"] = TlmJson.ToJson(lodData.ModelsDrawDistances);
        var lodNode = TlmNodes.Create(LodInstanceKind, $"LOD {number}", json);
        lodNode.SetTransform(matrix);
        for (var level = 0; level < lodData.Meshes.Count; level++)
        {
            var levelNode = lodNode.AddChild(TlmNodes.Create(LodMeshKind, $"LOD {number} Level {level}", new JsonObject { ["Level"] = level }));
            levelNode[TlmNodes.MeshKey] = assetManager.GetAssetData<MeshData>(lodData.Meshes[level]).WriteTlmMesh(file, materials);
        }

        return lodNode;
    }

    /// <summary>
    /// The placements as a model file of their own, moved so the origin is where they're placed around (a prefab of them)
    /// </summary>
    internal TlmFile WritePlacements(IReadOnlyList<SceneryPlacement> placements, Vector3 origin)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var materials = new TlmMaterials(file);
        var root = TlmNodes.Create(TlmKind, Owner.Name);
        AddPlacementNodes(file, materials, root, placements, i =>
        {
            var matrix = placements[i].Matrix.ToSystem();
            matrix.Translation -= origin;
            return matrix;
        }, _ => null);

        file.Root = root;
        return file;
    }

    /// <summary>
    /// The meshes and LODs placed in such a file made into meshes and LODs of this scenery, moved by the offset. Not among its placements
    /// yet
    /// </summary>
    internal List<SceneryPlacement> ReadPlacements(TlmFile file, Vector3 offset)
    {
        var root = TlmTreeNode.Of(file.Root ?? new JsonObject());
        var placed = ReadPlacementNodes(file, new TlmMaterials(file, Owner), root.Traverse().Skip(1), $"Placed {(UInt32)Guid.NewGuid().GetHashCode():X8} ");
        foreach (var placement in placed)
        {
            var matrix = placement.Matrix.ToSystem();
            matrix.Translation += offset;
            placement.Matrix = matrix.ToTwin();
        }

        return placed;
    }

    /// <summary>
    /// A placeholder of the shape standing at the position: a new mesh of the scenery on the checker material (one of the package, made the
    /// first time), not among its placements yet
    /// </summary>
    internal SceneryPlacement CreatePlaceholder(PlaceholderShape shape, Vector3 position)
    {
        var file = new TlmFile(TlmAssetType, Owner.Name);
        var mesh = PlaceholderShapes.Make(shape);
        var material = SceneryPlaceholders.UseCheckerMaterial(file, Owner.Package);
        var matrix = Matrix4x4.CreateTranslation(position);
        var node = TlmNodes.Create(MeshInstanceKind, shape.ToString(), new JsonObject
        {
            ["Matrix"] = TlmJson.ToJson(TlmNodes.ToArray(matrix)),
            ["BoundingBox"] = new JsonArray()
        });
        node.SetTransform(matrix);
        node[TlmNodes.MeshKey] = new JsonObject { ["parts"] = new JsonArray(SceneryPlaceholders.WritePart(file, mesh, material)) };
        var root = TlmNodes.Create(TlmKind, Owner.Name);
        root.AddChild(node);
        file.Root = root;
        return ReadPlacementNodes(file, new TlmMaterials(file, Owner), TlmTreeNode.Of(root).Traverse().Skip(1), $"{shape} {(UInt32)Guid.NewGuid().GetHashCode():X8} ").Single();
    }

    // The node every placement is in, the one it was in while that still holds it
    private string[] PlacementPaths()
    {
        var root = RootCell;
        return Placements.Select(placement => SceneryTree.PathOf(root, TreeDepth, SceneryTree.WorldBox(placement), placement.Node)).ToArray();
    }

    // The node every placement is in, and the kept values the tree still needs: of the nodes it has, the ones that aren't what the
    // build works out and the nodes' own light bits
    private (string[] Paths, List<SceneryTreeNode> Kept) TreeToWrite()
    {
        var layout = SceneryTree.Layout(RootCell, TreeDepth, Placements);
        var paths = new string[Placements.Count];
        foreach (var node in layout)
        {
            foreach (var index in node.Meshes.Concat(node.Lods))
            {
                paths[index] = node.Path;
            }
        }

        var nodes = layout.ToDictionary(node => node.Path);
        var kept = TreeNodes.Where(kept => nodes.TryGetValue(kept.Path, out var node) && (kept.Path.Length > 0 && kept.LightsEnabler != null || !SceneryTree.IsWorkedOut(kept, node))).ToList();
        return (paths, kept);
    }

    // A dictionary keyed by their index like every list of the file
    private static JsonObject WriteTreeNodes(List<SceneryTreeNode> kept)
    {
        var result = new JsonObject();
        foreach (var node in kept)
        {
            var json = new JsonObject
            {
                ["Path"] = node.Path,
                ["BoundsCenter"] = TlmJson.ToJson(node.BoundsCenter),
                ["BoundsMin"] = TlmJson.ToJson(node.BoundsMin),
                ["BoundsMax"] = TlmJson.ToJson(node.BoundsMax),
                ["BoundsHalfSize"] = TlmJson.ToJson(node.BoundsHalfSize)
            };
            if (node.LightsEnabler != null)
            {
                json["LightsEnabler"] = TlmJson.ToJson(node.LightsEnabler);
            }

            result[result.Count.ToString()] = json;
        }

        return result;
    }

    private static SceneryTreeNode ReadTreeNode(JsonObject json)
    {
        var lights = json.GetBools("LightsEnabler");
        Boolean[]? enabler = null;
        if (lights.Length > 0)
        {
            enabler = new Boolean[MaxLights];
            Array.Copy(lights, enabler, Math.Min(lights.Length, MaxLights));
        }

        return new SceneryTreeNode
        {
            Path = json.GetString("Path") ?? string.Empty,
            BoundsCenter = json.GetVector4("BoundsCenter"),
            BoundsMin = json.GetVector4("BoundsMin"),
            BoundsMax = json.GetVector4("BoundsMax"),
            BoundsHalfSize = json.GetVector4("BoundsHalfSize"),
            LightsEnabler = enabler
        };
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

    /// <returns>Whether the file has to be written again: materials made in Blender became the project's, or it had 1.0.0's tree</returns>
    internal Boolean ReadTlm(TlmFile file)
    {
        // The add-on of TT Lab 1.0.0, or a Blender scene it imported, still writes the tree the migration takes out of projects of then:
        // read without it, the game's values of the tree were lost
        var hadTree = file.Root is { } json && Version110.HasTree(json) && Version110.MigrateScenery(json);
        if (hadTree)
        {
            Log.WriteLine($"{Owner.Alias}'s file has the scenery tree of TT Lab 1.0.0, which an older Blender add-on wrote: it's read the way migrating a project brings it to " +
                          $"{TT_Lab.Project.Project.CURRENT_VERSION}. Install the add-on that comes with this TT Lab", Log.LogType.Warning);
        }

        var root = TlmTreeNode.Of(file.Root ?? new JsonObject());
        var materials = new TlmMaterials(file, Owner);
        var data = root.Data;
        FogColor = (UInt32)data.GetInt("FogColor", (Int32)FogColor);
        UnusedByte = (Byte)data.GetInt("UnusedByte", UnusedByte);
        LightOrder = data.GetInts("LightOrder").ToList();
        TreeDepth = data.GetUInt("TreeDepth", SceneryTree.DefaultDepth);
        var cellMin = data.GetFloats("BoundsMin");
        var cellMax = data.GetFloats("BoundsMax");
        var cellKept = cellMin.Length >= 3 && cellMax.Length >= 3;
        if (cellKept)
        {
            BoundsMin = new Vector3(cellMin[0], cellMin[1], cellMin[2]);
            BoundsMax = new Vector3(cellMax[0], cellMax[1], cellMax[2]);
        }

        TreeNodes = data.GetIndexed("TreeNodes").Select(ReadTreeNode).ToList();
        var nodes = root.Traverse().Skip(1).ToList();
        ReadLights(nodes);

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

        ReadPlacements(file, materials, nodes, collisionNodes, dynamicModels);
        WidenRoot(cellKept, collisionData);
        var paths = PlacementPaths();
        for (var i = 0; i < Placements.Count; i++)
        {
            Placements[i].Node = paths[i];
        }

        return materials.AddedToProject || hadTree;
    }

    // The root holds the chunk's objects (SceneryBounds): one the file left to what got placed in it (made in Blender) or one without room
    // (the flat ground's new chunks had) gets a box like the game's around the collision and what's placed
    private void WidenRoot(Boolean kept, CollisionData collision)
    {
        if (kept && SceneryBounds.HoldsAnything(this))
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

        foreach (var placement in Placements)
        {
            var box = SceneryTree.WorldBox(placement);
            min = vec3.Min(min, new vec3(box.Min.X, box.Min.Y, box.Min.Z));
            max = vec3.Max(max, new vec3(box.Max.X, box.Max.Y, box.Max.Z));
        }

        SceneryBounds.Widen(this, min, max);
    }

    // The tools left vertexes no triangle uses in some collisions
    private static IEnumerable<Vector4> UsedVertexes(CollisionData collision)
    {
        return collision.Triangles.SelectMany(triangle => triangle.Face.Indexes ?? []).Distinct()
            .Where(index => index >= 0 && index < collision.Vertexes.Count)
            .Select(index => collision.Vertexes[index]);
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

    // Meshes and LODs placed in the scenery, wherever they are under its root
    private void ReadPlacements(TlmFile file, TlmMaterials materials, List<TlmTreeNode> nodes, List<TlmTreeNode> collisionNodes, List<TlmTreeNode> dynamicModels)
    {
        var claimed = new HashSet<TlmTreeNode>(collisionNodes.Concat(dynamicModels).SelectMany(node => node.Traverse()));
        Placements = ReadPlacementNodes(file, materials, nodes.Where(node => !claimed.Contains(node)), string.Empty);
    }

    // The meshes and LODs placed in the nodes, made into meshes and LODs of the scenery named with the prefix. Meshes TT Lab doesn't know
    // are placed meshes too, so new ones can simply be added, and so is a LOD's level moved out of its LOD
    private List<SceneryPlacement> ReadPlacementNodes(TlmFile file, TlmMaterials materials, IEnumerable<TlmTreeNode> nodes, String prefix)
    {
        var claimed = new HashSet<TlmTreeNode>();
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
            else if (node.Kind == MeshInstanceKind || node.Mesh != null)
            {
                instances.Add((node, node.Data, false, instances.Count));
            }
        }

        var placements = new List<SceneryPlacement>();
        foreach (var (node, json, isLod, index) in instances.OrderBy(i => i.Json.GetInt("Order", Int32.MaxValue)).ThenBy(i => i.Index))
        {
            var matrix = TlmNodes.KeepStored(json.GetFloats("Matrix"), GetInstanceMatrix(node), out _);
            IAsset model;
            (Vector3 Min, Vector3 Max)? bounds;
            if (isLod)
            {
                (model, bounds) = ReadLod(file, materials, node, json, index, prefix);
            }
            else
            {
                if (node.Mesh == null)
                {
                    continue;
                }

                model = RigidModelData.ReadTlm<Mesh>(Owner, file, node.Mesh, materials, null, $"{prefix}SceneryMesh_{index}");
                bounds = GetBounds(model.GetData<MeshData>());
            }

            placements.Add(new SceneryPlacement
            {
                Model = model.URI,
                IsLod = isLod,
                Matrix = matrix.ToTwin(),
                Box = GetInstanceBox(json, bounds),
                // Placed ones made outside of TT Lab go where the build puts new ones
                Node = json.GetString("Node")
            });
        }

        return placements;
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

    private (LodModel Lod, (Vector3 Min, Vector3 Max)? Bounds) ReadLod(TlmFile file, TlmMaterials materials, TlmTreeNode node, JsonObject json, Int32 index, String prefix)
    {
        var lod = new LodModel
        {
            Package = Owner.Package,
            InvariantName = $"{Owner.Name}_{prefix}LOD_{index}",
            Alias = $"{prefix}LOD {index}",
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
            var mesh = RigidModelData.ReadTlm<Mesh>(Owner, file, levelNode.Mesh!, materials, levelNode.GetBakedTransform(node), $"{prefix}LOD_{index}_{levelIndex}");
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

    // The game's boxes can be a hair smaller than their meshes (Doc Amok's poles by 1.2e-4 of their 0.29)
    private static Boolean Contains(Single[] box, (Vector3 Min, Vector3 Max) bounds)
    {
        var min = new Vector3(box[0], box[1], box[2]);
        var max = new Vector3(box[4], box[5], box[6]);
        var tolerance = Vector3.Max(new Vector3(1e-4f), (max - min) * 1e-3f);
        return Vector3.Min(min, bounds.Min + tolerance) == min && Vector3.Max(max, bounds.Max - tolerance) == max;
    }

    protected override void SaveInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        WriteTlm().Save(dataPath);
    }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        var writeAgain = ReadTlm(TlmFile.Load(dataPath));
        DisposedValue = false;
        if (writeAgain)
        {
            // Materials made in Blender are referred to from now on, a file of 1.0.0's tree has this TT Lab's list
            SaveInternal(dataPath, settings);
        }
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var scenery = GetTwinItem<ITwinScenery>();
        FogColor = scenery.FogColor;
        UnusedByte = scenery.UnusedByte;
        if (scenery.HasLighting)
        {
            AmbientLights = CloneUtils.DeepClone(scenery.AmbientLights);
            DirectionalLights = CloneUtils.DeepClone(scenery.DirectionalLights);
            PointLights = CloneUtils.DeepClone(scenery.PointLights);
            SpotLights = CloneUtils.DeepClone(scenery.SpotLights);
            LightOrder = [..scenery.LightOrder];
        }

        var assetManager = AssetManager.Get();
        var flat = SceneryTree.Flatten(scenery.Sceneries, RootLights(),
            (id, isLod) => isLod ? assetManager.GetUriByTwinId<LodModel>(Owner, id) : assetManager.GetUriByTwinId<Mesh>(Owner, id));
        Placements = flat.Placements;
        TreeNodes = flat.Kept;
        TreeDepth = flat.Depth;
        BoundsMin = flat.Root.Min;
        BoundsMax = flat.Root.Max;
    }

    /// <summary>
    /// How many lights the root tree node's flags can turn on
    /// </summary>
    public const Int32 MaxLights = 128;

    public Int32 LightCount => AmbientLights.Count + DirectionalLights.Count + PointLights.Count + SpotLights.Count;

    /// <summary>
    /// The game only gathers the lights whose bit the root tree node has (<c>FUN_001c7f50</c>, the scenery's order of lights), and every
    /// retail scenery's root has the bits of all of its lights and no others. Built that way: a light put in lights the level
    /// </summary>
    internal Boolean[] RootLights()
    {
        var lights = new Boolean[MaxLights];
        for (var i = 0; i < Math.Min(LightCount, MaxLights); i++)
        {
            lights[i] = true;
        }

        return lights;
    }

    /// <summary>
    /// The tree the game culls the scenery with, made from where its meshes and LODs are
    /// </summary>
    internal List<TwinSceneryBaseType> BuildTree(Func<SceneryPlacement, UInt32> idOf)
    {
        return SceneryTree.Build(RootCell, TreeDepth, Placements, TreeNodes, RootLights(), idOf);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        CheckCount("lights", LightCount, MaxLights);
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

        var tree = BuildTree(placement => assetManager.GetAsset(placement.Model).ExportTwinID);
        writer.Write(tree.Count);
        foreach (var node in tree)
        {
            writer.Write((Int32)node.GetObjectIndex());
            node.Write(writer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateScenery(ms);
    }

    private void WarnAboutCollisionOutsideBounds()
    {
        var assetManager = AssetManager.Get();
        if (Collision == LabURI.Empty || !assetManager.DoesAssetExist(Collision))
        {
            return;
        }

        const Single play = 0.01f;
        var outside = UsedVertexes(assetManager.GetAssetData<CollisionData>(Collision))
            .Count(vertex => vertex.X < BoundsMin.X - play || vertex.Y < BoundsMin.Y - play || vertex.Z < BoundsMin.Z - play ||
                             vertex.X > BoundsMax.X + play || vertex.Y > BoundsMax.Y + play || vertex.Z > BoundsMax.Z + play);
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

        var meshSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_MESHES_SECTION);
        var lodSection = graphicsSection.GetItem<ITwinSection>(Constants.GRAPHICS_LODS_SECTION);
        foreach (var placement in Placements)
        {
            assetManager.GetAsset(placement.Model).ResolveChunkResources(factory, placement.IsLod ? lodSection : meshSection);
        }

        return base.ResolveChunkResources(factory, section, Constants.SCENERY_SECENERY_ITEM, layoutId);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var result = new List<ViewportObject>();
        var renderContext = viewportContext.RenderContext;
        // The chunk's lights light its objects
        void ApplyLights()
        {
            renderContext.Lights = SceneLights.Of(this);
        }

        ApplyLights();
        var editingObject = new EditableObject(renderContext, null, $"SCENERY_{Owner.FullDataPath}")
        {
            IsSelectable = false
        };
        PropertyNode?[] lighting = [property.Find($"[data].AssetData.{nameof(AmbientLights)}"), property.Find($"[data].AssetData.{nameof(DirectionalLights)}"),
            property.Find($"[data].AssetData.{nameof(PointLights)}"), property.Find($"[data].AssetData.{nameof(SpotLights)}")];
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
        result.AddRange(GetPlacementObjects(viewportContext, property));
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
            var collisionData = AssetManager.Get().GetAssetData<CollisionData>(Collision);
            var shape = property.Find($"[data].AssetData.{nameof(CollisionShape)}");
            // Shown or hidden by the Collision layer: hidden here as well, the scenery's objects made again with the layer on (a mesh
            // placed or deleted in the scenery mode) left it hidden while the layer was ticked
            result.Add(new ViewportObject(collisionEditing, $"COLLISION_{property.Path}", property, collisionData)
            {
                Category = ViewportObjectCategory.Collision,
                RenderDependencies = shape == null ? [] : [shape],
                // Edited triangles go into the buffer every copy of the collision's mesh draws
                Refresh = () =>
                {
                    renderContext.QueueRenderAction(() => renderContext.MeshFactory.RebuildCollisionMesh(collisionVisual, collisionData));
                    return true;
                },
            });
        }

        return result;
    }

    // Every placed mesh on its own, picked and edited in the viewport's scenery mode like an instance, a LOD drawn with its closest mesh
    private List<ViewportObject> GetPlacementObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var result = new List<ViewportObject>();
        var list = property.Find($"[data].AssetData.{nameof(Placements)}");
        for (var i = 0; i < Placements.Count; i++)
        {
            var placement = Placements[i];
            var element = list?.Children.ElementAtOrDefault(i);
            if (CreatePlacementVisual(viewportContext.RenderContext, placement, $"{placement.Description} (placed mesh {i})") is not { } editable)
            {
                continue;
            }

            result.Add(new ViewportObject(editable, $"SCENERY_PLACEMENT_{element?.Path ?? i.ToString()}", property)
            {
                Transform = element?.FindChild($".{nameof(SceneryPlacement.Matrix)}"),
                Category = ViewportObjectCategory.Scenery,
                Mode = ViewportEditMode.Scenery,
                DuplicatedElement = element,
                InspectorRoot = element,
            });
        }

        return result;
    }

    /// <summary>
    /// What a placed mesh or LOD looks like in a viewport where it's placed: its mesh, a LOD's closest level's, in the box it's culled by.
    /// None for a LOD without levels or a mesh that can't be drawn
    /// </summary>
    internal static EditableObject? CreatePlacementVisual(RenderContext renderContext, SceneryPlacement placement, String name)
    {
        var model = placement.Model;
        if (placement.IsLod)
        {
            var lod = AssetManager.Get().GetAssetData<LodModelData>(placement.Model);
            if (lod.Meshes.Count == 0)
            {
                return null;
            }

            model = lod.Meshes[0];
        }

        var mesh = renderContext.MeshService.GetMesh(model, true).Model;
        if (mesh == null)
        {
            return null;
        }

        var min = new vec3(placement.Box.V1.X, placement.Box.V1.Y, placement.Box.V1.Z);
        var size = new vec3(placement.Box.V2.X, placement.Box.V2.Y, placement.Box.V2.Z) - min;
        // Darkened like a selected instance a mesh disappeared against the dark walls around it
        var editable = new EditableObject(renderContext, mesh, name, min, size) { SelectedColor = new vec4(1.0f, 0.78f, 0.5f, 1.0f) };
        editable.SetLocalTransform(placement.Matrix.ToGlm());
        return editable;
    }

    // The box the game keeps the chunk's objects in, picked and dragged by the handle on its top
    private ViewportObject? GetBoundsObject(ViewportContext viewportContext, PropertyNode property)
    {
        var bounds = property.Find($"[data].AssetData.{nameof(Bounds)}");
        var center = bounds?.FindChild($".{nameof(SceneryBounds.Center)}");
        var halfSize = bounds?.FindChild($".{nameof(SceneryBounds.HalfSize)}");
        if (bounds == null || center == null || halfSize == null)
        {
            return null;
        }

        var top = new SceneryBoundsTop(halfSize);
        var handle = new SceneryBoundsHandle(viewportContext.RenderContext, $"{Owner.FullDataPath}_BOUNDS");
        handle.SetPosition(top.ToPosition(center.GetValue()));
        handle.SetScale(SceneryBounds.HalfSizeOf(this));
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
