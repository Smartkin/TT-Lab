using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;

namespace TT_Lab.Project;

/// <summary>
/// How every asset's data file was when TT Lab last read or wrote it, its length and the time it was written, so the project's watcher tells
/// what TT Lab saved itself from what another program changed (a text editor saving a script, Blender exporting a model over its file)
/// </summary>
internal static class AssetFileStamps
{
    // File systems keep write times to their clock's tick (Windows' timer, FAT's 2 seconds): another program writing as many bytes right
    // after TT Lab got the same length and time, so a file written this recently is told apart by its content as well
    private static readonly TimeSpan SameTickWindow = TimeSpan.FromSeconds(2);

    public static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly ConcurrentDictionary<string, Stamp> Stamps = new(PathComparer);

    private readonly record struct Stamp(long Length, DateTime WriteTime, ulong? Content);

    public static string Normalize(string path) => Path.GetFullPath(path);

    public static void Record(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return;
            }

            var writeTime = file.LastWriteTimeUtc;
            Stamps[Normalize(path)] = new Stamp(file.Length, writeTime, DateTime.UtcNow - writeTime < SameTickWindow ? Hash(path) : null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Whether the file is the one TT Lab last read or wrote, a file it never did is someone else's
    /// </summary>
    public static bool IsAsRecorded(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || !Stamps.TryGetValue(Normalize(path), out var stamp) || stamp.Length != file.Length || stamp.WriteTime != file.LastWriteTimeUtc)
            {
                return false;
            }

            return stamp.Content is not { } content || Hash(path) == content;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static ulong Hash(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(stream));
    }

    public static void Clear() => Stamps.Clear();
}
