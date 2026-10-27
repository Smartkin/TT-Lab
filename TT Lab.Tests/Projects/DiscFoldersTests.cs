using System.Text;
using TT_Lab.AssetData.Global;
using TT_Lab.Assets.Global;
using TT_Lab.Project;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;
using Path = System.IO.Path;

namespace TT_Lab.Tests.Projects;

// The game's files are found whatever the case of their folders: NTSC's archive is CRASH6/CRASH.BD and a disc copied by another program can
// have its folders in any case, which the unpack read by the PAL disc's names and Linux didn't find (the PS2 version not made, the Xbox one
// without its startup or levels). The Xbox disc's files are read in the PS2 archive's order, the startup's first
[Collection(ProjectCollection.Name)]
public sealed class DiscFoldersTests : IDisposable
{
    private readonly TestProject _project = new();
    private readonly string _discs = Path.Combine(Path.GetTempPath(), "TT Lab Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _project.Dispose();
        if (Directory.Exists(_discs))
        {
            Directory.Delete(_discs, true);
        }
    }

    private string Disc(string name) => Path.Combine(_discs, name);

    private static void AddFiles(string folder, params (string Path, string Text)[] files)
    {
        foreach (var (path, text) in files)
        {
            var file = Path.Combine(folder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, Encoding.Latin1.GetBytes(text));
        }
    }

    // The PS2 disc's archive of the files under the names given, packed the way a build packs its files
    private string Ps2Disc(string folder, string header, string data, params (string Path, string Text)[] files)
    {
        var built = Disc("built");
        AddFiles(built, files);
        var disc = Disc("ps2");
        Directory.CreateDirectory(Path.Combine(disc, folder));
        using var bh = File.Create(Path.Combine(disc, folder, header));
        using var bd = File.Create(Path.Combine(disc, folder, data));
        PS2BD.WriteArchive(built, bh, bd);
        return disc;
    }

    // The text files unpacked, by their folder and name
    private Dictionary<string, string> UnpackedTexts()
    {
        return _project.AssetManager.GetAllAssetsOf<TextFile>()
            .ToDictionary(text => text.GlobalPath.Replace('\\', '/') + "/" + text.InvariantName, text => ((TextFileData)text.GetData()).Text);
    }

    [Theory]
    [InlineData("Crash6", "Crash.BH", "Crash.BD")]
    [InlineData("CRASH6", "CRASH.BH", "CRASH.BD")]
    [InlineData("crash6", "crash.bh", "crash.bd")]
    public void ThePs2ArchiveIsFoundWhateverItsCase(string folder, string header, string data)
    {
        _project.Project.DiscContentPathPS2 = Ps2Disc(folder, header, data, ("Startup/LevelSelect.txt", "\"beach\""), ("Language/English/Text.txt", "Hello"));
        using var gate = new MemoryGate(1L << 30);

        _project.Project.UnpackAssetsPS2(gate);

        var texts = UnpackedTexts();
        Assert.Equal("\"beach\"", texts["Startup/LevelSelect"]);
        Assert.Equal("Hello", texts["Language/English/Text"]);
    }

    [Fact]
    public void APs2DiscWithoutItsArchiveIsNamed()
    {
        var disc = Disc("ps2");
        AddFiles(disc, ("SYSTEM.CNF", "BOOT2 = cdrom0:\\SLES_525.68;1"), ("Crash6/Crash.BH", "header"));
        _project.Project.DiscContentPathPS2 = disc;
        using var gate = new MemoryGate(1L << 30);

        var missing = Assert.Throws<ProjectException>(() => _project.Project.UnpackAssetsPS2(gate));

        Assert.Contains(disc, missing.Message);
        Assert.Contains("Crash6/Crash.BD", missing.Message);
    }

    // The default chunk shares items with levels and the chunk an item is first found in has its asset: sorted all together (TT Lab 1.1.0's
    // order, which repairing a project it made needs) the levels came first. The folders are named the game's way, like Windows found them
    [Fact]
    public void TheXboxDiscsStartupIsReadFirstUnderTheGamesNames()
    {
        var disc = Disc("xbox");
        AddFiles(disc, ("default.xbe", ""), ("Levels/Earth/Hub/beach.rmx", ""), ("language/English/Text.txt", "Hola"), ("Extras/Gallery.psm", ""),
            ("STARTUP/Default.rmx", ""), ("Levels/Earth/Hub/Beach.smx", ""), ("STARTUP/LevelSelect.txt", ""), ("CrashTwinsanity/Startup/Default.rmx", ""));

        Assert.Equal(["Startup\\Default.rmx", "Startup\\LevelSelect.txt", "Levels\\Earth\\Hub\\beach.rmx", "Levels\\Earth\\Hub\\Beach.smx", "Extras\\Gallery.psm",
            "Language\\English\\Text.txt"], TT_Lab.Project.Project.GetXboxDiscFiles(disc).Select(file => file.Path));
        Assert.Equal(["Extras\\Gallery.psm", "Language\\English\\Text.txt", "Levels\\Earth\\Hub\\beach.rmx", "Levels\\Earth\\Hub\\Beach.smx", "Startup\\Default.rmx",
            "Startup\\LevelSelect.txt"], TT_Lab.Project.Project.GetXboxDiscFiles(disc, oldFileOrder: true).Select(file => file.Path));
        Assert.Equal(Encoding.Latin1.GetBytes("Hola"), TT_Lab.Project.Project.GetXboxDiscFiles(disc).Single(file => file.Path.EndsWith("Text.txt")).Read());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheXboxDiscsFoldersAreFoundWhateverTheirCase(bool oldFileOrder)
    {
        var disc = Disc("xbox");
        AddFiles(disc, ("default.xbe", ""), ("STARTUP/LevelSelect.txt", "\"beach\""), ("language/English/Text.txt", "Hola"));
        _project.Project.DiscContentPathXbox = disc;
        using var gate = new MemoryGate(1L << 30);

        _project.Project.UnpackAssetsXbox(gate, oldFileOrder);

        var texts = UnpackedTexts();
        Assert.Equal("\"beach\"", texts["Startup/LevelSelect"]);
        Assert.Equal("Hola", texts["Language/English/Text"]);
        Assert.True(_project.Project.GlobalPackageXbox.Enabled);
    }
}
