using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
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
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private static string Script(string name) =>
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
        "   state State_0() {\n" +
        "   }\n" +
        "}\n";

    private BehaviourGraph AddGraph(string name, UInt32 id)
    {
        var graph = _project.Add(new BehaviourGraph(), name, id, _project.Project.Ps2Package);
        graph.SetData(new BehaviourGraphData(graph) { Graph = Script(name) });
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

    private static byte[] Write(ITwinSerializable item)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        item.Write(writer);
        writer.Flush();
        return stream.ToArray();
    }
}
