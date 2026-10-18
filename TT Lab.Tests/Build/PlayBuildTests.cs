using System.Text;
using TT_Lab.AssetData.Global;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Pcsx2;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Build;

// Play built the chunk and the chunks it links, and the game played the global objects and their models (crates, wumpa fruit) the way the
// last whole build had them: it reads the default chunk as it boots and takes its objects in place of the levels' copies. Play builds the
// default chunk and the startup's and language's files with its chunks now, the Extras menu's pictures are left to the whole build
[Collection(ProjectCollection.Name)]
public sealed class PlayBuildTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly LevelChunk _beach;
    private readonly Particles _particles;

    public PlayBuildTests()
    {
        var global = _project.Project.GlobalPackagePS2;
        _project.BuildProjectTree(Path.Combine(global.Name, "Startup"), Path.Combine(global.Name, "Language", "Code"), Path.Combine(global.Name, "Extras"));
        var startup = _project.GetFolder(global, "Startup");
        var defaultChunk = _project.Add(new LevelChunk { AdditionalPath = "startup/default" }, "default");
        startup.AddChild(defaultChunk);
        // A default chunk's file needs its particles, a level's without systems will do
        _particles = _project.Add(new Particles { Chunk = "startup/default" }, "Global Particles", Constants.LEVEL_PARTICLES_ITEM);
        _particles.SetData(new ParticleData(_particles));
        _particles.Serialize(SerializationFlags.SaveData);
        defaultChunk.ChunkResources.Add(_particles.URI);
        AddText(startup, "Startup", "LevelSelect", "\"beach\"\t\"\"\t\"levels\\earth\\hub\\beach\"\r\n");
        AddText(_project.GetFolder(global, "Language", "Code"), "Language/Code", "English", "Hello");
        AddText(_project.GetFolder(global, "Extras"), "Extras", "credits", "Thanks");

        // Built before and unchanged, the build keeps it
        _beach = _project.Add(new LevelChunk { AdditionalPath = "levels/earth/hub/beach" }, "beach", package: _project.Project.Ps2Package);
        var levels = Path.Combine(FilesPath, "Levels", "earth", "hub");
        Directory.CreateDirectory(levels);
        string[] outputs = [Path.Combine(levels, "beach.rm2"), Path.Combine(levels, "beach.sm2")];
        File.WriteAllBytes(outputs[0], [1, 2]);
        File.WriteAllBytes(outputs[1], [3]);
        var cache = BuildCache.Load(_project.Project.ProjectPath, _project.AssetManager);
        cache.Record($"chunk:{_beach.URI}", [], outputs);
        cache.Save();
    }

    public void Dispose() => _project.Dispose();

    private string FilesPath => Path.Combine(_project.Project.ProjectPath, "build", "archives");

    private string DefaultRm2 => Path.Combine(FilesPath, "Startup", "Default.rm2");

    private void AddText(Folder folder, string globalPath, string name, string text)
    {
        var file = _project.Add(new TextFile { GlobalPath = globalPath }, name);
        file.SetData(new TextFileData(file, text));
        folder.AddChild(file);
    }

    [Fact]
    public void PlayingAChunkBuildsTheDefaultChunkAndTheStartupsAndLanguagesFiles()
    {
        Pcsx2Service.BuildChunks(_project.Project, [_beach]);

        Assert.True(File.Exists(DefaultRm2));
        Assert.Equal("\"beach\"\t\"\"\t\"levels\\earth\\hub\\beach\"\r\n", File.ReadAllText(Path.Combine(FilesPath, "Startup", "LevelSelect.txt"), Encoding.Latin1));
        Assert.Equal("Hello", File.ReadAllText(Path.Combine(FilesPath, "Language", "Code", "English.txt"), Encoding.Latin1));
        Assert.False(Directory.Exists(Path.Combine(FilesPath, "Extras")));
        Assert.Equal([1, 2], File.ReadAllBytes(Path.Combine(FilesPath, "Levels", "earth", "hub", "beach.rm2")));
    }

    [Fact]
    public void TheDefaultChunkIsOnlyWrittenAgainOnceWhatItsMadeOfChanged()
    {
        Pcsx2Service.BuildChunks(_project.Project, [_beach]);
        var built = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(DefaultRm2, built);

        Pcsx2Service.BuildChunks(_project.Project, [_beach]);
        Assert.Equal(built, File.GetLastWriteTimeUtc(DefaultRm2));

        // A global asset of the default chunk changed
        _particles.Alias = "Default Particles";
        Pcsx2Service.BuildChunks(_project.Project, [_beach]);
        Assert.NotEqual(built, File.GetLastWriteTimeUtc(DefaultRm2));
    }
}
