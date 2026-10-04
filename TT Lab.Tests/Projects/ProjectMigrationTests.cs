using System.IO.Compression;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Project;
using TT_Lab.Project.Migration;
using TT_Lab.Tests.Support;
using TT_Lab.ViewModels;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// Projects TT Lab 1.0.0 made open in this TT Lab once migrated, every file the migration changes kept as it was first
[Collection(ProjectCollection.Name)]
public sealed class ProjectMigrationTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private string ProjectFile => Path.Combine(_project.Project.ProjectPath, "Test.tson");

    // TT Lab 1.0.0's file of the tests' scenery then: the game's octree as nodes, a mesh in the root, a LOD in the node of its slot 0, a mesh
    // in that node's leaf of slot 5 and two in the root's leaf of slot 2
    private static string OldSceneryPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TT Lab.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory!.FullName, "TT Lab.Tests", "Projects", "Fixtures", "scenery_1_0_0.tlm");
    }

    private void WriteVersion(string version)
    {
        var json = JsonNode.Parse(File.ReadAllText(ProjectFile))!;
        json["Version"] = version;
        File.WriteAllText(ProjectFile, json.ToJsonString());
    }

    [Fact]
    public void ASceneryOfOneZeroZeroBecomesItsListOfPlacements()
    {
        var file = TlmFile.Load(OldSceneryPath());
        var root = file.Root!;

        Assert.True(Version110.MigrateScenery(root));
        // A file already migrated stays as it is
        Assert.False(Version110.MigrateScenery(root));

        Assert.Equal([SceneryData.MeshesKind, SceneryData.LodsKind, SceneryData.LightsKind, "collision", "dynamic_scenery"], root.GetChildren().Select(child => child.GetKind()));
        Assert.Equal(["Mesh 0", "Mesh 1", "Mesh 2", "Mesh 3"], root.GetChildren(SceneryData.MeshesKind).Single().GetChildren().Select(Name));
        var lod = Assert.Single(root.GetChildren(SceneryData.LodsKind).Single().GetChildren());
        Assert.Equal("LOD 0", Name(lod));
        Assert.Equal(["LOD 0 Level 0", "LOD 0 Level 1"], lod.GetChildren().Select(Name));
        var data = root.GetData();
        Assert.False(data.ContainsKey("HasLighting"));
        Assert.Equal(1, data.GetInt("TreeDepth"));
        Assert.Equal([-100f, -20f, -100f], data.GetFloats("BoundsMin"));
        Assert.Equal([100f, 20f, 100f], data.GetFloats("BoundsMax"));
        Assert.Equal(["", "0", "05", "2"], data.GetIndexed("TreeNodes").Select(node => node.GetString("Path")));
        // Every placement keeps the tree node it was in, in the order the tree had them
        Assert.Equal([("", 0), ("05", 2), ("2", 3), ("2", 4), ("0", 1)],
            root.GetChildren().Take(2).SelectMany(group => group.GetChildren()).Select(node => (node.GetData().GetString("Node"), node.GetData().GetInt("Order"))));

        AssertReadsAsTheTree(file);
    }

    // A node keeps light bits of its own where they aren't the root's (the build gives the root one for each light, 1.0.0 left the root's
    // stored bits as they were when lights changed), a node made in Blender has no values of the game to keep, and placements outside the
    // tree go after the tree's
    [Fact]
    public void TreeNodesKeepOnlyWhatTheBuildDoesNotWorkOut()
    {
        JsonArray Bits(params int[] on) => new(Enumerable.Range(0, 128).Select(i => (JsonNode?)JsonValue.Create(on.Contains(i))).ToArray());
        JsonObject Mesh(string name) => TlmNodes.Create(SceneryData.MeshInstanceKind, name, new JsonObject { ["Order"] = 0 });
        JsonObject TreeNode(int slot, JsonArray? lights, bool bounds, params JsonObject[] children)
        {
            var data = new JsonObject { ["Slot"] = slot };
            if (bounds)
            {
                foreach (var key in new[] { "BoundsCenter", "BoundsMin", "BoundsMax", "BoundsHalfSize" })
                {
                    data[key] = new JsonArray(0f, 0f, 0f, 1f);
                }
            }

            if (lights != null)
            {
                data["LightsEnabler"] = lights;
            }

            var node = TlmNodes.Create("tree_node", $"Node {slot}", data);
            foreach (var child in children)
            {
                node.AddChild(child);
            }

            return node;
        }

        var root = TlmNodes.Create(SceneryData.TlmKind, "Scenery", new JsonObject { ["HasLighting"] = true });
        root.AddChild(TreeNode(0, Bits(0, 5), true, Mesh("Mesh 0.0"), TreeNode(1, Bits(0, 1), true), TreeNode(3, Bits(1), true), TreeNode(5, null, false, Mesh("Mesh 3.0"))));
        root.AddChild(Mesh("Loose"));
        var lights = root.AddChild(TlmNodes.Create(SceneryData.LightsKind, "Lights"));
        lights.AddChild(TlmNodes.Create(SceneryData.AmbientLightKind, "ambient_light 0"));
        lights.AddChild(TlmNodes.Create(SceneryData.DirectionalLightKind, "directional_light 0"));

        Assert.True(Version110.MigrateScenery(root));

        var treeNodes = root.GetData().GetIndexed("TreeNodes");
        Assert.Equal(["", "1", "3"], treeNodes.Select(node => node.GetString("Path")));
        Assert.Equal([false, false, true], treeNodes.Select(node => node.ContainsKey("LightsEnabler")));
        Assert.Equal([("", 0), ("5", 1), (null, 2)],
            root.GetChildren(SceneryData.MeshesKind).Single().GetChildren().Select(mesh => (mesh.GetData().GetString("Node"), mesh.GetData().GetInt("Order"))));
    }

    // What the tree held, read the way TT Lab reads sceneries: it fits a placement's node to the tree's depth, the tests' tree of then was
    // only one deep with a leaf under a node
    private void AssertReadsAsTheTree(TlmFile file)
    {
        var read = ((IAsset)new TestAssets(_project).AddScenery()).GetData<SceneryData>();
        read.ReadTlm(file);
        Assert.Equal([(false, 0f, 0f, 0f), (true, -25f, 0f, -25f), (false, -75f, 0f, -75f), (false, 50f, 0f, 50f), (false, 60f, 5f, 60f)],
            read.Placements.Select(placement =>
            {
                var at = placement.Matrix.ToSystem().Translation;
                return (placement.IsLod, at.X, at.Y, at.Z);
            }));
        Assert.Equal((1u, -100f, 100f), (read.TreeDepth, read.BoundsMin.X, read.BoundsMax.X));
    }

    // The whole project: its file gets the version, the scenery's file its list, both kept in the backup as they were
    [Fact]
    public void AMigratedProjectKeepsWhatItChangedInItsBackup()
    {
        _project.Project.Serialize();
        var sceneryFile = Path.Combine(_project.AssetsPath, _project.Project.GlobalPackagePS2.Name, "levels", "test", "Scenery", "Scenery 0.tlm");
        Directory.CreateDirectory(Path.GetDirectoryName(sceneryFile)!);
        File.Copy(OldSceneryPath(), sceneryFile);
        WriteVersion("1.0.0");
        var oldProjectFile = File.ReadAllBytes(ProjectFile);
        Assert.True(ProjectMigration.CanMigrate("1.0.0"));

        var (changed, backup) = ProjectMigration.Migrate(ProjectFile);

        Assert.Equal(2, changed);
        Assert.Equal(TT_Lab.Project.Project.CURRENT_VERSION, ProjectMigration.ReadVersion(ProjectFile));
        Assert.False(ProjectMigration.CanMigrate(TT_Lab.Project.Project.CURRENT_VERSION));
        using (var archive = ZipFile.OpenRead(backup!))
        {
            Assert.Equal(oldProjectFile, Read(archive, "Test.tson"));
            Assert.Equal(File.ReadAllBytes(OldSceneryPath()), Read(archive, Path.GetRelativePath(_project.Project.ProjectPath, sceneryFile).Replace('\\', '/')));
        }

        AssertReadsAsTheTree(TlmFile.Load(sceneryFile));
    }

    // Opening an older project asks first, a project not migrated stays as it is and doesn't open
    [AvaloniaFact]
    public async Task OpeningAnOlderProjectAsksBeforeMigrating()
    {
        _project.Project.Serialize();
        WriteVersion("1.0.0");
        var before = File.ReadAllBytes(ProjectFile);
        var projectManager = Locator.Current.GetService<ProjectManager>()!;
        // Opening clears the log panel
        Log.SetViewModel(new LogViewModel(new TestProject.NullEventAggregator(), projectManager));
        var asked = new List<string>();
        projectManager.AskToMigrate = question =>
        {
            asked.Add(question);
            return Task.FromResult(false);
        };

        projectManager.OpenProject(_project.Project.ProjectPath);
        await WaitUntil(() => asked.Count == 1);
        await Task.Delay(200);

        Assert.Contains("1.0.0", Assert.Single(asked));
        Assert.Null(projectManager.OpenedProject);
        Assert.Equal(before, File.ReadAllBytes(ProjectFile));
        Assert.Empty(Directory.GetFiles(_project.Project.ProjectPath, "migration_backup_*"));

        projectManager.AskToMigrate = _ => Task.FromResult(true);
        projectManager.OpenProject(_project.Project.ProjectPath);
        await WaitUntil(() => projectManager.WorkableProject);

        Assert.Equal(TT_Lab.Project.Project.CURRENT_VERSION, projectManager.OpenedProject!.Version);
        Assert.Single(Directory.GetFiles(_project.Project.ProjectPath, "migration_backup_*"));
        Log.SetViewModel(null);
    }

    private static string Name(JsonObject node) => node.GetString(TlmNodes.NameKey) ?? string.Empty;

    private static byte[] Read(ZipArchive archive, string name)
    {
        using var stream = archive.GetEntry(name)!.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var waited = 0; waited < 20000 && !condition(); waited += 20)
        {
            await Task.Delay(20);
        }

        Assert.True(condition());
    }
}
