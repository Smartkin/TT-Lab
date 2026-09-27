using System.Text;
using TT_Lab.Libraries;

namespace TT_Lab.Tests.Build;

public sealed class XboxImageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"TTLabXboxImage_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private string Folder => Path.Combine(_root, "game");

    private void AddFile(string path, byte[] content)
    {
        var full = Path.Combine(Folder, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
    }

    // Finds a file the way the Xbox does: a binary search in every directory's tree, names compared without case
    private static (uint Sector, uint Size, byte Attributes)? Find(byte[] image, string path)
    {
        var (sector, size) = (BitConverter.ToUInt32(image, 0x10000 + 0x14), BitConverter.ToUInt32(image, 0x10000 + 0x18));
        (uint Sector, uint Size, byte Attributes)? found = null;
        foreach (var name in path.Split('/'))
        {
            var table = image.AsSpan((int)(sector * XboxImageMaker.SectorSize), (int)size);
            var offset = 0;
            found = null;
            while (true)
            {
                var entryName = Encoding.ASCII.GetString(table.Slice(offset + 14, table[offset + 13]));
                var comparison = string.CompareOrdinal(name.ToUpperInvariant(), entryName.ToUpperInvariant());
                if (comparison == 0)
                {
                    found = (BitConverter.ToUInt32(table.Slice(offset + 4, 4)), BitConverter.ToUInt32(table.Slice(offset + 8, 4)), table[offset + 12]);
                    break;
                }

                var next = BitConverter.ToUInt16(table.Slice(offset + (comparison < 0 ? 0 : 2), 2));
                if (next == 0)
                {
                    return null;
                }

                offset = next * 4;
            }

            (sector, size) = (found.Value.Sector, found.Value.Size);
        }

        return found;
    }

    [Fact]
    public void EveryFileIsFoundWithItsContent()
    {
        var random = new Random(5);
        var files = new Dictionary<string, byte[]>
        {
            ["default.xbe"] = [1, 2, 3],
            ["Startup/Default.rmx"] = Enumerable.Range(0, 5000).Select(_ => (byte)random.Next(256)).ToArray(),
            ["Startup/Empty.txt"] = [],
            ["Levels/Earth/Hub/beach.rmx"] = [9, 9]
        };
        // Enough names that the tree has several levels and its table more than one sector
        for (var i = 0; i < 150; i++)
        {
            files[$"Levels/Many/file_{i:D3}_with_a_long_name_{new string('x', i % 30)}.bin"] = [(byte)i];
        }

        foreach (var (path, content) in files)
        {
            AddFile(path, content);
        }

        Directory.CreateDirectory(Path.Combine(Folder, "EmptyFolder"));
        var imagePath = Path.Combine(_root, "game.iso");

        XboxImageMaker.Make(Folder, imagePath);

        var image = File.ReadAllBytes(imagePath);
        Assert.Equal("MICROSOFT*XBOX*MEDIA", Encoding.ASCII.GetString(image, 0x10000, 20));
        Assert.Equal("MICROSOFT*XBOX*MEDIA", Encoding.ASCII.GetString(image, 0x10000 + 0x7EC, 20));
        Assert.Equal(0, image.Length % XboxImageMaker.SectorSize);
        foreach (var (path, content) in files)
        {
            var entry = Find(image, path.ToUpperInvariant());
            Assert.NotNull(entry);
            Assert.Equal(0x20, entry.Value.Attributes);
            Assert.Equal(content, image.AsSpan((int)(entry.Value.Sector * XboxImageMaker.SectorSize), (int)entry.Value.Size).ToArray());
        }

        Assert.Equal(0x10, Find(image, "levels/earth")!.Value.Attributes);
        Assert.Null(Find(image, "EmptyFolder"));
        Assert.Null(Find(image, "Startup/Missing.rmx"));
    }
}
