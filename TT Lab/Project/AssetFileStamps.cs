using System;
using System.Collections.Concurrent;
using System.IO;

namespace TT_Lab.Project;

/// <summary>
/// How every asset's data file was when TT Lab last read or wrote it, its length and the time it was written, so the project's watcher tells
/// what TT Lab saved itself from what another program changed (a text editor saving a script, Blender exporting a model over its file)
/// </summary>
internal static class AssetFileStamps
{
    public static readonly StringComparer PathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly ConcurrentDictionary<string, (long Length, DateTime WriteTime)> Stamps = new(PathComparer);

    public static string Normalize(string path) => Path.GetFullPath(path);

    public static void Record(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (file.Exists)
            {
                Stamps[Normalize(path)] = (file.Length, file.LastWriteTimeUtc);
            }
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
            return file.Exists && Stamps.TryGetValue(Normalize(path), out var stamp) && stamp == (file.Length, file.LastWriteTimeUtc);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void Clear() => Stamps.Clear();
}
