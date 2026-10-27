using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Code.Object;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.Tests.Build;

// A starter makes the game run its graph on its own. The game only has a graph's starter in the chunks with objects referencing it,
// High Seas' rats got the starter destroying them from another chunk and couldn't spawn
[Collection(ProjectCollection.Name)]
public sealed class BehaviourStarterTests : IDisposable
{
    private const UInt32 FurnitureId = 0x31;
    private const UInt32 FurnitureStarter = 0x3E;

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private TT_Lab.Project.Project Project => _project.Project;

    // A graph with a starter whose state runs the child
    private static string Script(string name, string child = "") =>
        "[StartFrom(State_0)]\n" +
        "[Priority(50)]\n" +
        $"behaviour {name} {{\n" +
        "   starter {\n" +
        "      assigner = {\n" +
        "         AssignType = ME;\n" +
        "         AssignLocality = ANYWHERE;\n" +
        "         AssignStatus = ANYSTATE;\n" +
        "         AssignPreference = ANYHOW;\n" +
        "      }\n" +
        "   }\n" +
        "   [Unknown(0x0)]\n" +
        $"   state State_0({child}) {{\n" +
        "   }\n" +
        "}\n";

    private BehaviourGraph AddGraph(string name, UInt32 id, Package? package = null, UInt32? starterId = null, string? script = null)
    {
        var graph = _project.Add(new BehaviourGraph { StarterId = starterId }, name, id, package ?? _project.Project.Ps2Package);
        graph.SetData(new BehaviourGraphData(graph) { Graph = script ?? Script(name) });
        // Building releases the data of what it resolved and loads it back when needed
        graph.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        return graph;
    }

    [AvaloniaFact]
    public void ChunksOnlyGetTheStartersTheirObjectsReference()
    {
        var walk = AddGraph("COM_RAT_WALK", 0x11);
        var destroy = AddGraph("COM_GENERIC_CREATURE_DESTROY", 0x13);
        var rat = _project.Add(new GameObject(), "Rat", 0x51, _project.Project.Ps2Package);
        rat.SetData(new GameObjectData(rat) { BehaviourSlots = [walk.URI, destroy.URI], GraphsWithoutStarter = [destroy.URI] });
        var spawner = _project.Add(new GameObject(), "Spawner", 0x92, _project.Project.Ps2Package);
        // The spawner references the destroy graph with its starter
        spawner.SetData(new GameObjectData(spawner) { BehaviourSlots = [destroy.URI], ObjectSlots = [rat.URI] });
        rat.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        spawner.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        var factory = new PS2ItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2, ChunkPath = "levels/test" };
        var code = factory.GenerateRM().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var objects = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        var behaviours = code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);

        rat.ResolveChunkResources(factory, objects);

        Assert.True(behaviours.ContainsItem(0x10));
        Assert.False(behaviours.ContainsItem(0x12));
        Assert.Equal(new UInt16[] { 0x10, 0x11, 0x13 }, objects.GetItem<ITwinObject>(0x51).RefBehaviours.Order());
        // The starter's header has its ID and its first assigner the ID two past it, like the game's
        var starterBytes = Write(behaviours.GetItem<TwinBehaviourStarter>(0x10));
        Assert.Equal(0x10, BitConverter.ToUInt16(starterBytes, 0));
        Assert.Equal(0x12, BitConverter.ToInt32(starterBytes, 8));

        spawner.ResolveChunkResources(factory, objects);

        Assert.True(behaviours.ContainsItem(0x12));
        Assert.Contains((UInt16)0x12, objects.GetItem<ITwinObject>(0x92).RefBehaviours);
        Assert.DoesNotContain((UInt16)0x12, objects.GetItem<ITwinObject>(0x51).RefBehaviours);
    }

    // Crates placed in levels are copies of the startup chunk's that list nothing. Chunks only have the behaviours objects list, the
    // startup chunk has the crates'
    [AvaloniaFact]
    public void ObjectsCanLeaveTheirResourcesUnlisted()
    {
        var crateGraph = AddGraph("COM_GENERIC_CRATE_DEFAULT", 0x27);
        var crate = _project.Add(new GameObject(), "Crate", 0xE, _project.Project.Ps2Package);
        crate.SetData(new GameObjectData(crate) { BehaviourSlots = [crateGraph.URI], ReferencesResources = false });
        crate.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        var factory = new PS2ItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2, ChunkPath = "levels/test" };
        var code = factory.GenerateRM().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var objects = code.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);

        crate.ResolveChunkResources(factory, objects);

        var written = objects.GetItem<ITwinObject>(0xE);
        Assert.False(written.ReferencesResources);
        Assert.Equal(new UInt16[] { 0x26 }, written.BehaviourSlots);
        Assert.Equal(0, code.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION).GetItemsAmount());
    }

    // Whether an object lists what it uses isn't edited, a startup object a level gets without having a version of its own on the disc is a
    // copy of the startup chunk's and lists nothing like the game's copies
    [AvaloniaFact]
    public void StartupObjectsListTheirResourcesInLevelsThatHadThem()
    {
        var crateGraph = AddGraph("COM_GENERIC_CRATE_DEFAULT", 0x27);
        var crate = _project.Add(new GameObject(), "Crate", 0xE, _project.Project.GlobalPackagePS2);
        crate.SetData(new GameObjectData(crate) { BehaviourSlots = [crateGraph.URI] });
        crate.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        var levelObject = _project.Add(new GameObject(), "Rat", 0x51, _project.Project.Ps2Package);
        levelObject.SetData(new GameObjectData(levelObject));
        levelObject.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);

        ITwinObject Write(IAsset asset, IReadOnlyDictionary<(Type, UInt32), LabURI>? versions)
        {
            var factory = new PS2ItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2, ChunkPath = "levels/test", ChunkVersions = versions };
            var objects = factory.GenerateRM().GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
            asset.ResolveChunkResources(factory, objects);
            return objects.GetItem<ITwinObject>(asset.ID);
        }

        Assert.False(Write(crate, null).ReferencesResources);
        Assert.True(Write(crate, new Dictionary<(Type, UInt32), LabURI> { [(typeof(GameObject), 0xE)] = crate.URI }).ReferencesResources);
        Assert.True(Write(levelObject, null).ReferencesResources);
    }

    // The startup chunk's crate templates refer to the starters of the crates' graphs, not the graphs
    [AvaloniaFact]
    public void TemplatesReferToTheStartersOfTheirGraphs()
    {
        var crateGraph = AddGraph("COM_GENERIC_CRATE_DEFAULT", 0x29);
        var crate = _project.Add(new GameObject(), "Crate", 0x3, _project.Project.Ps2Package);
        crate.SetData(new GameObjectData(crate) { BehaviourSlots = [crateGraph.URI], GraphsWithoutStarter = [crateGraph.URI] });
        crate.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        var template = _project.Add(new InstanceTemplate { Chunk = System.IO.Path.Combine("levels", "test"), LayoutID = 0 }, "BASICCRATE", 0x1, _project.Project.Ps2Package);
        template.SetData(new InstanceTemplateData(template) { TemplateName = "BASICCRATE", ObjectId = crate.URI, BehaviourStarters = [crateGraph.URI] });
        template.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        var factory = new PS2ItemFactory { GlobalPackage = _project.Project.GlobalPackagePS2, ChunkPath = "levels/test" };
        var rm = factory.GenerateRM();

        template.ResolveChunkResources(factory, rm);

        var written = rm.GetItem<ITwinSection>(0).GetItem<ITwinSection>(Constants.LAYOUT_TEMPLATES_SECTION).GetItem<ITwinTemplate>(0x1);
        Assert.Equal(new UInt16[] { 0x28 }, written.BehaviourStarters);
        Assert.True(rm.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION).ContainsItem(0x28));
    }

    private GameObject AddObject(string name, UInt32 id, Package? package = null, Func<GameObject, GameObjectData>? data = null)
    {
        var gameObject = _project.Add(new GameObject(), name, id, package ?? Project.Ps2Package);
        gameObject.SetData(data?.Invoke(gameObject) ?? new GameObjectData(gameObject) { FloatProperties = [1], IntProperties = [0, 255] });
        gameObject.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        return gameObject;
    }

    private ObjectInstance AddInstance(GameObject gameObject, BehaviourGraph spawnScript, Package? package = null)
    {
        var instance = _project.Add(new ObjectInstance { Chunk = "levels/test", LayoutID = 0 }, "Instance 0", 0, package ?? Project.Ps2Package);
        instance.SetData(new ObjectInstanceData(instance) { ObjectId = gameObject.URI, SpawnScript = spawnScript.URI, IntProperties = [0, 255] });
        instance.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        return instance;
    }

    private (ITwinItemFactory Factory, ITwinSection Root) NewChunk(bool isDefault = false)
    {
        var factory = new PS2ItemFactory { GlobalPackage = Project.GlobalPackagePS2, ChunkPath = isDefault ? "startup/default" : "levels/test", IsDefaultResolution = isDefault };
        return (factory, isDefault ? factory.GenerateDefault() : factory.GenerateRM());
    }

    private static ITwinSection Code(ITwinSection root, int id) => root.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>((UInt32)id);

    // The section's items' IDs in the order they're written
    private static List<UInt32> InOrder(ITwinSection section) => Enumerable.Range(0, section.GetItemsAmount()).Select(i => section.GetItem(i).GetID()).ToList();

    private static List<UInt32> Ids(ITwinSection section) => InOrder(section).Order().ToList();

    private static ITwinInstance BuiltInstance(ITwinSection root) => root.GetItem<ITwinSection>(0).GetItem<ITwinSection>(Constants.LAYOUT_INSTANCES_SECTION).GetItem<ITwinInstance>(0);

    // Two of the game's starters have another ID than the one before their graph's (AltEarth's 0xBFE of 0xBED, labext's 0xF6C of 0xF65),
    // which the builds dropped: a graph of such a starter is built with it, its first assigner running the graph, and what names its starter
    // names it (slots, trigger messages, an object's list, an instance's spawn script)
    [AvaloniaFact]
    public void AStarterOfTheGamesOtherIdIsBuiltWithItAndNamedByIt()
    {
        var furniture = AddGraph("COM_FURNITURE", FurnitureId, starterId: FurnitureStarter);
        var sphere = AddObject("SPHERE", 0x61, data: asset => new GameObjectData(asset)
        {
            BehaviourSlots = [furniture.URI], TriggerBehaviours = [new ObjectTriggerBehaviourData { TriggerBehaviour = furniture.URI, MessageID = 5 }],
            FloatProperties = [1], IntProperties = [0, 255]
        });
        var instance = AddInstance(sphere, furniture);
        var (factory, root) = NewChunk();

        instance.ResolveChunkResources(factory, root);

        var behaviours = Code(root, Constants.CODE_BEHAVIOURS_SECTION);
        Assert.Equal([FurnitureId, FurnitureStarter], Ids(behaviours));
        // Its assigner runs the graph, the ID two past the starter's of every other starter
        Assert.Equal((Int32)FurnitureId + 1, behaviours.GetItem<TwinBehaviourStarter>(FurnitureStarter).Assigners[0].Behaviour);
        var built = Code(root, Constants.CODE_GAME_OBJECTS_SECTION).GetItem<ITwinObject>(0x61);
        Assert.Equal(new UInt16[] { (UInt16)FurnitureStarter }, built.BehaviourSlots);
        Assert.Equal((UInt16)FurnitureStarter, built.TriggerBehaviours[0].TriggerBehaviour);
        Assert.Equal(new UInt16[] { (UInt16)FurnitureStarter, (UInt16)FurnitureId }, built.RefBehaviours);
        Assert.Equal((UInt16)FurnitureStarter, BuiltInstance(root).SpawnScriptId);
    }

    // A copy of the graph is no item of the disc's (AssetRelocation.Copy drops its StarterId), and nor is the graph built with another ID than
    // its own: their starters have the ID before the graph's
    [AvaloniaFact]
    public void AGraphOfAnotherIdHasTheStarterBeforeIt()
    {
        var furniture = AddGraph("COM_FURNITURE", FurnitureId, starterId: FurnitureStarter);
        var copy = AddGraph("COM_FURNITURE (2)", 0x45, script: Script("COM_FURNITURE"));
        var sphere = AddObject("SPHERE", 0x61, data: asset => new GameObjectData(asset) { BehaviourSlots = [copy.URI], FloatProperties = [1], IntProperties = [0, 255] });
        var (factory, root) = NewChunk();

        sphere.ResolveChunkResources(factory, Code(root, Constants.CODE_GAME_OBJECTS_SECTION));

        Assert.Equal([0x44, 0x45], Ids(Code(root, Constants.CODE_BEHAVIOURS_SECTION)));
        Assert.Equal(new UInt16[] { 0x44 }, Code(root, Constants.CODE_GAME_OBJECTS_SECTION).GetItem<ITwinObject>(0x61).BehaviourSlots);

        Assert.Equal(0x46U, BehaviourGraph.StarterIdOf(furniture, 0x47));
        (factory, root) = NewChunk();
        var behaviours = Code(root, Constants.CODE_BEHAVIOURS_SECTION);
        var data = ((IAsset)furniture).GetData<BehaviourGraphData>();
        data.ResolveChunkResources(factory, behaviours, 0x47);
        data.AddStarter(factory, behaviours);
        Assert.Equal([0x46, 0x47], Ids(behaviours));
        Assert.Equal(0x48, behaviours.GetItem<TwinBehaviourStarter>(0x46).Assigners[0].Behaviour);
    }

    // The chunks of a build share a graph's compile and its starter: the compile gives the starter its ID and the graph it runs, and nothing
    // changes it once it's shared. Given them by every chunk putting it in, the game's starters of other IDs would run the graph two past
    // their own for a moment, which a chunk written meanwhile could get
    [AvaloniaFact]
    public void AStarterHasItsValuesFromTheCompileOnAndNothingChangesThem()
    {
        var furniture = AddGraph("COM_FURNITURE", FurnitureId, starterId: FurnitureStarter);
        var data = ((IAsset)furniture).GetData<BehaviourGraphData>();
        var build = new PS2ItemFactory { GlobalPackage = Project.GlobalPackagePS2 };

        var starter = data.GetCompiledBehaviour(build).Get<TwinBehaviourStarter>();

        Assert.Equal(FurnitureStarter, starter.GetID());
        var compiled = Write(starter);
        Assert.Equal((UInt16)FurnitureStarter, BitConverter.ToUInt16(compiled, 0));
        Assert.Equal((Int32)FurnitureId + 1, BitConverter.ToInt32(compiled, 8));

        // What a chunk putting it in did to it would show: a value it's given once it's shared stays
        starter.Assigners[0].Behaviour = 0x7777;
        foreach (var chunk in new[] { build.ForChunk(), build.ForChunk() })
        {
            var behaviours = Code(chunk.GenerateRM(), Constants.CODE_BEHAVIOURS_SECTION);
            data.AddStarter(chunk, behaviours);
            Assert.Same(starter, behaviours.GetItem<TwinBehaviourStarter>(FurnitureStarter));
        }

        Assert.Equal(0x7777, starter.Assigners[0].Behaviour);
        Assert.Equal(compiled[..8], Write(starter)[..8]);
    }

    // An instance's spawn script came in alone, without its starter, which the instance names, or the graphs it runs: it comes in like an
    // object's behaviours
    [AvaloniaFact]
    public void AnInstancesSpawnScriptComesInWithItsStarterAndWhatItRuns()
    {
        AddGraph("COM_CHILD", 0x45);
        var spawn = AddGraph("COM_SPAWN", 0x47, script: Script("COM_SPAWN", "COM_CHILD"));
        var holder = AddObject("HOLDER", 0x70);
        var instance = AddInstance(holder, spawn);
        var (factory, root) = NewChunk();

        instance.ResolveChunkResources(factory, root);

        // The graph and its starter, then the graph it runs, without its starter: the way a template's graphs come in
        Assert.Equal([0x47, 0x46, 0x45], InOrder(Code(root, Constants.CODE_BEHAVIOURS_SECTION)));
        Assert.Equal([holder.ID], Ids(Code(root, Constants.CODE_GAME_OBJECTS_SECTION)));
        Assert.Equal((UInt16)0x46, BuiltInstance(root).SpawnScriptId);
    }

    // The game's crates spawn with the default chunk's outline (starter 0x28), which no level has of its own: a level's instance names it
    // there. docamok1's and docamok2's crates, in layout 0, brought it into their levels
    [AvaloniaFact]
    public void ALevelsInstanceRunsTheDefaultChunksSpawnScriptFromThere()
    {
        var outline = AddGraph("COM_GENERIC_CRATE_OUTLINE", 0x29, Project.GlobalPackagePS2);
        var crate = AddObject("IRONCRATE", 0xF);
        var instance = AddInstance(crate, outline);

        var (factory, root) = NewChunk();
        instance.ResolveChunkResources(factory, root);

        Assert.Empty(Ids(Code(root, Constants.CODE_BEHAVIOURS_SECTION)));
        Assert.Equal((UInt16)0x28, BuiltInstance(root).SpawnScriptId);

        // The default chunk's own instance has it in the chunk with its starter
        (factory, root) = NewChunk(isDefault: true);
        instance.ResolveChunkResources(factory, root);
        Assert.Equal([0x28, 0x29], Ids(Code(root, Constants.CODE_BEHAVIOURS_SECTION)));
    }

    // A level's template leaves a graph of the default chunk there too: naming the crates' outline it brought the outline and its starter
    // into the level (only the default chunk has templates on the discs)
    [AvaloniaFact]
    public void ALevelsTemplateLeavesTheDefaultChunksGraphsThere()
    {
        var outline = AddGraph("COM_GENERIC_CRATE_OUTLINE", 0x29, Project.GlobalPackagePS2);
        var crate = AddObject("IRONCRATE", 0xF);
        var template = _project.Add(new InstanceTemplate { Chunk = "levels/test", LayoutID = 0 }, "IRONCRATE", 0x1, Project.Ps2Package);
        template.SetData(new InstanceTemplateData(template) { TemplateName = "IRONCRATE", ObjectId = crate.URI, BehaviourStarters = [outline.URI] });
        template.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);

        var (factory, root) = NewChunk();
        template.ResolveChunkResources(factory, root);

        Assert.Empty(Ids(Code(root, Constants.CODE_BEHAVIOURS_SECTION)));
        Assert.Equal([crate.ID], Ids(Code(root, Constants.CODE_GAME_OBJECTS_SECTION)));
        var built = root.GetItem<ITwinSection>(0).GetItem<ITwinSection>(Constants.LAYOUT_TEMPLATES_SECTION).GetItem<ITwinTemplate>(0x1);
        Assert.Equal(new UInt16[] { 0x28 }, built.BehaviourStarters);

        // The default chunk's own template has it in the chunk with its starter
        (factory, root) = NewChunk(isDefault: true);
        template.ResolveChunkResources(factory, root);
        Assert.Equal([0x28, 0x29], Ids(Code(root, Constants.CODE_BEHAVIOURS_SECTION)));
    }

    // An Xbox project made while its default chunk was unpacked after the levels has the crates' outline in the XBOX package: a graph the
    // default chunk's templates start is the default chunk's all the same
    [AvaloniaFact]
    public void ALevelLeavesAGraphTheDefaultChunksTemplatesStartThere()
    {
        var outline = AddGraph("COM_GENERIC_CRATE_OUTLINE", 0x29, Project.XboxPackage);
        var crate = AddObject("IRONCRATE", 0xF, Project.XboxPackage);
        var instance = AddInstance(crate, outline, Project.XboxPackage);
        ITwinSection Build()
        {
            var factory = new XboxItemFactory { GlobalPackage = Project.GlobalPackageXbox, ChunkPath = "levels/test" };
            var root = factory.GenerateRM();
            instance.ResolveChunkResources(factory, root);
            return root;
        }

        void AddTemplate(string chunk, UInt32 id)
        {
            var template = _project.Add(new InstanceTemplate { Chunk = chunk, LayoutID = 0 }, "IRONCRATE", id, Project.GlobalPackageXbox);
            template.SetData(new InstanceTemplateData(template) { TemplateName = "IRONCRATE", ObjectId = crate.URI, BehaviourStarters = [outline.URI] });
            template.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        }

        // A level's graph of no template of the default chunk comes into the level, a level chunk of the global package named default is no
        // default chunk
        AddTemplate(System.IO.Path.Combine("levels", "earth", "default"), 0x1);
        Assert.Equal([0x28, 0x29], Ids(Code(Build(), Constants.CODE_BEHAVIOURS_SECTION)));

        AddTemplate(System.IO.Path.Combine("startup", "default"), 0x2);

        var level = Build();
        Assert.Empty(Ids(Code(level, Constants.CODE_BEHAVIOURS_SECTION)));
        Assert.Equal((UInt16)0x28, BuiltInstance(level).SpawnScriptId);
    }

    private static byte[] Write(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }
}
