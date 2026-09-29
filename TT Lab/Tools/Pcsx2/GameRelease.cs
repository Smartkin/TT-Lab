using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TT_Lab.Tools.Pcsx2;

/// <summary>
/// A release of the PS2 version TT Lab plays in PCSX2, told apart by SYSTEM.CNF's boot file and version and checked by its executable's
/// CRC. The addresses were found by matching the PAL release's functions in the others (Ghidra Stuff/Engine_RE, PCSX2 section): the
/// code is the same, the NTSC releases' game controller lacks the PAL one's first 8 bytes
/// </summary>
/// <param name="LoosePrefix">The "cdrom0:\" literal <c>StartLoadingFileFromDisk_</c> puts in front of every file</param>
/// <param name="LaunchArguments">The built-in "rb batch=Crash6\Crash" <c>Main</c> always parses</param>
/// <param name="StartChunkGap">Unused bytes after .vutext, at least <see cref="GameExecutable.MaxChunkPathLength"/> + 1 of them</param>
/// <param name="StartChunkLoad">The lui a1 of the lui/addiu pair handing <c>CopyToString(&amp;G_StartChunk_Path, ...)</c> the Beach literal</param>
/// <param name="StatesOffset">Where the game controller keeps its 64 bit word of states</param>
/// <param name="StartTimerOffset">The game controller's timer of the start's title card and notices</param>
public sealed record GameRelease(
    string Name,
    string Executable,
    string Version,
    UInt32 Crc,
    UInt32 PatchedCrc,
    UInt32 LoosePrefix,
    UInt32 LaunchArguments,
    UInt32 StartChunkGap,
    UInt32 StartChunkLoad,
    UInt32 BeachLiteral,
    UInt32 GameControllerPointer,
    UInt32 StatesOffset,
    UInt32 StartTimerOffset,
    UInt32 ChunkManagerPointer)
{
    public static readonly GameRelease Pal = new("PAL 1.01", "SLES_525.68", "1.01", 0x1510E1D1, 0x77AB0001,
        0x3069c0, 0x2ec400, 0x2e6ed0, 0x178930, 0x2f5708, 0x309888, 0x8, 0x14, 0x30a0a8)
    {
        LevelSelectPatch = "TT_Lab.Tools.Pcsx2.LevelSelectPal.pnach"
    };

    public static readonly GameRelease Ntsc100 = new("NTSC 1.00", "SLUS_209.09", "1.00", 0xB318AA3C, 0x77AB0002,
        0x305fe0, 0x2ebb00, 0x2e6510, 0x178740, 0x2f4e28, 0x308f08, 0x0, 0xC, 0x309708);

    public static readonly GameRelease Ntsc200 = new("NTSC 2.00", "SLUS_209.09", "2.00", 0x8CFAB4EA, 0x77AB0003,
        0x306610, 0x2ec080, 0x2e6b50, 0x178818, 0x2f53a8, 0x309508, 0x0, 0xC, 0x309d08);

    public static IReadOnlyList<GameRelease> All { get; } = [Pal, Ntsc100, Ntsc200];

    // The resource of the pnach Play applies to the patched executable (Ghidra Stuff/Engine_RE/pnach, build.py --tt-lab writes it): a
    // level select in the pause menu reading Startup\LevelSelect.txt, and save and load state. Only made for the PAL release
    public string? LevelSelectPatch { get; init; }

    public static string Supported => string.Join(", ", All.Select(release => release.Name));

    /// <summary>
    /// The release of a disc folder, by SYSTEM.CNF's boot file and version, its executable checked by its CRC
    /// </summary>
    public static GameRelease Detect(string discContentPath)
    {
        var systemCnf = DiscPath.Find(discContentPath, "SYSTEM.CNF") ?? throw new FileNotFoundException($"The disc folder {discContentPath} has no SYSTEM.CNF");
        var (executable, version) = ReadSystemCnf(File.ReadAllText(systemCnf));
        var release = All.FirstOrDefault(item => item.Executable.Equals(executable, StringComparison.OrdinalIgnoreCase) && item.Version == version)
            ?? throw new InvalidDataException($"The disc is {executable} version {version}, only {Supported} can be played in PCSX2");
        var executablePath = DiscPath.Find(discContentPath, executable) ?? throw new FileNotFoundException($"The disc folder has no {executable}");
        var crc = GameExecutable.Crc(File.ReadAllBytes(executablePath));
        if (crc != release.Crc)
        {
            throw new InvalidDataException($"{executable} isn't the {release.Name} release's executable (CRC {crc:X8}, it's {release.Crc:X8}), only the retail executables can be played in PCSX2");
        }

        return release;
    }

    // BOOT2 = cdrom0:\SLUS_209.09;1 and VER = 2.00
    internal static (string Executable, string Version) ReadSystemCnf(string text)
    {
        string? executable = null;
        string? version = null;
        foreach (var line in text.Split('\n'))
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var value = parts[1].Trim();
            switch (parts[0].Trim().ToUpperInvariant())
            {
                case "BOOT2":
                    executable = value[(value.LastIndexOfAny([':', '\\']) + 1)..].Split(';')[0];
                    break;
                case "VER":
                    version = value;
                    break;
            }
        }

        if (executable == null || version == null)
        {
            throw new InvalidDataException("SYSTEM.CNF names no boot file or version");
        }

        return (executable, version);
    }
}

/// <summary>
/// Files of a disc folder whatever their case: PAL extracts have Crash6/Crash.BH, NTSC ones CRASH6/CRASH.BH
/// </summary>
public static class DiscPath
{
    public static string? Find(string folder, params string[] segments)
    {
        var current = folder;
        foreach (var segment in segments)
        {
            if (!Directory.Exists(current))
            {
                return null;
            }

            var exact = Path.Combine(current, segment);
            if (File.Exists(exact) || Directory.Exists(exact))
            {
                current = exact;
                continue;
            }

            var found = Directory.EnumerateFileSystemEntries(current).FirstOrDefault(entry => Path.GetFileName(entry).Equals(segment, StringComparison.OrdinalIgnoreCase));
            if (found == null)
            {
                return null;
            }

            current = found;
        }

        return current;
    }
}
