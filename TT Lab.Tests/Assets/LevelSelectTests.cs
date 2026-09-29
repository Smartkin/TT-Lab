using Avalonia.Headless.XUnit;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Global;
using TT_Lab.Assets.Instance;
using TT_Lab.ServiceProviders;
using TT_Lab.Tests.Support;
using TT_Lab.Tools.Pcsx2;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Assets;

// The startup's LevelSelect.txt lists a level a line ("name"<tabs>"note"<tabs>"chunk path", blank lines between the worlds), the level
// select Play puts in the PAL release's pause menu reads it. Chunks made in TT Lab go at its end and leave it when they're deleted
[Collection(ProjectCollection.Name)]
public sealed class LevelSelectTests : IDisposable
{
    private const string Retail = "\"nsanity isle and jungle bungle\"\t\t\"\"\t\"levels\\earth\\hub\\beach\"\r\n" +
                                  "\"cavern catastrophe\"\t\t\t\"\"\t\"levels\\earth\\cavern\\cavent\"\r\n" +
                                  "\r\n" +
                                  "\"iceberg lab and ice climb\"\t\t\"\"\t\"levels\\ice\\hub\\labext\"\r\n";

    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public void LevelsAreItsQuotedNamesAndPathsAndTheirWorldIsTheFolderAfterLevels()
    {
        Assert.Equal([("nsanity isle and jungle bungle", "levels\\earth\\hub\\beach"), ("cavern catastrophe", "levels\\earth\\cavern\\cavent"),
            ("iceberg lab and ice climb", "levels\\ice\\hub\\labext")], LevelSelect.Entries(Retail));
        Assert.Equal("earth", LevelSelect.WorldOf("levels\\earth\\hub\\beach"));
        Assert.Equal("cave", LevelSelect.WorldOf("levels\\cave"));
        Assert.Equal("startup", LevelSelect.WorldOf("startup\\default"));
    }

    [Fact]
    public void ALevelGoesAtTheEndInItsWorldOrAfterABlankLineStartingIt()
    {
        var sameWorld = LevelSelect.WithLevel(Retail, "igloo", "levels\\ice\\igloo");
        Assert.Equal(Retail + "\"igloo\"\t\"\"\t\"levels\\ice\\igloo\"\r\n", sameWorld);

        var otherWorld = LevelSelect.WithLevel(Retail, "my \"best\" level", "levels\\mymod\\start");
        Assert.Equal(Retail + "\r\n\"my 'best' level\"\t\"\"\t\"levels\\mymod\\start\"\r\n", otherWorld);
        // Once is enough, and a file without a last line ending or with other line endings keeps its own
        Assert.Equal(otherWorld, LevelSelect.WithLevel(otherWorld, "again", "LEVELS\\MYMOD\\START"));
        Assert.Equal("\"a\"\t\"\"\t\"levels\\x\\a\"\r\n\"b\"\t\"\"\t\"levels\\x\\b\"\r\n", LevelSelect.WithLevel("\"a\"\t\"\"\t\"levels\\x\\a\"", "b", "levels\\x\\b"));
        Assert.Equal("\"a\"\t\"\"\t\"levels\\x\\a\"\n\"b\"\t\"\"\t\"levels\\x\\b\"\n", LevelSelect.WithLevel("\"a\"\t\"\"\t\"levels\\x\\a\"\n", "b", "levels\\x\\b"));
        Assert.Equal("\"b\"\t\"\"\t\"levels\\x\\b\"\r\n", LevelSelect.WithLevel("", "b", "levels\\x\\b"));
    }

    [Fact]
    public void TakingALevelOutTakesTheBlankLineOfAWorldLeftEmpty()
    {
        var withLevels = LevelSelect.WithLevel(LevelSelect.WithLevel(Retail, "start", "levels\\mymod\\start"), "end", "levels\\mymod\\end");

        Assert.Equal(LevelSelect.WithLevel(Retail, "end", "levels\\mymod\\end"), LevelSelect.WithoutLevel(withLevels, "levels/mymod/start"));
        Assert.Equal(Retail, LevelSelect.WithoutLevel(LevelSelect.WithoutLevel(withLevels, "levels\\mymod\\start"), "levels\\mymod\\end"));
        // The first world's last level, and a level the file doesn't have
        Assert.Equal("\"iceberg lab and ice climb\"\t\t\"\"\t\"levels\\ice\\hub\\labext\"\r\n",
            LevelSelect.WithoutLevel(LevelSelect.WithoutLevel(Retail, "levels\\earth\\hub\\beach"), "levels\\earth\\cavern\\cavent"));
        Assert.Equal(Retail, LevelSelect.WithoutLevel(Retail, "levels\\earth\\hub\\nowhere"));
    }

    private TextFile AddLevelSelect()
    {
        var file = _project.Add(new TextFile(_project.Project.GlobalPackagePS2.URI, true, "startup_levelselect.txt", LevelSelect.FileName, Retail)
            { GlobalPath = "Startup" }, LevelSelect.FileName);
        file.Serialize(SerializationFlags.SaveData);
        return file;
    }

    private void AddCrashAndASurface()
    {
        var crash = _project.Add(new GameObject(), "Crash", 0x0);
        crash.SetData(new GameObjectData(crash) { Name = "Crash" });
        var surface = new CollisionSurface { Chunk = "default" };
        surface.Parameters.Add(CollisionSurface.EditorColorParameter, CollisionSurface.DefaultColors[0]);
        _project.Add(surface, "Surface", 0x0);
    }

    [AvaloniaFact]
    public async Task ChunksMadeInTTLabAreAddedAndDeletedOnesTakenOut()
    {
        var package = _project.Project.Ps2Package;
        _project.BuildProjectTree(Path.Combine(package.Name, "levels", "mymod"));
        var file = AddLevelSelect();
        AddCrashAndASurface();
        var world = _project.GetFolder(package, "levels", "mymod");

        var chunk = (LevelChunk)AssetFactory.CreateAsset(typeof(LevelChunk), world, "start", string.Empty, TwinIdGeneratorServiceProvider.GetGenerator<LevelChunk>(),
            asset => AssetDataFactory.CreateChunkData(world, asset))!;

        var expected = LevelSelect.WithLevel(Retail, "start", $"levels\\mymod\\start");
        Assert.Equal(expected, ((IAsset)file).GetData<TextFileData>().Text);
        Assert.Equal(expected, File.ReadAllText(file.FullDataPath, System.Text.Encoding.Latin1));

        Assert.True(await AssetDeletion.DeleteAsync(chunk));

        Assert.Equal(Retail, File.ReadAllText(file.FullDataPath, System.Text.Encoding.Latin1));
    }

    // Play builds the level select's chunks the disc doesn't have, they wouldn't load otherwise
    [Fact]
    public void PlayBuildsTheListedChunksTheDiscLacks()
    {
        var package = _project.Project.Ps2Package;
        var beach = _project.Add(new LevelChunk { AdditionalPath = Path.Combine("levels", "earth", "hub", "beach") }, "beach", package: package);
        var start = _project.Add(new LevelChunk { AdditionalPath = Path.Combine("levels", "mymod", "start") }, "start", package: package);
        _project.Add(new LevelChunk { AdditionalPath = Path.Combine("levels", "mymod", "unlisted") }, "unlisted", package: package);
        var levelSelect = LevelSelect.WithLevel(Retail, "start", "levels\\mymod\\start");
        HashSet<string> archive = [Pcsx2DevFolder.GamePath("Levels\\Earth\\Hub\\Beach.rm2")];

        Assert.Equal([start], Pcsx2Service.ChunksTheDiscLacks(_project.Project, levelSelect, archive));
        Assert.Equal([beach, start], Pcsx2Service.ChunksTheDiscLacks(_project.Project, levelSelect, new HashSet<string>()));
    }
}
