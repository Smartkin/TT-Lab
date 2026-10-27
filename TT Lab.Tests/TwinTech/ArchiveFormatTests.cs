using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;

namespace TT_Lab.Tests.TwinTech;

// PS2BD.WriteArchive streams a build's files into the PS2 archive, which BuildRecords and Write made holding every file in memory (the
// whole archive, 930 MB for the PAL disc): the same bytes, a file at a time
public sealed class ArchiveFormatTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"TTLabArchive_{Guid.NewGuid():N}");

    public ArchiveFormatTests()
    {
        Directory.CreateDirectory(Archives);
        Directory.CreateDirectory(Output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string Archives => Path.Combine(_root, "archives");

    // Outside the folder packed, which every file under it goes into
    private string Output => Path.Combine(_root, "output");

    private static byte[] Bytes(Random random, int length)
    {
        var bytes = new byte[length];
        random.NextBytes(bytes);
        return bytes;
    }

    // A build's folder: nested folders, an empty file, a file without an extension and files longer than one read of a copy
    private Dictionary<string, byte[]> AddBuildFiles()
    {
        var random = new Random(7);
        var files = new Dictionary<string, byte[]>
        {
            ["Startup/Default.rm2"] = Bytes(random, 70_000),
            ["Startup/Empty.txt"] = [],
            ["Startup/NoExtension"] = Bytes(random, 1),
            ["Language/English/Text.txt"] = Bytes(random, 4096),
            ["Levels/Earth/Hub/beach.rm2"] = Bytes(random, 3 * 1024 * 1024 + 5),
            ["Levels/Earth/Hub/beach.sm2"] = Bytes(random, 1024 * 1024),
            ["Levels/Earth/Hub/Deeper/Still/far.sm2"] = Bytes(random, 333)
        };
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(Archives, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, content);
        }

        return files;
    }

    // What BuildRecords and Write make of the folder, the way Project.CreatePs2ArchivesAndIso packs it
    private (byte[] Bh, byte[] Bd) ArchiveOfTheOldWriter(string folder)
    {
        var bhPath = Path.Combine(Output, "Old.BH");
        var bdPath = Path.Combine(Output, "Old.BD");
        var archive = new PS2BD("", bhPath);
        using (var bdFile = new FileStream(bdPath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(bdFile))
        {
            archive.BuildRecords(folder);
            archive.Write(writer);
        }

        return (File.ReadAllBytes(bhPath), File.ReadAllBytes(bdPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheArchiveIsTheOneBuildRecordsAndWriteMake(bool folderEndsInASeparator)
    {
        AddBuildFiles();
        var folder = folderEndsInASeparator ? Archives + Path.DirectorySeparatorChar : Archives;
        var (oldBh, oldBd) = ArchiveOfTheOldWriter(folder);

        using var bh = new MemoryStream();
        using var bd = new MemoryStream();
        PS2BD.WriteArchive(folder, bh, bd);

        Assert.Equal(oldBh, bh.ToArray());
        Assert.Equal(oldBd, bd.ToArray());
        // They're the caller's to close
        Assert.True(bh.CanWrite);
        Assert.True(bd.CanWrite);
    }

    [Fact]
    public void AnEmptyFolderIsAnArchiveOfNoFiles()
    {
        var (oldBh, oldBd) = ArchiveOfTheOldWriter(Archives);

        using var bh = new MemoryStream();
        using var bd = new MemoryStream();
        PS2BD.WriteArchive(Archives, bh, bd);

        Assert.Equal(new byte[] { 0x01, 0x05, 0x00, 0x00 }, bh.ToArray());
        Assert.Equal(oldBh, bh.ToArray());
        Assert.Empty(bd.ToArray());
        Assert.Empty(oldBd);
    }

    [Fact]
    public void TheHeaderFindsEveryFilesBytesInTheArchive()
    {
        var files = AddBuildFiles();
        var bhPath = Path.Combine(Output, "Crash.BH");
        var bdPath = Path.Combine(Output, "Crash.BD");
        using (var bh = File.Create(bhPath))
        using (var bd = File.Create(bdPath))
        {
            PS2BD.WriteArchive(Archives, bh, bd);
        }

        var records = PS2BD.ReadRecords(bhPath);
        // Every file once, by its path in the folder with the game's separators
        Assert.Equal(files.Keys.Select(path => path.Replace('/', '\\')).Order(), records.Select(record => record.Path).Order());
        // In the order Directory.GetFiles lists them, the order BuildRecords always took them in (it lists them the way WriteArchive does
        // now, so comparing with it can't tell)
        var listed = Directory.GetFiles(Archives, "*.*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(Archives, file).Replace('/', '\\'));
        Assert.Equal(listed, records.Select(record => record.Path));
        foreach (var record in records)
        {
            Assert.Equal(files[record.Path.Replace('\\', '/')], PS2BD.ReadFile(bdPath, record));
        }

        // One after the other from the archive's start, nothing between them or after the last
        var end = 0L;
        foreach (var record in records)
        {
            Assert.Equal(end, record.Offset);
            end += record.Length;
        }

        Assert.Equal(end, new FileInfo(bdPath).Length);
    }

    // The header keeps offsets and lengths as Int32s, which BuildRecords wrapped around past 2 GB: a file longer than they hold (no bytes
    // before it) or one ending past them after the others (a file of 2 bytes at the folder's top, which Directory.GetFiles lists before its
    // folders' files) is refused by its name before any of it is read
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void AnArchivePastWhatItsHeaderHoldsIsRefusedNamingTheFile(int bytesBefore)
    {
        if (bytesBefore > 0)
        {
            File.WriteAllBytes(Path.Combine(Archives, "First.bin"), Bytes(new Random(7), bytesBefore));
        }

        var huge = Path.Combine(Archives, "Levels", "huge.rm2");
        Directory.CreateDirectory(Path.GetDirectoryName(huge)!);
        // Sparse where the file system makes it so, ending a byte past what the header holds
        using (var stream = File.Create(huge))
        {
            stream.SetLength((long)int.MaxValue + 1 - bytesBefore);
        }

        // Streams with room for the files before it alone: a byte more throws a NotSupportedException
        using var bh = new MemoryStream(Array.Empty<byte>());
        using var bd = new MemoryStream(new byte[bytesBefore]);
        var refusal = Assert.Throws<IOException>(() => PS2BD.WriteArchive(Archives, bh, bd));

        Assert.Contains(huge, refusal.Message);
        Assert.Equal(bytesBefore, bd.Position);
    }
}
