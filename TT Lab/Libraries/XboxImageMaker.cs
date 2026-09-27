using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TT_Lab.Libraries;

/// <summary>
/// Writes a folder as an Xbox disc image (XDVDFS), the format Xbox emulators and softmodded consoles load games from
/// </summary>
/// <remarks>
/// The image starts with 32 empty sectors and the volume descriptor. Every directory is a table of entries forming a binary search
/// tree ordered by their names without case, entries don't cross sectors and the unused bytes are 0xFF. Files start on sectors
/// </remarks>
public static class XboxImageMaker
{
    public const Int32 SectorSize = 0x800;
    private const Int64 VolumeDescriptorSector = 32;
    private const Byte DirectoryAttribute = 0x10;
    private const Byte FileAttribute = 0x20;
    private const Int32 EntryHeaderLength = 14;
    private static readonly Byte[] Magic = Encoding.ASCII.GetBytes("MICROSOFT*XBOX*MEDIA");

    private sealed class Entry
    {
        public required String Name { get; init; }
        public String? SourcePath { get; init; }
        public Int64 Length { get; set; }
        public List<Entry>? Children { get; init; }
        public Int64 Sector { get; set; }
        public Byte[]? Table { get; set; }

        public Boolean IsDirectory => Children != null;
    }

    public static void Make(String sourceFolder, String imagePath, Action<Double>? progress = null)
    {
        var root = ReadFolder(sourceFolder, string.Empty);
        // Tables only depend on their entries' sizes, their sectors get filled in once everything has one
        var directories = new List<Entry>();
        CollectDirectories(root, directories);
        foreach (var directory in directories)
        {
            directory.Table = BuildTable(directory);
            directory.Length = directory.Table.Length;
        }

        var sector = VolumeDescriptorSector + 1;
        foreach (var directory in directories)
        {
            directory.Sector = sector;
            sector += SectorsOf(directory.Length);
        }

        var files = directories.SelectMany(directory => directory.Children!).Where(entry => !entry.IsDirectory).ToList();
        foreach (var file in files)
        {
            file.Sector = file.Length == 0 ? 0 : sector;
            sector += SectorsOf(file.Length);
        }

        foreach (var directory in directories)
        {
            directory.Table = BuildTable(directory);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(imagePath))!);
        using var image = new FileStream(imagePath, FileMode.Create, FileAccess.Write);
        image.SetLength(sector * SectorSize);
        WriteVolumeDescriptor(image, root);
        foreach (var directory in directories)
        {
            image.Position = directory.Sector * SectorSize;
            image.Write(directory.Table!);
        }

        var totalLength = Math.Max(1, files.Sum(file => file.Length));
        var written = 0L;
        foreach (var file in files.Where(file => file.Length > 0))
        {
            image.Position = file.Sector * SectorSize;
            using var source = File.OpenRead(file.SourcePath!);
            source.CopyTo(image);
            written += file.Length;
            progress?.Invoke((Double)written / totalLength);
        }
    }

    private static Entry ReadFolder(String path, String name)
    {
        var children = new List<Entry>();
        foreach (var directory in Directory.EnumerateDirectories(path))
        {
            var child = ReadFolder(directory, Path.GetFileName(directory));
            // The file system has no way to store an empty directory's table
            if (child.Children!.Count > 0)
            {
                children.Add(child);
            }
        }

        children.AddRange(Directory.EnumerateFiles(path).Select(file => new Entry
        {
            Name = Path.GetFileName(file),
            SourcePath = file,
            Length = new FileInfo(file).Length
        }));
        foreach (var child in children)
        {
            if (Encoding.ASCII.GetByteCount(child.Name) > Byte.MaxValue || child.Name.Any(c => c > 0x7F))
            {
                throw new InvalidOperationException($"{child.Name} can't be stored on an Xbox disc, its name has to be ASCII and at most 255 characters");
            }
        }

        return new Entry { Name = name, Children = children.OrderBy(child => child.Name, NameComparer.Instance).ToList() };
    }

    private static void CollectDirectories(Entry directory, List<Entry> directories)
    {
        directories.Add(directory);
        foreach (var child in directory.Children!.Where(child => child.IsDirectory))
        {
            CollectDirectories(child, directories);
        }
    }

    // The entries in the order they're written: every subtree's middle entry comes before its left and right halves
    private static Byte[] BuildTable(Entry directory)
    {
        var order = new List<(Entry Entry, Int32 Left, Int32 Right)>();
        AddSubtree(directory.Children!, 0, directory.Children!.Count, order);
        var offsets = new Int32[order.Count];
        var position = 0;
        for (var i = 0; i < order.Count; i++)
        {
            var length = GetEntryLength(order[i].Entry);
            if (position / SectorSize != (position + length - 1) / SectorSize)
            {
                position = (position / SectorSize + 1) * SectorSize;
            }

            offsets[i] = position;
            position += length;
        }

        var table = new Byte[SectorsOf(position) * SectorSize];
        Array.Fill(table, (Byte)0xFF);
        using var writer = new BinaryWriter(new MemoryStream(table));
        for (var i = 0; i < order.Count; i++)
        {
            var (entry, left, right) = order[i];
            writer.BaseStream.Position = offsets[i];
            writer.Write((UInt16)(left < 0 ? 0 : offsets[left] / 4));
            writer.Write((UInt16)(right < 0 ? 0 : offsets[right] / 4));
            writer.Write((UInt32)entry.Sector);
            writer.Write((UInt32)entry.Length);
            writer.Write(entry.IsDirectory ? DirectoryAttribute : FileAttribute);
            var name = Encoding.ASCII.GetBytes(entry.Name);
            writer.Write((Byte)name.Length);
            writer.Write(name);
        }

        return table;
    }

    // Returns the index of the subtree's root in the order
    private static Int32 AddSubtree(List<Entry> entries, Int32 start, Int32 end, List<(Entry Entry, Int32 Left, Int32 Right)> order)
    {
        if (start >= end)
        {
            return -1;
        }

        var middle = (start + end) / 2;
        var index = order.Count;
        order.Add((entries[middle], -1, -1));
        var left = AddSubtree(entries, start, middle, order);
        var right = AddSubtree(entries, middle + 1, end, order);
        order[index] = (entries[middle], left, right);
        return index;
    }

    private static Int32 GetEntryLength(Entry entry)
    {
        return (EntryHeaderLength + Encoding.ASCII.GetByteCount(entry.Name) + 3) & ~3;
    }

    private static void WriteVolumeDescriptor(Stream image, Entry root)
    {
        image.Position = VolumeDescriptorSector * SectorSize;
        using var writer = new BinaryWriter(image, Encoding.ASCII, true);
        writer.Write(Magic);
        writer.Write((UInt32)root.Sector);
        writer.Write((UInt32)root.Length);
        writer.Write(DateTime.UtcNow.ToFileTimeUtc());
        image.Position = VolumeDescriptorSector * SectorSize + 0x7EC;
        writer.Write(Magic);
    }

    private static Int64 SectorsOf(Int64 length)
    {
        return (length + SectorSize - 1) / SectorSize;
    }

    // The Xbox looks names up without their case
    internal sealed class NameComparer : IComparer<String>
    {
        public static readonly NameComparer Instance = new();

        public Int32 Compare(String? x, String? y)
        {
            return String.CompareOrdinal(x?.ToUpperInvariant(), y?.ToUpperInvariant());
        }
    }
}
