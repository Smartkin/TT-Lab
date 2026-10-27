using System.Text;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code;
using Twinsanity.TwinsanityInterchange.Implementations.Xbox.Items.RMX.Code.AgentLab;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// The game reuses graph IDs for other scripts in other chunks (0xBED is COM_PIRATE_MISSILE_INIT in High Seas and
// COM_GENERIC_FURNITURE_WAKE_CONTACT_RESPONSE in AltEarth) and two of its starters have another ID than the one before their graph's
// (AltEarth's 0xBFE). Unpacking linked every object to the first chunk's graph of an ID (a variant is named after the first chunk having
// it, an object belongs to no chunk) and gave every graph of an ID the first chunk's starter: High Seas' build got AltEarth's script, a
// slot naming the other starter was left empty, and the builds dropped the starters whose ID isn't the one before their graph's. What a
// chunk's objects name is the chunk's own, a graph's starter its chunk's, else another chunk's of the same script, and the build gives the
// game's starter its own ID. A graph's state ran the first chunk's graph of the ID too (docamok3's bear ran l03stock's platform in a project
// made of a disc TT Lab built, whose archive has l03stock first): it runs its chunk's
[Collection(ProjectCollection.Name)]
public sealed class ReusedGraphIdTests : IDisposable
{
    private const UInt32 Reused = 0x31;
    private const UInt32 MissileStarter = 0x30;
    private const UInt32 FurnitureStarter = 0x3E;
    private const UInt32 BearOrPlatform = 0x51;
    private const UInt32 PlatformStarter = 0x50;
    private const UInt32 BearDefault = 0x53;
    private const UInt32 PlatformDefault = 0x55;

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static PS2ItemFactory FactoryOf(GamePlatform platform) => platform == GamePlatform.Xbox ? new XboxItemFactory() : new PS2ItemFactory();

    // The graph of one state and, when it has one, its starter, compiled for the version the way the game's files have them: the starter
    // of the ID given, its assigner running the graph. The state runs the graph of the child's ID given
    private static List<ITwinItem> Graph(GamePlatform platform, string name, UInt32 id, UInt32? starterId, UInt32? child = null)
    {
        var script = $"behaviour {name} {{\n" + (starterId != null ? "   starter {\n      assigner = {\n         AssignType = ME;\n      }\n   }\n" : "") +
                     "   state S() {\n   }\n}\n";
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write((Int32)id);
            writer.Write(Encoding.UTF8.GetBytes(script));
        }

        stream.Position = 0;
        var result = FactoryOf(platform).GenerateBehaviourGraph(stream);
        Assert.False(result.CompilerStatus.IsError, result.CompilerStatus.Message);
        var graph = result.Get<ITwinBehaviourGraph>();
        graph.SetID(id);
        // The game's files have the child's ID, a script names it the way its project finds it
        if (child != null)
        {
            graph.ScriptStates[0].BehaviourIndexOrSlot = (Int16)child.Value;
        }

        if (starterId == null)
        {
            return [graph];
        }

        var starter = result.Get<TwinBehaviourStarter>();
        starter.SetID(starterId.Value);
        starter.Compile(id);
        return [starter, graph];
    }

    private static ITwinObject Object(GamePlatform platform, string name, UInt16 id, UInt16[] slots, UInt16[] references, UInt16? message = null)
    {
        PS2AnyObject gameObject = platform == GamePlatform.Xbox ? new XboxAnyObject() : new PS2AnyObject();
        gameObject.Name = name;
        gameObject.BehaviourSlots = [..slots];
        gameObject.RefBehaviours = [..references];
        gameObject.FloatProperties = [1];
        gameObject.IntProperties = [0, 255];
        if (message != null)
        {
            gameObject.TriggerBehaviours = [new TwinObjectTriggerBehaviour { MessageID = 5, TriggerBehaviour = message.Value }];
        }

        gameObject.BehaviourPack = platform == GamePlatform.Xbox ? new XboxBehaviourCommandPack() : new PS2BehaviourCommandPack();
        gameObject.SetID(id);
        return gameObject;
    }

    // A level's resource file of the behaviours and objects, its collision and particles empty (the unpack reads them of every chunk)
    private static ITwinSection Chunk(GamePlatform platform, IEnumerable<ITwinItem> behaviours, params ITwinObject[] objects)
    {
        var chunk = FactoryOf(platform).GenerateRM();
        var code = chunk.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var behaviourSection = code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        foreach (var behaviour in behaviours)
        {
            behaviourSection.AddItem(behaviour);
        }

        var objectSection = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        foreach (var gameObject in objects)
        {
            objectSection.AddItem(gameObject);
        }

        var isXbox = platform == GamePlatform.Xbox;
        var collision = isXbox ? new XboxAnyCollisionData { Version = PS2AnyCollisionData.GameVersion } : new PS2AnyCollisionData { Version = PS2AnyCollisionData.GameVersion };
        collision.SetID(Constants.LEVEL_COLLISION_ITEM);
        chunk.AddItem(collision);
        var particles = isXbox ? new XboxAnyParticleData() : new PS2AnyParticleData();
        particles.SetID(Constants.LEVEL_PARTICLES_ITEM);
        chunk.AddItem(particles);
        return chunk;
    }

    private string Unpack(GamePlatform platform) => Unpack(platform, ReusedIdChunks(platform));

    // In the PS2 archive's order High Seas' missile comes first, the Xbox's files are in their paths' order and AltEarth's furniture does:
    // whichever graph of the ID comes first is the asset, the other the variant. The bear's move of 0x51 comes after the totem's platform's
    // in both and has no starter anywhere, the platform's starter of the ID before it. Each of their chunks has a graph whose state runs 0x51
    private static List<(string Path, ITwinSection Chunk)> ReusedIdChunks(GamePlatform platform)
    {
        var extension = platform == GamePlatform.Xbox ? "rmx" : "rm2";
        return new List<(string Path, ITwinSection Chunk)>
        {
            ($"Levels\\Ice\\HighSeas\\gpa04.{extension}", Chunk(platform, Graph(platform, "COM_PIRATE_MISSILE_INIT", Reused, MissileStarter),
                Object(platform, "MISSILE", 0x60, [(UInt16)MissileStarter], [(UInt16)MissileStarter, (UInt16)Reused]))),
            ($"Levels\\AltEarth\\Core\\corea.{extension}", Chunk(platform, Graph(platform, "COM_FURNITURE", Reused, FurnitureStarter),
                Object(platform, "SPHERE", 0x61, [], [(UInt16)FurnitureStarter, (UInt16)Reused], (UInt16)FurnitureStarter),
                Object(platform, "SPAWNER", 0x62, [], [(UInt16)FurnitureStarter, (UInt16)Reused]))),
            // Another chunk of the furniture, its sphere of another name a variant the merge takes the place of
            ($"Levels\\AltEarth\\Core\\coreb.{extension}", Chunk(platform, Graph(platform, "COM_FURNITURE", Reused, FurnitureStarter),
                Object(platform, "SPHERE_B", 0x61, [], [(UInt16)FurnitureStarter, (UInt16)Reused], (UInt16)FurnitureStarter))),
            ($"Levels\\Earth\\Totem\\l03stock.{extension}", Chunk(platform, Graph(platform, "COM_FLIP_FLOP_PLATFORM", BearOrPlatform, PlatformStarter)
                    .Concat(Graph(platform, "COM_PLATFORM_DEFAULT", PlatformDefault, null, BearOrPlatform)),
                Object(platform, "PLATFORM", 0x64, [(UInt16)PlatformStarter], [(UInt16)PlatformStarter, (UInt16)BearOrPlatform]))),
            ($"Levels\\Earth\\Zoo\\bearpit.{extension}", Chunk(platform, Graph(platform, "COM_BEAR_MOVE", BearOrPlatform, null)
                    .Concat(Graph(platform, "COM_BEAR_DEFAULT", BearDefault, null, BearOrPlatform)),
                Object(platform, "BEAR", 0x63, [], [(UInt16)BearOrPlatform])))
        };
    }

    // Unpacked, written and merged the way creating a project does it, the chunks in the order the version's disc has them (the PS2 archive
    // as given, the Xbox's files by their paths): the version's package's folder
    private string Unpack(GamePlatform platform, List<(string Path, ITwinSection Chunk)> chunks)
    {
        if (platform == GamePlatform.Xbox)
        {
            chunks = chunks.OrderBy(chunk => chunk.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var files = chunks.Select(chunk =>
        {
            var bytes = TestGeometry.Serialize(chunk.Chunk);
            return new TT_Lab.Project.Project.DiscFile(chunk.Path, () => bytes);
        }).ToList();
        var project = _project.Project;
        var (globalPackage, package) = platform == GamePlatform.Xbox ? (project.GlobalPackageXbox, project.XboxPackage) : (project.GlobalPackagePS2, project.Ps2Package);
        globalPackage.Enabled = true;
        package.Enabled = true;
        using var gate = new MemoryGate(1L << 30);
        var writer = new CreationWriter(project.AssetManager, gate);

        project.UnpackAssets(platform, files, globalPackage, package, gate);
        writer.ImportAndWrite();
        writer.RemoveInternalAssets();
        new VariantMerger(project.AssetManager, _project.AssetsPath).Merge();
        project.Serialize(writer.IsWritten);

        return Path.Combine(_project.AssetsPath, package.Name);
    }

    // A graph's metadata and script, by its name
    private static (JObject Metadata, string Script) GraphOf(string package, string name)
    {
        var file = Directory.EnumerateFiles(Path.Combine(package, "BehaviourGraph"), "*.json")
            .Single(file => (string)JObject.Parse(File.ReadAllText(file))[nameof(IAsset.InvariantName)]! == name);
        return (JObject.Parse(File.ReadAllText(file)), File.ReadAllText(Path.ChangeExtension(file, ".lab")));
    }

    private static string UriOf(JObject metadata) => (string)metadata[nameof(IAsset.URI)]!["_uri"]!;

    private static JObject ObjectOf(string package, string name, UInt16 id) => JObject.Parse(File.ReadAllText(Path.Combine(package, "GameObject", $"{name}_{id:X}.data")));

    private static List<string> Links(JObject data, string list) => ((JArray)data[list]!).Select(link => (string)link["_uri"]!).ToList();

    [Theory]
    [InlineData(GamePlatform.PS2)]
    [InlineData(GamePlatform.Xbox)]
    public void ObjectsNameTheirChunksGraphsAndGraphsGetTheirChunksStarters(GamePlatform platform)
    {
        var package = Unpack(platform);

        var missile = GraphOf(package, "COM_PIRATE_MISSILE_INIT");
        var furniture = GraphOf(package, "COM_FURNITURE");
        var platformGraph = GraphOf(package, "COM_FLIP_FLOP_PLATFORM");
        var bear = GraphOf(package, "COM_BEAR_MOVE");
        // The graph of the ID the version's order has second is the variant
        var (first, second) = platform == GamePlatform.Xbox ? (furniture, missile) : (missile, furniture);
        Assert.True(string.IsNullOrEmpty((string?)first.Metadata[nameof(IAsset.Variation)]));
        Assert.False(string.IsNullOrEmpty((string?)second.Metadata[nameof(IAsset.Variation)]));
        Assert.False(string.IsNullOrEmpty((string?)bear.Metadata[nameof(IAsset.Variation)]));

        // The furniture's starter keeps its ID, the others have the one before their graph's
        Assert.Equal(FurnitureStarter, (UInt32)furniture.Metadata[nameof(BehaviourGraph.StarterId)]!);
        Assert.Null(missile.Metadata[nameof(BehaviourGraph.StarterId)]);
        Assert.Null(platformGraph.Metadata[nameof(BehaviourGraph.StarterId)]);
        Assert.Contains("starter {", missile.Script);
        Assert.Contains("starter {", furniture.Script);
        Assert.Contains("starter {", platformGraph.Script);
        // The bear's move has no starter in any chunk, the platform's starter of its ID isn't its
        Assert.DoesNotContain("starter {", bear.Script);

        // A slot naming the starter, the variant's too, names its graph
        var missileObject = ObjectOf(package, "MISSILE", 0x60);
        Assert.Equal([UriOf(missile.Metadata)], Links(missileObject, nameof(GameObjectData.BehaviourSlots)));
        Assert.Empty(Links(missileObject, nameof(GameObjectData.GraphsWithoutStarter)));
        var platformObject = ObjectOf(package, "PLATFORM", 0x64);
        Assert.Equal([UriOf(platformGraph.Metadata)], Links(platformObject, nameof(GameObjectData.BehaviourSlots)));

        // A message naming the starter of another ID, and the graph listed with that starter
        var sphere = ObjectOf(package, "SPHERE", 0x61);
        Assert.Equal(UriOf(furniture.Metadata), (string)sphere[nameof(GameObjectData.TriggerBehaviours)]![0]!["TriggerBehaviour"]!["_uri"]!);
        Assert.Empty(Links(sphere, nameof(GameObjectData.GraphsWithoutStarter)));
        Assert.Empty(Links(ObjectOf(package, "SPAWNER", 0x62), nameof(GameObjectData.GraphsWithoutStarter)));

        // A graph listed without its starter is the chunk's
        Assert.Equal([UriOf(bear.Metadata)], Links(ObjectOf(package, "BEAR", 0x63), nameof(GameObjectData.GraphsWithoutStarter)));

        // The other furniture chunk's sphere, the same but its name, is the first's: no value of the chunk's own
        var coreb = Directory.EnumerateFiles(package, "coreb.json", SearchOption.AllDirectories).Single();
        Assert.Empty((JArray)JObject.Parse(File.ReadAllText(coreb))[nameof(LevelChunk.Overrides)]!);
    }

    [Theory]
    [InlineData(GamePlatform.PS2)]
    [InlineData(GamePlatform.Xbox)]
    public void GraphsStatesRunTheirChunksGraphs(GamePlatform platform)
    {
        var package = Unpack(platform);

        var platformGraph = GraphOf(package, "COM_FLIP_FLOP_PLATFORM");
        var bear = GraphOf(package, "COM_BEAR_MOVE");
        // The totem's platform is the graph of the ID, its name finds it
        Assert.Contains("state State_0(COM_FLIP_FLOP_PLATFORM) {", GraphOf(package, "COM_PLATFORM_DEFAULT").Script);
        // The bear's move is the zoo's variant, which the name doesn't find from a graph of no variation: its URI does
        Assert.Contains($"state State_0(\"{UriOf(bear.Metadata)}\") {{", GraphOf(package, "COM_BEAR_DEFAULT").Script);
        Assert.NotEqual(UriOf(platformGraph.Metadata), UriOf(bear.Metadata));
    }

    // Two chunks with scripts of one ID and starters of one ID: an instance's spawn script names its own chunk's graph, the later chunk's
    // variant, like it did before the starters were found among every chunk's graphs (whose order has the first chunk's graph first)
    [Fact]
    public void AnInstancesSpawnScriptIsItsChunksGraphOfTheStarter()
    {
        var second = Chunk(GamePlatform.PS2, Graph(GamePlatform.PS2, "COM_SECOND", 0x41, 0x40), Object(GamePlatform.PS2, "BOX", 0x66, [], []));
        var instance = new PS2AnyInstance { ObjectId = 0x66, SpawnScriptId = 0x40, FloatProperties = [1], IntProperties = [0, 255] };
        instance.SetID(0);
        second.GetItem<ITwinSection>(Constants.LEVEL_LAYOUT_1_SECTION).GetItem<ITwinSection>(Constants.LAYOUT_INSTANCES_SECTION).AddItem(instance);

        var package = Unpack(GamePlatform.PS2, [
            ("Levels\\Earth\\Hub\\hubA.rm2", Chunk(GamePlatform.PS2, Graph(GamePlatform.PS2, "COM_FIRST", 0x41, 0x40))),
            ("Levels\\Earth\\Hub\\hubC.rm2", second)
        ]);

        var variant = GraphOf(package, "COM_SECOND");
        Assert.False(string.IsNullOrEmpty((string?)variant.Metadata[nameof(IAsset.Variation)]));
        var unpacked = _project.AssetManager.GetAllAssetsOf<ObjectInstance>().Single();
        var data = JObject.Parse(File.ReadAllText(unpacked.FullDataPath));
        Assert.Equal(UriOf(variant.Metadata), (string)data[nameof(ObjectInstanceData.SpawnScript)]!["_uri"]!);
    }
}
