using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Archives;

namespace TT_Lab.Tools.Pcsx2;

/// <summary>
/// The folder PCSX2 plays a project from (<c>&lt;project&gt;/build/pcsx2</c>), the root of its host file system: the patched
/// executable, every file of the PS2 archive on its own the way the game asks for it (upper case: LEVELS/EARTH/HUB/BEACH.RM2), and the
/// disc's sound banks, which the game streams through the same loader (CRASH6/MUSIC.MB). Only what changed gets written
/// </summary>
public sealed class Pcsx2DevFolder(string path)
{
    // The disc archive's sizes and times when its files were taken out, they're only taken out again once it changed
    private const string ArchiveStampName = ".archive";

    public string FolderPath { get; } = path;

    public string ExecutablePath(GameRelease release) => Path.Combine(FolderPath, release.Executable);

    // PCSX2 on Linux keeps the case of names, the game asks for them in upper case
    public static string GamePath(string relativePath) => relativePath.Replace('\\', '/').TrimStart('/').ToUpperInvariant();

    /// <summary>
    /// The disc's executable (<see cref="GameRelease.Detect"/>) patched to start in the chunk
    /// </summary>
    public void WriteExecutable(GameRelease release, string discContentPath, string startChunk)
    {
        var retail = File.ReadAllBytes(DiscPath.Find(discContentPath, release.Executable)!);
        WriteIfChanged(ExecutablePath(release), GameExecutable.Patch(release, retail, startChunk));
    }

    /// <summary>
    /// Every file of the disc's archive (Crash6/Crash.BH and BD), taken out again only when the archive changed
    /// </summary>
    public int ExtractArchive(string discContentPath)
    {
        var header = DiscPath.Find(discContentPath, "Crash6", "Crash.BH") ?? throw new FileNotFoundException($"The disc folder {discContentPath} has no Crash6/Crash.BH");
        var data = DiscPath.Find(discContentPath, "Crash6", "Crash.BD") ?? throw new FileNotFoundException($"The disc folder {discContentPath} has no Crash6/Crash.BD");
        var stamp = string.Join(' ', new[] { header, data }.Select(file => $"{new FileInfo(file).Length}:{File.GetLastWriteTimeUtc(file).Ticks}"));
        var stampPath = Path.Combine(FolderPath, ArchiveStampName);
        if (File.Exists(stampPath) && File.ReadAllText(stampPath) == stamp)
        {
            return 0;
        }

        var written = 0;
        using (var archive = new FileStream(data, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            foreach (var record in PS2BD.ReadRecords(header))
            {
                var bytes = new byte[record.Length];
                archive.Position = record.Offset;
                archive.ReadExactly(bytes);
                if (WriteIfChanged(Path.Combine(FolderPath, GamePath(record.Path)), bytes))
                {
                    written++;
                }
            }
        }

        File.WriteAllText(stampPath, stamp);
        return written;
    }

    /// <summary>
    /// The files the disc's archive has, the way the game asks for them
    /// </summary>
    public static HashSet<string> ArchiveFiles(string discContentPath)
    {
        var header = DiscPath.Find(discContentPath, "Crash6", "Crash.BH") ?? throw new FileNotFoundException($"The disc folder {discContentPath} has no Crash6/Crash.BH");
        return PS2BD.ReadRecords(header).Select(record => GamePath(record.Path)).ToHashSet();
    }

    /// <summary>
    /// A file of the project's in place of the archive's, like Startup/LevelSelect.txt
    /// </summary>
    public bool WriteFile(string relativePath, byte[] bytes) => WriteIfChanged(Path.Combine(FolderPath, GamePath(relativePath)), bytes);

    /// <summary>
    /// The build's files (build/archives, what a build packs into the archive) over the archive's
    /// </summary>
    public int CopyBuiltFiles(string buildFilesPath)
    {
        if (!Directory.Exists(buildFilesPath))
        {
            return 0;
        }

        return Directory.EnumerateFiles(buildFilesPath, "*", SearchOption.AllDirectories)
            .Count(file => CopyIfChanged(file, Path.Combine(FolderPath, GamePath(Path.GetRelativePath(buildFilesPath, file)))));
    }

    /// <summary>
    /// The music and speech banks next to the archive (Crash6/*.mh and *.mb), which twinmusic edits
    /// </summary>
    public int CopySoundBanks(string discContentPath)
    {
        var folder = DiscPath.Find(discContentPath, "Crash6");
        if (folder == null)
        {
            return 0;
        }

        return Directory.EnumerateFiles(folder)
            .Where(file => Path.GetExtension(file).ToLowerInvariant() is ".mh" or ".mb")
            .Count(file => CopyIfChanged(file, Path.Combine(FolderPath, "CRASH6", Path.GetFileName(file).ToUpperInvariant())));
    }

    // A copy gets its source's time, so a copy whose size and time still match is up to date. Written next to it and moved in, the
    // game never reads half a file
    private static bool CopyIfChanged(string source, string destination)
    {
        var sourceInfo = new FileInfo(source);
        var destinationInfo = new FileInfo(destination);
        if (destinationInfo.Exists && destinationInfo.Length == sourceInfo.Length && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".tmp";
        File.Copy(source, temporary, true);
        File.SetLastWriteTimeUtc(temporary, sourceInfo.LastWriteTimeUtc);
        MoveIn(temporary, destination);
        return true;
    }

    // Windows doesn't replace a file something has open, the game may be reading it
    private static void MoveIn(string temporary, string destination)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temporary, destination, true);
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 50)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static bool WriteIfChanged(string destination, byte[] bytes)
    {
        var info = new FileInfo(destination);
        if (info.Exists && info.Length == bytes.Length && File.ReadAllBytes(destination).AsSpan().SequenceEqual(bytes))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        MoveIn(temporary, destination);
        return true;
    }
}
