using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;

namespace TT_Lab.Project.Build;

// The PS2 disc folder's Crash.BH/BD: the archive the last build made, or the game's own before the first one. A chunk a build profile
// leaves out and that was never built is taken from it, the build's archive is made of the build folder alone and needs every chunk
public sealed class DiscArchive
{
    private readonly string _bdPath;
    private readonly Dictionary<string, BHRecord> _records = new(StringComparer.Ordinal);

    private DiscArchive(string bdPath, IEnumerable<BHRecord> records)
    {
        _bdPath = bdPath;
        foreach (var record in records)
        {
            _records.TryAdd(Normalize(record.Path), record);
        }
    }

    public static DiscArchive? Open(string? discContentPath)
    {
        if (string.IsNullOrEmpty(discContentPath))
        {
            return null;
        }

        var bhPath = Path.Combine(discContentPath, "Crash6", "Crash.BH");
        var bdPath = Path.Combine(discContentPath, "Crash6", "Crash.BD");
        if (!File.Exists(bhPath) || !File.Exists(bdPath))
        {
            return null;
        }

        try
        {
            return new DiscArchive(bdPath, PS2BD.ReadRecords(bhPath));
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException)
        {
            Log.WriteLine($"The disc's archive couldn't be read: {ex.Message}", Log.LogType.Warning);
            return null;
        }
    }

    // The game's archive has Levels\AltEarth\Core\treasure.rm2 where the build writes Levels/altearth/core/treasure.rm2
    internal static string Normalize(string path)
    {
        return path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();
    }

    public bool Contains(string archivePath) => _records.ContainsKey(Normalize(archivePath));

    // Whether the archive had every file, nothing is written otherwise
    public bool TryCopy(IReadOnlyList<(string ArchivePath, string Destination)> files)
    {
        foreach (var (archivePath, _) in files)
        {
            if (!Contains(archivePath))
            {
                return false;
            }
        }

        using var bd = new FileStream(_bdPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        foreach (var (archivePath, destination) in files)
        {
            var record = _records[Normalize(archivePath)];
            var bytes = new byte[record.Length];
            bd.Position = record.Offset;
            bd.ReadExactly(bytes);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, bytes);
        }

        return true;
    }
}
