using TT_Lab.Libraries;
using TT_Lab.Tests.Support;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;

namespace TT_Lab.Tests.Build;

// Build ISO packs the build's files into the disc folder's archive, the last build's that DiscArchive and Play read, and makes the image of
// that folder, which takes the last one's place once it's whole. The archive goes over the disc's own names (NTSC's CRASH6/CRASH.BD, which
// Linux didn't find) and the disc's archive stays as it was until the pack is done (it was truncated before anything was read)
[Collection(ProjectCollection.Name)]
public sealed class Ps2ImageTests : IDisposable
{
    private readonly TestProject _project = new();

    public void Dispose() => _project.Dispose();

    private TT_Lab.Project.Project Project => _project.Project;

    private string Disc => Path.Combine(Project.ProjectPath, "disc", "ps2");

    private string Archives => Path.Combine(Project.ProjectPath, "build", "archives");

    private string Image => Path.Combine(Project.ProjectPath, "build", "image", "Test.iso");

    // The game's archive under the names given, bytes standing for it
    private void AddDisc(string crash6 = "Crash6", string header = "Crash.BH", string data = "Crash.BD")
    {
        Directory.CreateDirectory(Path.Combine(Disc, crash6));
        File.WriteAllBytes(Path.Combine(Disc, crash6, header), [1, 5, 0, 0, 0xAA]);
        File.WriteAllBytes(Path.Combine(Disc, crash6, data), [0xBB, 0xBB, 0xBB]);
        File.WriteAllText(Path.Combine(Disc, "SYSTEM.CNF"), "BOOT2 = cdrom0:\\SLES_525.68;1");
        Project.DiscContentPathPS2 = Disc;
    }

    private void AddBuiltFile(string path, byte[] bytes)
    {
        var file = Path.Combine(Archives, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes);
    }

    private static string[] NamesIn(string folder) => Directory.GetFileSystemEntries(folder).Select(entry => Path.GetFileName(entry)).Order(StringComparer.Ordinal).ToArray();

    [Theory]
    [InlineData("Crash6", "Crash.BH", "Crash.BD")]
    [InlineData("CRASH6", "CRASH.BH", "CRASH.BD")]
    [InlineData("crash6", "crash.bh", "crash.bd")]
    public void TheArchiveGoesOverTheDiscsOwnNames(string crash6, string header, string data)
    {
        AddDisc(crash6, header, data);
        AddBuiltFile("Startup/Default.rm2", [1, 2, 3]);
        AddBuiltFile("Levels/earth/hub/beach.rm2", [4, 5]);
        Project.MakePs2Image = (_, image) => File.WriteAllBytes(image, []);

        Project.CreatePs2ArchivesAndIso();

        // Nothing next to them, no folder of other names
        Assert.Equal(new[] { crash6, "SYSTEM.CNF" }.Order(StringComparer.Ordinal), NamesIn(Disc));
        Assert.Equal(new[] { header, data }.Order(StringComparer.Ordinal), NamesIn(Path.Combine(Disc, crash6)));
        using var builtHeader = new MemoryStream();
        using var builtData = new MemoryStream();
        PS2BD.WriteArchive(Archives, builtHeader, builtData);
        Assert.Equal(builtHeader.ToArray(), File.ReadAllBytes(Path.Combine(Disc, crash6, header)));
        Assert.Equal(builtData.ToArray(), File.ReadAllBytes(Path.Combine(Disc, crash6, data)));
    }

    [Fact]
    public void APackThatFailsLeavesTheDiscsArchiveAsItWas()
    {
        AddDisc();
        var header = File.ReadAllBytes(Path.Combine(Disc, "Crash6", "Crash.BH"));
        var data = File.ReadAllBytes(Path.Combine(Disc, "Crash6", "Crash.BD"));
        var imaged = false;
        Project.MakePs2Image = (_, _) => imaged = true;

        // Nothing built yet
        Assert.Throws<DirectoryNotFoundException>(() => Project.CreatePs2ArchivesAndIso());
        Assert.Equal(data, File.ReadAllBytes(Path.Combine(Disc, "Crash6", "Crash.BD")));

        // An archive past what its header holds is refused by the file that takes it there (sparse where the file system makes it so)
        AddBuiltFile("Startup/Default.rm2", [1, 2, 3]);
        var huge = Path.Combine(Archives, "Levels", "earth", "hub", "huge.rm2");
        Directory.CreateDirectory(Path.GetDirectoryName(huge)!);
        using (var stream = File.Create(huge))
        {
            stream.SetLength((long)int.MaxValue + 1);
        }

        var refusal = Assert.Throws<IOException>(() => Project.CreatePs2ArchivesAndIso());

        Assert.Contains(huge, refusal.Message);
        Assert.Equal(header, File.ReadAllBytes(Path.Combine(Disc, "Crash6", "Crash.BH")));
        Assert.Equal(data, File.ReadAllBytes(Path.Combine(Disc, "Crash6", "Crash.BD")));
        // The files it was written into go
        Assert.Equal(["Crash.BD", "Crash.BH"], NamesIn(Path.Combine(Disc, "Crash6")));
        Assert.False(imaged);
    }

    // The data goes in first: a header that can't be moved over the old one (Windows, while another program has it open) left the new data
    // with the old header
    [Fact]
    public void AHeaderThatCantBeMovedInLeavesTheOldDataWithIt()
    {
        AddDisc();
        var data = File.ReadAllBytes(Path.Combine(Disc, "Crash6", "Crash.BD"));
        AddBuiltFile("Startup/Default.rm2", [1, 2, 3]);
        var imaged = false;
        Project.MakePs2Image = (_, _) => imaged = true;
        // Nothing can be moved over a folder
        File.Delete(Path.Combine(Disc, "Crash6", "Crash.BH"));
        Directory.CreateDirectory(Path.Combine(Disc, "Crash6", "Crash.BH", "in use"));

        var failure = Record.Exception(() => Project.CreatePs2ArchivesAndIso());

        Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString());
        Assert.Equal(data, File.ReadAllBytes(Path.Combine(Disc, "Crash6", "Crash.BD")));
        Assert.Equal(["Crash.BD", "Crash.BH"], NamesIn(Path.Combine(Disc, "Crash6")));
        Assert.False(imaged);
    }

    [Fact]
    public void AnImageThatIsntMadeLeavesTheLastOne()
    {
        AddDisc();
        AddBuiltFile("Startup/Default.rm2", [1, 2, 3]);
        Directory.CreateDirectory(Path.GetDirectoryName(Image)!);
        File.WriteAllBytes(Image, [7]);
        var part = Image + ".part";
        Project.MakePs2Image = (_, image) =>
        {
            Assert.Equal(part, image);
            File.WriteAllBytes(image, [1]);
            throw new IOException("PS2ImageMaker couldn't make the image");
        };

        Assert.Throws<IOException>(() => Project.CreatePs2ArchivesAndIso());

        Assert.Equal([7], File.ReadAllBytes(Image));
        Assert.False(File.Exists(part));

        // A packer that ran away goes on writing the image it was making: it stays where the error says it is
        Project.MakePs2Image = (_, image) =>
        {
            File.WriteAllBytes(image, [2]);
            throw new Ps2ImageRunawayException("PS2ImageMaker ran away");
        };

        Assert.Throws<Ps2ImageRunawayException>(() => Project.CreatePs2ArchivesAndIso());

        Assert.Equal([7], File.ReadAllBytes(Image));
        Assert.True(File.Exists(part));
    }

    [Fact]
    public void AMadeImageTakesTheLastOnesPlace()
    {
        AddDisc();
        AddBuiltFile("Startup/Default.rm2", [1, 2, 3]);
        Directory.CreateDirectory(Path.GetDirectoryName(Image)!);
        File.WriteAllBytes(Image, [7]);
        string? madeOf = null;
        Project.MakePs2Image = (folder, image) =>
        {
            madeOf = folder;
            File.WriteAllBytes(image, [0x50, 0x53, 0x32]);
        };

        Project.CreatePs2ArchivesAndIso();

        Assert.Equal(Disc, madeOf);
        Assert.Equal([0x50, 0x53, 0x32], File.ReadAllBytes(Image));
        Assert.False(File.Exists(Image + ".part"));
    }
}
