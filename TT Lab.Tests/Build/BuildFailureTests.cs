using System.Text;
using Avalonia.Headless.XUnit;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Project.Build;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code.AgentLab;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Build;

// A chunk that couldn't be written stopped the build without a word in debug builds: the build's task threw into nothing and the
// project was never handed back. Failures now name what failed and where, and left out chunks never built come from the disc's archive
[Collection(ProjectCollection.Name)]
public sealed class BuildFailureTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private LevelChunk AddChunk(string path)
    {
        return _project.Add(new LevelChunk { AdditionalPath = path }, path.Split('/')[^1]);
    }

    [Fact]
    public void FailuresAreReportedOneLineEach()
    {
        var single = ProjectManager.BuildFailureLines("Error building", new BuildException("Rock of beach", new InvalidOperationException("broken")));
        Assert.Equal(["Error building: Rock of beach: broken"], single);

        var chunks = Enumerable.Range(0, 7).Select(i => (Exception)new BuildException($"AI Navigation Position 0 of chunk{i}", new InvalidDataException("no Radius"))).ToList();
        var lines = ProjectManager.BuildFailureLines("Error building", new AggregateException(chunks));

        Assert.Equal("Error building, 7 chunks failed:", lines[0]);
        Assert.Equal("  AI Navigation Position 0 of chunk0: no Radius", lines[1]);
        Assert.Equal(7, lines.Count);
        Assert.Equal("  and 2 more, all of them are in the session log", lines[^1]);
    }

    [AvaloniaFact]
    public async Task AFailingBuildHandsTheProjectBack()
    {
        var manager = Locator.Current.GetService<ProjectManager>()!;
        manager.WorkableProject = true;
        using var release = new ManualResetEventSlim();

        manager.RunBuild("Error building", _ =>
        {
            release.Wait(TimeSpan.FromSeconds(10));
            throw new BuildException("Rock of beach", new InvalidOperationException("broken"));
        });

        Assert.False(manager.WorkableProject);
        release.Set();
        for (var i = 0; i < 250 && !manager.WorkableProject; i++)
        {
            await Task.Delay(20);
        }

        Assert.True(manager.WorkableProject);
    }

    // The AI positions of a project made before their W got named Radius had FloatArg, every chunk with one failed on it
    [Fact]
    public void AChunkThatFailsNamesTheAssetAndItsFile()
    {
        var beach = AddChunk("levels/earth/hub/beach");
        var position = _project.Add(new AiPosition { Chunk = beach.AdditionalPath!, LayoutID = 6 }, "AI Navigation Position 0");
        position.SetData(new AiPositionData(position) { Coords = new Vector3(1, 2, 3), Radius = 1 });
        position.Serialize(SerializationFlags.SaveData);
        File.WriteAllText(position.FullDataPath, File.ReadAllText(position.FullDataPath).Replace("\"Radius\"", "\"FloatArg\""));
        beach.ChunkResources.Add(position.URI);
        var outputs = new[] { Path.Combine(_project.Root, "out", "beach.rm2"), Path.Combine(_project.Root, "out", "beach.sm2") };
        Directory.CreateDirectory(Path.GetDirectoryName(outputs[0])!);
        var cache = BuildCache.Load(_project.Project.ProjectPath, _project.AssetManager);

        var failure = Assert.Throws<BuildException>(() => _project.Project.WriteChunk(new PS2ItemFactory(), cache, beach, outputs, false));

        Assert.Equal("AI Navigation Position 0 of beach", failure.What);
        Assert.Contains($"{position.Data} can't be read", failure.Message);
        Assert.Contains("Required property 'Radius' not found", failure.Message);
        Assert.False(File.Exists(outputs[0]));
    }

    private BehaviourGraph AddGraph(string name, UInt32 id, string script)
    {
        var graph = _project.Add(new BehaviourGraph(), name, id);
        graph.SetData(new BehaviourGraphData(graph) { Graph = script });
        graph.Serialize(SerializationFlags.SaveData);
        return graph;
    }

    private GameObject AddObject(string name, UInt32 id, Func<GameObject, GameObjectData> data)
    {
        var gameObject = _project.Add(new GameObject(), name, id);
        gameObject.SetData(data(gameObject));
        gameObject.Serialize(SerializationFlags.SaveData);
        return gameObject;
    }

    private static ITwinSection Code(ITwinSection root, int id) => root.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION).GetItem<ITwinSection>((UInt32)id);

    // A script that passes the live check but not the compiler was built as a graph of no states (the compiler keeps what it made before it
    // failed) without a word: a state running a behaviour the project doesn't have, deleted after the script named it, fails the chunk
    // naming the script and what's wrong. A script that compiles is built as it compiled
    [Fact]
    public void AGraphThatDoesntCompileFailsItsChunkNamingIt()
    {
        var beach = AddChunk("levels/earth/hub/beach");
        var graph = AddGraph("COM_BROKEN", 0x31, "behaviour COM_BROKEN {\n   state S(COM_DELETED) {\n   }\n}\n");
        var bird = AddObject("Bird", 0x51, asset => new GameObjectData(asset) { Name = "Bird", BehaviourSlots = [graph.URI], FloatProperties = [1], IntProperties = [0, 255] });
        var instance = _project.Add(new ObjectInstance { Chunk = beach.AdditionalPath!, LayoutID = 0 }, "Instance 0");
        instance.SetData(new ObjectInstanceData(instance) { ObjectId = bird.URI, IntProperties = [0, 255] });
        instance.Serialize(SerializationFlags.SaveData);
        beach.ChunkResources.Add(instance.URI);
        var outputs = new[] { Path.Combine(_project.Root, "out", "beach.rm2"), Path.Combine(_project.Root, "out", "beach.sm2") };
        var cache = BuildCache.Load(_project.Project.ProjectPath, _project.AssetManager);

        var failure = Assert.Throws<BuildException>(() => _project.Project.WriteChunk(new PS2ItemFactory(), cache, beach, outputs, false));

        Assert.Equal("Instance 0 of beach", failure.What);
        Assert.Contains("COM_BROKEN's script doesn't compile", failure.Message);
        Assert.Contains("COM_DELETED", failure.Message);
        Assert.False(File.Exists(outputs[0]));

        ((IAsset)graph).GetData<BehaviourGraphData>().Graph = "behaviour COM_BROKEN {\n   state S() {\n   }\n}\n";
        graph.Serialize(SerializationFlags.SaveData);
        var factory = new PS2ItemFactory { ChunkPath = beach.AdditionalPath! };
        var rm2 = factory.GenerateRM();
        instance.ResolveChunkResources(factory, rm2);
        var built = Code(rm2, Constants.CODE_BEHAVIOURS_SECTION).GetItem<ITwinBehaviourGraph>(0x31);
        Assert.Same(((IAsset)graph).GetData<BehaviourGraphData>().GetCompiledBehaviour(factory).Get<ITwinBehaviourGraph>(), built);
        Assert.Single(built.ScriptStates);
    }

    // A command sequence was built cut off at the command that failed, or not at all
    [Fact]
    public void ACommandSequenceThatDoesntCompileFailsNamingIt()
    {
        var sequence = _project.Add(new BehaviourCommandsSequence(), "PickupLibrary", 5);
        var data = new BehaviourCommandsSequenceData(sequence);
        sequence.SetData(data);
        Assert.IsAssignableFrom<ITwinBehaviourCommandsSequence>(data.Export(new PS2ItemFactory()));

        data.Code = data.Code.Replace("ClearTrail();", "NoSuchCommand();");
        var failure = Assert.Throws<InvalidOperationException>(() => data.Export(new PS2ItemFactory()));

        Assert.Contains("PickupLibrary's script doesn't compile", failure.Message);
        Assert.Contains("NoSuchCommand", failure.Message);
    }

    // An object's command pack that didn't compile failed the build with "The given key '...ITwinBehaviourCommandPack' was not present in
    // the dictionary", which named nothing
    [Fact]
    public void AnObjectsCommandsThatDontCompileFailNamingTheObject()
    {
        var bird = AddObject("Bird", 0x51, asset => new GameObjectData(asset) { Name = "Bird", BehaviourPack = "NoSuchCommand();\n", FloatProperties = [1], IntProperties = [0, 255] });
        var factory = new PS2ItemFactory { ChunkPath = "levels/earth/hub/beach" };

        var failure = Assert.Throws<InvalidOperationException>(() => bird.ResolveChunkResources(factory, Code(factory.GenerateRM(), Constants.CODE_GAME_OBJECTS_SECTION)));

        Assert.Contains("Bird's script doesn't compile", failure.Message);
        Assert.Contains("NoSuchCommand", failure.Message);
    }

    // The BH's records: the path's length and characters, then where the file is in the BD and how long it is
    private static void WriteArchive(string discPath, params (string Path, byte[] Bytes)[] files)
    {
        var crash6 = Path.Combine(discPath, "Crash6");
        Directory.CreateDirectory(crash6);
        using var bh = new BinaryWriter(File.Create(Path.Combine(crash6, "Crash.BH")));
        using var bd = File.Create(Path.Combine(crash6, "Crash.BD"));
        bh.Write(0x501);
        foreach (var (path, bytes) in files)
        {
            bh.Write(path.Length);
            bh.Write(Encoding.ASCII.GetBytes(path));
            bh.Write((int)bd.Position);
            bh.Write(bytes.Length);
            bd.Write(bytes);
        }
    }

    [Fact]
    public void LeftOutChunksNeverBuiltComeFromTheDiscsArchive()
    {
        var beach = AddChunk("levels/earth/hub/beach");
        var cave = AddChunk("levels/earth/hub/cave");
        var filesPath = Path.Combine(_project.Root, "build", "archives");
        var disc = Path.Combine(_project.Root, "disc", "ps2");
        // The game's archive names them its own way
        WriteArchive(disc, ("Startup\\Default.rm2", [9]), ("Levels\\Earth\\Hub\\beach.rm2", [1, 2, 3]), ("Levels\\Earth\\Hub\\beach.sm2", [4, 5]));
        var excluded = new HashSet<LabURI> { beach.URI, cave.URI };
        var leftOut = new TT_Lab.Project.Project.LeftOutChunks(excluded, TT_Lab.Project.Project.GamePlatform.PS2, filesPath, disc);
        string[] Outputs(string name) => [Path.Combine(filesPath, "Levels", "earth", "hub", $"{name}.rm2"), Path.Combine(filesPath, "Levels", "earth", "hub", $"{name}.sm2")];

        Assert.True(leftOut.Contains(beach));
        Assert.True(leftOut.Keep(beach, Outputs("beach")));
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Outputs("beach")[0]));
        Assert.Equal([4, 5], File.ReadAllBytes(Outputs("beach")[1]));

        // Neither built nor on the disc, it has to be written
        Assert.False(leftOut.Keep(cave, Outputs("cave")));
        Assert.False(File.Exists(Outputs("cave")[0]));

        // Built before, its files stay as they are
        File.WriteAllBytes(Outputs("beach")[0], [7]);
        Assert.True(leftOut.Keep(beach, Outputs("beach")));
        Assert.Equal([7], File.ReadAllBytes(Outputs("beach")[0]));

        // The Xbox game takes the disc's files itself
        var xbox = new TT_Lab.Project.Project.LeftOutChunks(excluded, TT_Lab.Project.Project.GamePlatform.Xbox, filesPath, null);
        Assert.True(xbox.Keep(cave, Outputs("cave")));
        Assert.False(File.Exists(Outputs("cave")[0]));
    }

    [Fact]
    public void ADiscWithoutAnArchiveHasNoneToTakeFrom()
    {
        Assert.Null(DiscArchive.Open(null));
        Assert.Null(DiscArchive.Open(Path.Combine(_project.Root, "nowhere")));
    }
}
