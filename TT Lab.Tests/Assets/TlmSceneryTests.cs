using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.AssetData.Graphics.SubModels;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.DynamicScenery;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Tests.Support;
using TT_Lab.Util;
using Twinsanity.PS2Hardware;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.DynamicScenery;
using Twinsanity.TwinsanityInterchange.Common.Lights;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.SubItems;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using static TT_Lab.Tests.Support.TestGeometry;
using Material = TT_Lab.Assets.Graphics.Material;
using Mesh = TT_Lab.Assets.Graphics.Mesh;
using Path = System.IO.Path;
using Skin = TT_Lab.Assets.Graphics.Skin;
using Texture = TT_Lab.Assets.Graphics.Texture;
using Vector3 = System.Numerics.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.Tests.Assets;

// Sceneries, collisions and skydomes saved to their TT Lab model file and loaded back from it export to the same bytes as before
[Collection(ProjectCollection.Name)]
public sealed class TlmSceneryTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly TestAssets _assets;

    public TlmSceneryTests()
    {
        _assets = new TestAssets(_project);
    }

    public void Dispose() => _project.Dispose();

    [Fact]
    public void CollisionKeepsItsOrder()
    {
        var (collision, data) = _assets.AddCollision(_project.Add(new Collision { Chunk = "levels/test" }, "Collision"));
        var vectors = data.Vertexes.Select(Tuple).ToList();
        var triangles = data.Triangles.Select(Tuple).ToList();
        var before = _assets.Export(collision);

        var read = _assets.Reload<CollisionData>(collision);

        Assert.Equal(vectors, read.Vertexes.Select(Tuple));
        Assert.Equal(triangles, read.Triangles.Select(Tuple));
        Assert.Equal(before, _assets.Export(collision));
    }

    [Fact]
    public void CollisionEditedInBlenderIsReadFromItsMesh()
    {
        var (collision, data) = _assets.AddCollision(_project.Add(new Collision { Chunk = "levels/test" }, "Collision"));
        var triangles = Placed(data);
        var usedVectors = data.Vertexes.Count - 1;
        collision.Serialize(SerializationFlags.SaveData);
        // Faces added or removed in Blender leave the order TT Lab wrote behind
        Edit(collision, root =>
        {
            foreach (var part in root["surfaces"]!.AsArray().OfType<JsonObject>())
            {
                part.Remove("triangles");
                part.Remove("vertexes");
            }
        });

        var read = ((IAsset)collision).GetData<CollisionData>();

        Assert.Equal(triangles, Placed(read));
        Assert.Equal(usedVectors, read.Vertexes.Count);
    }

    [Fact]
    public void SceneryKeepsItsTreeAndEverythingInIt()
    {
        var scenery = _assets.AddScenery();
        var data = ((IAsset)scenery).GetData<SceneryData>();
        var before = _assets.Export(scenery);
        var collisionBefore = _assets.Export(_assets.Get(data.Collision));
        var dynamicSceneryBefore = _assets.Export(_assets.Get(data.DynamicScenery));

        var read = _assets.Reload<SceneryData>(scenery);

        Assert.Equal(before, _assets.Export(scenery));
        Assert.Equal(collisionBefore, _assets.Export(_assets.Get(read.Collision)));
        Assert.Equal(dynamicSceneryBefore, _assets.Export(_assets.Get(read.DynamicScenery)));
    }

    // A light's node points its Z axis along the light's direction: turning the empty in Blender turns the light, the stored direction
    // is kept while the node still points along it
    [Fact]
    public void LightsPointTheirNodesAlongTheirDirections()
    {
        var scenery = _assets.AddScenery();
        var before = _assets.Export(scenery);
        var direction = ((IAsset)scenery).GetData<SceneryData>().DirectionalLights[0].Direction;

        var read = _assets.Reload<SceneryData>(scenery);
        Assert.Equal(before, _assets.Export(scenery));
        Assert.Equal((direction.X, direction.Y, direction.Z, direction.W), (read.DirectionalLights[0].Direction.X, read.DirectionalLights[0].Direction.Y, read.DirectionalLights[0].Direction.Z, read.DirectionalLights[0].Direction.W));

        Edit(scenery, root =>
        {
            // Turned half a turn about Y, the arrow now points along -Z
            FindNode(root, node => node["kind"]?.ToString() == SceneryData.DirectionalLightKind)["rotation"] = new JsonArray(0.0f, 1.0f, 0.0f, 0.0f);
            FindNode(root, node => node["kind"]?.ToString() == SceneryData.SpotLightKind)["rotation"] = new JsonArray(0.70710677f, 0.0f, 0.0f, 0.70710677f);
        });
        var turned = ((IAsset)scenery).GetData<SceneryData>();

        var directional = turned.DirectionalLights[0].Direction;
        var spot = turned.SpotLights[0].Direction;
        Assert.Equal((0f, 0f, -1f, 0f), (MathF.Round(directional.X, 4), MathF.Round(directional.Y, 4), MathF.Round(directional.Z, 4), directional.W));
        // A quarter turn about X takes Z to -Y
        Assert.Equal((0f, -1f, 0f, 0f), (MathF.Round(spot.X, 4), MathF.Round(spot.Y, 4), MathF.Round(spot.Z, 4), spot.W));
    }

    // Edited cone angles give the cosines the game lights with, unedited ones keep the game's
    [Fact]
    public void SpotLightConesEditedInBlenderGetNewCosines()
    {
        var scenery = _assets.AddScenery();
        var data = ((IAsset)scenery).GetData<SceneryData>();
        data.SpotLights[0].SetCone(104.128f, 5.037f);
        data.SpotLights[0].InnerConeCosine = 0.615f;
        var before = _assets.Export(scenery);

        var read = _assets.Reload<SceneryData>(scenery);
        Assert.Equal(before, _assets.Export(scenery));
        Assert.Equal(0.615f, read.SpotLights[0].InnerConeCosine);

        Edit(scenery, root =>
        {
            var light = FindNode(root, node => node["kind"]?.ToString() == SceneryData.SpotLightKind)["data"]!.AsObject();
            light["ConeAngle"] = 10923;
            light["FalloffAngle"] = 1820;
        });
        var edited = ((IAsset)scenery).GetData<SceneryData>().SpotLights[0];

        Assert.Equal((10923u, 1820u), (edited.ConeAngle, edited.FalloffAngle));
        Assert.Equal(MathF.Cos(30 * MathF.PI / 180), edited.InnerConeCosine, 1e-4f);
        Assert.Equal(MathF.Cos(40 * MathF.PI / 180), edited.OuterConeCosine, 1e-4f);
    }

    [Fact]
    public void MeshesAddedInBlenderJoinTheTree()
    {
        var scenery = _assets.AddScenery();
        var leafBounds = Bounds(((IAsset)scenery).GetData<SceneryData>().Sceneries[2]);
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            var mesh = FindNode(root, node => node.GetKind() == SceneryData.MeshInstanceKind)[TlmNodes.MeshKey]!;
            root.AddChild(new JsonObject { ["name"] = "Rock", ["translation"] = new JsonArray(-90f, 0f, -90f), ["mesh"] = mesh.DeepClone() });
            root.AddChild(new JsonObject { ["name"] = "Far Rock", ["translation"] = new JsonArray(500f, 0f, 0f), ["mesh"] = mesh.DeepClone() });
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        // The first rock lies in the leaf covering where it is and fits into it, the far one only fits into the root once it grows
        Assert.Contains(data.Sceneries[2].MeshModelMatrices, matrix => matrix.ToSystem().Translation == new Vector3(-90, 0, -90));
        Assert.Equal(leafBounds, Bounds(data.Sceneries[2]));
        Assert.Contains(data.Sceneries[0].MeshModelMatrices, matrix => matrix.ToSystem().Translation == new Vector3(500, 0, 0));
        Assert.True(data.Sceneries[0].BoundsMax.X > 510);
        Assert.Equal(data.Sceneries[0].MeshIDs.Count + data.Sceneries[0].LodIDs.Count, data.Sceneries[0].BoundingBoxes.Count);
    }

    [Fact]
    public void MovedMeshesGoToTheTreeNodeTheyAreIn()
    {
        var scenery = _assets.AddScenery();
        var leafBounds = Bounds(((IAsset)scenery).GetData<SceneryData>().Sceneries[2]);
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            var mesh = FindNode(root, node => node.GetString("name") == "Mesh 2.0");
            mesh["translation"] = new JsonArray(70f, 0f, 70f);
            mesh.Remove("rotation");
            mesh.Remove("scale");
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Empty(data.Sceneries[2].MeshIDs);
        Assert.Equal(leafBounds, Bounds(data.Sceneries[2]));
        Assert.Contains(data.Sceneries[3].MeshModelMatrices, matrix => matrix.ToSystem().Translation == new Vector3(70, 0, 70));
    }

    [Fact]
    public void MeshesStayInTheTreeNodeTheyWerePutUnder()
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            var mesh = FindNode(root, node => node.GetKind() == SceneryData.MeshInstanceKind)[TlmNodes.MeshKey]!;
            var leaf = FindNode(root, node => node.GetString("name") == "Leaf 3");
            leaf.AddChild(new JsonObject { ["name"] = "Inside", ["translation"] = new JsonArray(80f, 0f, 80f), ["mesh"] = mesh.DeepClone() });
            leaf.AddChild(new JsonObject { ["name"] = "Outside", ["translation"] = new JsonArray(-75f, 0f, -75f), ["mesh"] = mesh.DeepClone() });
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Contains(data.Sceneries[3].MeshModelMatrices, matrix => matrix.ToSystem().Translation == new Vector3(80, 0, 80));
        Assert.Contains(data.Sceneries[2].MeshModelMatrices, matrix => matrix.ToSystem().Translation == new Vector3(-75, 0, -75));
    }

    [Fact]
    public void TreeNodesMadeInBlenderJoinTheTree()
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            var mesh = FindNode(root, node => node.GetKind() == SceneryData.MeshInstanceKind)[TlmNodes.MeshKey]!;
            var node = FindNode(root, n => n.GetString("name") == "Scenery Tree").AddChild(TlmNodes.Create(SceneryData.TreeNodeKind, "New Node"));
            node.AddChild(new JsonObject { ["name"] = "Rock", ["translation"] = new JsonArray(50f, 0f, -50f), ["mesh"] = mesh.DeepClone() });
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Equal(5, data.Sceneries.Count);
        var added = data.Sceneries.Single(treeNode => treeNode.MeshModelMatrices.Any(matrix => matrix.ToSystem().Translation == new Vector3(50, 0, -50)));
        Assert.IsType<SceneryLeafData>(added);
        // The node takes the box of what got placed in it
        var box = added.BoundingBoxes[0];
        Assert.Equal((box.V1.X + 50, box.V1.Z - 50, box.V2.X + 50, box.V2.Z - 50), (added.BoundsMin.X, added.BoundsMin.Z, added.BoundsMax.X, added.BoundsMax.Z));
        Assert.Equal(ITwinScenery.SceneryType.Leaf, ((SceneryRootData)data.Sceneries[0]).SceneryTypes[1]);
        Assert.NotEmpty(_assets.Export(scenery));
    }

    [Fact]
    public void SceneryMadeInBlenderGetsATreeOfItsOwn()
    {
        var scenery = _project.Add(new Scenery { Chunk = "levels/test" }, "Scenery 0");
        var file = new TlmFile(SceneryData.TlmAssetType, "Scenery 0");
        var mesh = ((IAsset)_assets.AddMesh("Rock")).GetData<MeshData>().WriteTlmMesh(file, new TlmMaterials(file));
        var root = TlmNodes.Create(SceneryData.TlmKind, "Scenery 0");
        root.AddChild(new JsonObject { ["name"] = "Rock", ["translation"] = new JsonArray(10f, 0f, 0f), ["mesh"] = mesh });
        root.AddChild(new JsonObject { ["name"] = "Rock.001", ["translation"] = new JsonArray(-10f, 0f, 5f), ["mesh"] = mesh.DeepClone() });
        file.Root = root;
        Directory.CreateDirectory(Path.GetDirectoryName(scenery.FullDataPath)!);
        file.Save(scenery.FullDataPath);

        var data = ((IAsset)scenery).GetData<SceneryData>();

        var tree = Assert.Single(data.Sceneries);
        Assert.Equal(2, tree.MeshIDs.Count);
        Assert.Equal(2, tree.BoundingBoxes.Count);
        // The root grew from its default size to hold both
        var box = tree.BoundingBoxes[0];
        Assert.Equal(-10 + box.V1.X, tree.BoundsMin.X, 1e-4f);
        Assert.Equal(10 + box.V2.X, tree.BoundsMax.X, 1e-4f);
        Assert.Equal(5 + box.V2.Z, tree.BoundsMax.Z, 1e-4f);
        Assert.NotEmpty(_assets.Export(scenery));
    }

    // A dynamic model's movement stays as the game has it while its keys are its values, edited keys make the movement anew
    [Fact]
    public void DynamicModelsMoveTheWayTheirKeysSay()
    {
        var scenery = _assets.AddScenery();
        var data = ((IAsset)scenery).GetData<SceneryData>();
        var model = _assets.Get(data.DynamicScenery).GetData<DynamicSceneryData>().DynamicModels[0];
        model.ReadAnimationFromKeys(3, [0, 0, 0, 1, 0, 0, 2, 0, 0], [0, 0, 0, 0, 0.5f, 0, 0, 1, 0]);
        var before = _assets.Export(_assets.Get(data.DynamicScenery));
        scenery.Serialize(SerializationFlags.SaveData);
        data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Equal(before, _assets.Export(_assets.Get(data.DynamicScenery)));

        Edit(scenery, root =>
        {
            var animation = FindNode(root, node => node.GetKind() == DynamicSceneryModelData.TlmKind)["animation"]!.AsObject();
            animation["translation"] = _file!.Write(new[] { 0f, 0, 0, 1, 0, 0, 3, 0, 0 }.AsSpan());
        });
        data = ((IAsset)scenery).GetData<SceneryData>();

        var moved = _assets.Get(data.DynamicScenery).GetData<DynamicSceneryData>().DynamicModels[0].GetAnimationSamples();
        Assert.Equal([0f, 1f, 3f], moved.Select(sample => sample.Translation.Item2.X));
        // Turned by 1 around Y
        Assert.Equal(MathF.Sin(0.5f), moved[2].Rotation.Item2.Y, 5);
    }

    [Fact]
    public void SkydomesKeepTheirMeshes()
    {
        var skydome = _project.Add(new Skydome(), "Sky");
        var data = new SkydomeData(skydome);
        data.Meshes.AddRange([_assets.AddMesh("Clouds").URI, _assets.AddMesh("Sun").URI]);
        skydome.SetData(data);
        var before = _assets.Export(skydome);

        _assets.Reload<SkydomeData>(skydome);

        Assert.Equal(before, _assets.Export(skydome));
    }

    [AvaloniaFact]
    public void EverythingWrittenIsKnownToTheBlenderAddOn()
    {
        var skydome = _project.Add(new Skydome(), "Sky");
        var skydomeData = new SkydomeData(skydome);
        skydomeData.Meshes.Add(_assets.AddMesh("Clouds").URI);
        skydome.SetData(skydomeData);
        var material = _assets.AddMaterial("Fur");
        IAsset[] assets = [_assets.AddScenery(), skydome, _assets.AddOgi(), _assets.AddModel("Rock"), _assets.AddRigidModel("Chair", material.URI), _assets.AddMesh("Stone"),
            _assets.AddSkin(material.URI, "Skin"), _assets.AddBlendSkin(material.URI, "Face"), _assets.AddSaveIcon()];
        var checkedTypes = new HashSet<string>();
        foreach (var asset in assets)
        {
            asset.Serialize(SerializationFlags.SaveData);
            var file = TlmFile.Load(asset.FullDataPath);
            foreach (var node in file.Root!.Traverse())
            {
                var kind = node.GetKind()!;
                Assert.True(KindTypes.ContainsKey(kind), $"The add-on doesn't know {kind}");
                if (KindTypes[kind] is { } type)
                {
                    Check(type, node.GetData(), "Object");
                }

                foreach (var joint in node["joints"] as JsonArray ?? [])
                {
                    Check("Joint", joint!["data"]!.AsObject(), "Bone");
                }
            }
        }

        Assert.Superset(KindTypes.Values.OfType<string>().ToHashSet(), checkedTypes);

        void Check(string type, JsonObject data, string element)
        {
            var known = SchemaTests.KnownKeys(type, element);
            Assert.True(known != null, $"The add-on doesn't know {type}");
            Assert.Subset(known, data.Select(pair => pair.Key).ToHashSet());
            checkedTypes.Add(type);
        }
    }

    // The node kinds of TT Lab model files and the add-on's type for their data, like its tlm_blender.KIND_TYPES
    private static readonly Dictionary<string, string?> KindTypes = new()
    {
        ["ogi"] = "Ogi", ["armature"] = null, ["skin"] = "Skin", ["shape"] = "BlendSkin", ["rigid_bodies"] = null, ["body"] = "Body", ["exit_points"] = null,
        ["exit_point"] = "ExitPoint", ["collision_hulls"] = null, ["hull"] = "CollisionHull", ["model"] = "Model", ["rigid_model"] = "RigidModel", ["mesh"] = "Mesh", ["scenery"] = "Scenery", ["tree_node"] = "SceneryTreeNode",
        ["scenery_mesh"] = "SceneryMesh", ["scenery_lod"] = "SceneryLod", ["lod_mesh"] = "LodMesh", ["lights"] = null, ["ambient_light"] = "AmbientLight",
        ["directional_light"] = "DirectionalLight", ["point_light"] = "PointLight", ["spot_light"] = "SpotLight", ["collision"] = "Collision",
        ["dynamic_scenery"] = "DynamicScenery", ["dynamic_model"] = "DynamicSceneryModel", ["skydome"] = "Skydome", ["skydome_mesh"] = "SkydomeMesh",
        ["save_icon"] = "SaveIcon"
    };

    private TlmFile? _file;

    // Edits the asset's file the way Blender would, with the asset's data unloaded the way TT Lab does it when no editor uses it
    private void Edit(IAsset asset, Action<JsonObject> edit)
    {
        _file = TlmFile.Load(asset.FullDataPath);
        edit(_file.Root!);
        _file.Save(asset.FullDataPath);
        asset.UnloadData();
        _project.AssetManager.RemoveOrphanedInternalAssets(new HashSet<LabURI>());
    }

    private static JsonObject FindNode(JsonObject root, Func<JsonObject, bool> predicate) => root.Traverse().First(predicate);

    private static (float, float, float, float, float, float) Bounds(SceneryBaseData node)
    {
        return (node.BoundsMin.X, node.BoundsMin.Y, node.BoundsMin.Z, node.BoundsMax.X, node.BoundsMax.Y, node.BoundsMax.Z);
    }

    [Fact]
    public void CollisionSurfacesKeepTheirColors()
    {
        var (collision, _) = _assets.AddCollision(_project.Add(new Collision { Chunk = "levels/test" }, "Collision"));
        collision.Serialize(SerializationFlags.SaveData);

        var colors = TlmFile.Load(collision.FullDataPath).Root!["surfaces"]!.AsArray().OfType<JsonObject>()
            .ToDictionary(surface => surface.GetString("name")!, surface => surface.GetFloats("color"));

        var floor = CollisionSurface.DefaultColors[1];
        Assert.Equal(new[] { floor.R / 255.0f, floor.G / 255.0f, floor.B / 255.0f, floor.A / 255.0f }, colors["Floor"]);
        Assert.NotEqual(colors["Floor"], colors["Wall"]);
    }

    // Every triangle by its corners and surface, turned to start at its smallest corner
    private static List<string> Placed(CollisionData data)
    {
        return data.Triangles.Select(triangle =>
        {
            var corners = triangle.Face.Indexes!.Select(index => $"{data.Vertexes[index].X},{data.Vertexes[index].Y},{data.Vertexes[index].Z}").ToArray();
            var first = Array.IndexOf(corners, corners.Min(StringComparer.Ordinal));
            return $"{corners[first]} {corners[(first + 1) % 3]} {corners[(first + 2) % 3]} {triangle.Surface}";
        }).Order(StringComparer.Ordinal).ToList();
    }

    private static (uint, uint, uint, uint) Tuple(Vector4 vector) => (vector.GetBinaryX(), vector.GetBinaryY(), vector.GetBinaryZ(), vector.GetBinaryW());

    private static (int, int, int, LabURI) Tuple(CollisionTriangle triangle) => (triangle.Face.Indexes![0], triangle.Face.Indexes[1], triangle.Face.Indexes[2], triangle.Surface);
}
