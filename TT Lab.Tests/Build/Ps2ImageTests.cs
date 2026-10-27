using TT_Lab.Libraries;
using TT_Lab.Tests.Support;

namespace TT_Lab.Tests.Build;

// Build ISO packs the build's files into the disc folder's archive and makes the image of that folder, which takes the last one's place once
// it's whole
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
