using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace TT_Lab.Tools.Pcsx2;

/// <summary>
/// A release's executable patched to play a project's files from a folder on the PC, verified in the PAL executable (Ghidra
/// Stuff/Engine_RE, PCSX2 section) and matched in the others (<see cref="GameRelease"/>): <c>StartLoadingFileFromDisk_</c> puts a
/// "cdrom0:\" literal in front of every file it loads, "host0:\" makes PCSX2 read them from the folder the executable is in. <c>Main</c>
/// always parses the built-in launch arguments, whatever the emulator passes, and without "batch=Crash6\Crash" the archive isn't used
/// and every file is read on its own. The code filling <c>G_StartChunk_Path</c> is pointed at unused bytes after .vutext that hold the
/// chunk to start in. A word appended to the file keeps the CRC PCSX2 knows the game by (the XOR of every word) at the release's
/// patched one whatever the chunk, so one game settings file turns the host file system and PINE on for it
/// </summary>
public static class GameExecutable
{
    public const Int32 MaxChunkPathLength = 47;

    private static readonly byte[] RetailPrefix = "cdrom0:\\\0"u8.ToArray();
    private static readonly byte[] HostPrefix = "host0:\\\0\0"u8.ToArray();
    private static readonly byte[] RetailArguments = "rb batch=Crash6\\Crash\0"u8.ToArray();

    public static UInt32 Crc(ReadOnlySpan<byte> file)
    {
        var crc = 0u;
        for (var i = 0; i + 4 <= file.Length; i += 4)
        {
            crc ^= BinaryPrimitives.ReadUInt32LittleEndian(file[i..]);
        }

        return crc;
    }

    /// <summary>
    /// The game's path of a chunk, like the archive has them (Levels\Earth\Hub\Beach), the loader upper cases it
    /// </summary>
    public static string ChunkPath(string additionalPath) => additionalPath.Replace('/', '\\');

    public static byte[] Patch(GameRelease release, ReadOnlySpan<byte> retail, string startChunk)
    {
        var path = Encoding.Latin1.GetBytes(startChunk);
        if (path.Length == 0 || path.Length > MaxChunkPathLength)
        {
            throw new ArgumentException($"The game only has room for chunk paths of up to {MaxChunkPathLength} characters, {startChunk} has {path.Length}");
        }

        var patched = new byte[retail.Length + 4];
        retail.CopyTo(patched);
        Replace(release, patched, release.LoosePrefix, RetailPrefix, HostPrefix);
        Replace(release, patched, release.LaunchArguments, RetailArguments, [(byte)'r', (byte)'b', .. new byte[RetailArguments.Length - 2]]);
        Replace(release, patched, release.StartChunkGap, new byte[MaxChunkPathLength + 1], [.. path, .. new byte[MaxChunkPathLength + 1 - path.Length]]);
        // lui a1, %hi(x) and addiu a1, a1, %lo(x): the Beach literal, then the gap
        ReplaceWord(release, patched, release.StartChunkLoad, 0x3c050000 | High(release.BeachLiteral), 0x3c050000 | High(release.StartChunkGap));
        ReplaceWord(release, patched, release.StartChunkLoad + 8, 0x24a50000 | (release.BeachLiteral & 0xffff), 0x24a50000 | (release.StartChunkGap & 0xffff));
        var balance = Crc(patched.AsSpan(0, retail.Length)) ^ release.PatchedCrc;
        BinaryPrimitives.WriteUInt32LittleEndian(patched.AsSpan(retail.Length), balance);
        return patched;
    }

    // addiu sign extends its half, so the upper half of an address whose lower one is 0x8000 or more is one more
    private static UInt32 High(UInt32 address) => (address + 0x8000) >> 16;

    private static void Replace(GameRelease release, byte[] file, UInt32 address, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> replacement)
    {
        var offset = FileOffset(file, address);
        if (!file.AsSpan(offset, expected.Length).SequenceEqual(expected))
        {
            throw new InvalidDataException($"The executable isn't the {release.Name} release's, it has other bytes at {address:X}");
        }

        replacement.CopyTo(file.AsSpan(offset));
    }

    private static void ReplaceWord(GameRelease release, byte[] file, UInt32 address, UInt32 expected, UInt32 replacement)
    {
        Span<byte> expectedBytes = stackalloc byte[4];
        Span<byte> replacementBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(expectedBytes, expected);
        BinaryPrimitives.WriteUInt32LittleEndian(replacementBytes, replacement);
        Replace(release, file, address, expectedBytes, replacementBytes);
    }

    // Where the loaded segment holding the address is in the file, through the ELF's program headers
    internal static Int32 FileOffset(ReadOnlySpan<byte> elf, UInt32 address)
    {
        if (elf.Length < 0x34 || !elf[..4].SequenceEqual("\u007fELF"u8))
        {
            throw new InvalidDataException("The executable isn't an ELF file");
        }

        var headersOffset = BinaryPrimitives.ReadUInt32LittleEndian(elf[0x1c..]);
        var headerSize = BinaryPrimitives.ReadUInt16LittleEndian(elf[0x2a..]);
        var headerCount = BinaryPrimitives.ReadUInt16LittleEndian(elf[0x2c..]);
        for (var i = 0; i < headerCount; i++)
        {
            var header = elf[(Int32)(headersOffset + i * headerSize)..];
            var type = BinaryPrimitives.ReadUInt32LittleEndian(header);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
            var fileSize = BinaryPrimitives.ReadUInt32LittleEndian(header[16..]);
            if (type == 1 && address >= virtualAddress && address < virtualAddress + fileSize)
            {
                return (Int32)(offset + address - virtualAddress);
            }
        }

        throw new InvalidDataException($"The executable has nothing loaded at {address:X}");
    }
}
