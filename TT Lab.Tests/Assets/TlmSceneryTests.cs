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
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
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

    // Meshes made in Blender have no tree node, the build puts them down the octants their middle is in while the octants' cells grown
    // twice hold them, a mesh outside of every octant into the root, which doesn't grow
    [Fact]
    public void MeshesAddedInBlenderGoWhereTheyAre()
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            var mesh = FindNode(root, node => node.GetKind() == SceneryData.MeshInstanceKind)[TlmNodes.MeshKey]!;
            root.AddChild(new JsonObject { ["name"] = "Rock", ["translation"] = new JsonArray(-90f, 0f, -90f), ["mesh"] = mesh.DeepClone() });
            root.AddChild(new JsonObject { ["name"] = "Far Rock", ["translation"] = new JsonArray(500f, 0f, 0f), ["mesh"] = mesh.DeepClone() });
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Equal("57", PlacedAt(data, new Vector3(-90, 0, -90)).Node);
        Assert.Equal("", PlacedAt(data, new Vector3(500, 0, 0)).Node);
        var tree = data.BuildTree(_ => 0);
        Assert.Equal((-100f, 100f), (tree[0].BoundsMin.X, tree[0].BoundsMax.X));
        Assert.NotEmpty(_assets.Export(scenery));
    }

    // A mesh moved out of its node goes where the build puts new ones, the node's left without anything and its kept values with it
    [Fact]
    public void MovedMeshesGoToTheTreeNodeTheyAreIn()
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            var mesh = FindNode(root, node => node.GetString("name") == "Mesh 1");
            mesh["translation"] = new JsonArray(70f, 0f, 70f);
            mesh.Remove("rotation");
            mesh.Remove("scale");
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Equal("02", PlacedAt(data, new Vector3(70, 0, 70)).Node);
        Assert.DoesNotContain(data.Placements, placement => placement.Node!.StartsWith("57"));
        Assert.DoesNotContain(_assets.Reload<SceneryData>(scenery).TreeNodes, node => node.Path == "57");
    }

    // Blender shows the placed meshes under the root's Meshes and the LODs under its LODs, each numbered in its group and keeping its
    // place among all of them
    [Fact]
    public void PlacedMeshesAndLodsAreInTheirGroups()
    {
        var root = ((IAsset)_assets.AddScenery()).GetData<SceneryData>().WriteTlm().Root!;

        var meshes = root.FindChild(SceneryData.MeshesKind)!;
        var lods = root.FindChild(SceneryData.LodsKind)!;
        Assert.Equal(("Meshes", "LODs"), (meshes.GetString("name"), lods.GetString("name")));
        Assert.DoesNotContain(root.GetChildren(), node => node.GetKind() is SceneryData.MeshInstanceKind or SceneryData.LodInstanceKind);
        Assert.Equal(["Mesh 0", "Mesh 1", "Mesh 2", "Mesh 3"], meshes.GetChildren().Select(node => node.GetString("name")));
        Assert.All(meshes.GetChildren(), node => Assert.Equal(SceneryData.MeshInstanceKind, node.GetKind()));
        Assert.Equal([0, 1, 3, 4], meshes.GetChildren().Select(node => node.GetData().GetInt("Order")));
        var lod = Assert.Single(lods.GetChildren());
        Assert.Equal(("LOD 0", SceneryData.LodInstanceKind, 2), (lod.GetString("name"), lod.GetKind(), lod.GetData().GetInt("Order")));
        Assert.Equal(["LOD 0 Level 0", "LOD 0 Level 1"], lod.GetChildren().Select(node => node.GetString("name")));
    }

    // A LOD's level moved out of its LOD (into the Meshes in Blender) is a placed mesh of its own, it was left out
    [Fact]
    public void ALevelMovedOutOfItsLodIsAPlacedMesh()
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            var lod = FindNode(root, node => node.GetKind() == SceneryData.LodInstanceKind);
            var level = lod.GetChildren().Last();
            lod["children"]!.AsArray().Remove(level);
            root.FindChild(SceneryData.MeshesKind)!.AddChild(level);
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Equal(6, data.Placements.Count);
        var lod = Assert.Single(data.Placements, placement => placement.IsLod);
        Assert.Single(_assets.Get(lod.Model).GetData<LodModelData>().Meshes);
    }

    // A mesh keeps the node it was in while that node's cell grown twice still holds it, the root holds everything
    [Fact]
    public void MeshesStayInTheTreeNodeTheyWereIn()
    {
        var scenery = _assets.AddScenery();
        scenery.Serialize(SerializationFlags.SaveData);
        Edit(scenery, root =>
        {
            FindNode(root, node => node.GetString("name") == "Mesh 0")["translation"] = new JsonArray(40f, 0f, 40f);
            FindNode(root, node => node.GetString("name") == "LOD 0")["translation"] = new JsonArray(-10f, 0f, -10f);
        });

        var data = ((IAsset)scenery).GetData<SceneryData>();

        Assert.Equal("", PlacedAt(data, new Vector3(40, 0, 40)).Node);
        Assert.Equal("5", PlacedAt(data, new Vector3(-10, 0, -10)).Node);
    }

    // The values a node kept are what the build writes while they're within a hair of what it works out, with the node's own light bits
    [Fact]
    public void TreeNodesKeepTheGamesValues()
    {
        var scenery = _assets.AddScenery();
        var data = ((IAsset)scenery).GetData<SceneryData>();
        var kept = data.TreeNodes.Single(node => node.Path == "57");

        var read = _assets.Reload<SceneryData>(scenery);
        var leaf = read.BuildTree(_ => 0).Single(node => node.MeshIDs.Count == 1 && node is TwinSceneryLeaf);

        Assert.Equal(Tuple(kept.BoundsCenter), Tuple(leaf.BoundsCenter));
        Assert.Equal(Tuple(kept.BoundsMin), Tuple(leaf.BoundsMin));
        Assert.Equal(kept.LightsEnabler, leaf.LightsEnabler);
        // The kept values of a node the tree doesn't have aren't written, nor the ones that are what the build works out
        Assert.DoesNotContain(read.TreeNodes, node => node.Path == "33");
        Assert.DoesNotContain(read.TreeNodes, node => node.Path == "");
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

        Assert.Equal(2, data.Placements.Count);
        Assert.Equal(SceneryTree.DefaultDepth, data.TreeDepth);
        // The root is the box the game keeps the chunk's objects in, made like the game's around both (SceneryBounds)
        Assert.Equal((-200f, -100f, -200f, 200f, 100f, 200f), (data.BoundsMin.X, data.BoundsMin.Y, data.BoundsMin.Z, data.BoundsMax.X, data.BoundsMax.Y, data.BoundsMax.Z));
        Assert.All(data.Placements, placement => Assert.NotEmpty(placement.Node!));
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
        var treeNodes = 0;
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

                // The items of the scenery's lists
                foreach (var treeNode in node.GetData().GetIndexed("TreeNodes"))
                {
                    Assert.Subset(SchemaTests.KnownKeys("SceneryTreeNode", "Items")!, treeNode.Select(pair => pair.Key).ToHashSet());
                    treeNodes++;
                }

                foreach (var joint in node["joints"] as JsonArray ?? [])
                {
                    Check("Joint", joint!["data"]!.AsObject(), "Bone");
                }
            }
        }

        Assert.Superset(KindTypes.Values.OfType<string>().ToHashSet(), checkedTypes);
        Assert.NotEqual(0, treeNodes);

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
        ["exit_point"] = "ExitPoint", ["collision_hulls"] = null, ["hull"] = "CollisionHull", ["model"] = "Model", ["rigid_model"] = "RigidModel", ["mesh"] = "Mesh", ["scenery"] = "Scenery",
        ["scenery_meshes"] = null, ["scenery_mesh"] = "SceneryMesh", ["scenery_lods"] = null, ["scenery_lod"] = "SceneryLod", ["lod_mesh"] = "LodMesh",
        ["lights"] = null, ["ambient_light"] = "AmbientLight",
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

    private static SceneryPlacement PlacedAt(SceneryData data, Vector3 position) => data.Placements.Single(placement => placement.Matrix.ToSystem().Translation == position);

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
