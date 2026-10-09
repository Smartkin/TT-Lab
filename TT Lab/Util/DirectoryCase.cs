using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;

namespace TT_Lab.Util;

// Directories as the file system names them, whatever case they're asked for in. Windows doesn't tell names apart by case and Linux
// does. Assets find their files by their URIs, and the global packages' URIs name the startup folder in two cases: Startup for the
// startup folder's files (the disc's case) and startup for the default chunk and its assets (the chunk's path in the game, lower case
// like every chunk's). A project made on Windows had one folder for both, named in the case of whichever got written first, and on Linux
// the files of the other weren't found. A directory that isn't there takes the name of the one there named alike, the exact name first:
// projects made on Linux before have a folder of each name
internal static class DirectoryCase
{
    // By the path asked for, so the file system is asked about each directory once. Only directories found are kept: one that isn't
    // there can still be made, in either case
    private static readonly ConcurrentDictionary<string, string> Found = new(StringComparer.Ordinal);
    // A directory is looked up and made at once, one made meanwhile in another case is found: assets written in parallel made a folder of
    // each name, and an asset's data could go into the one and its metadata into the other
    private static readonly object Making = new();

    public static string Resolve(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath);
        var resolved = Resolve(path);
        return resolved == path ? relativePath : Path.GetRelativePath(root, resolved);
    }

    public static string Resolve(string directory)
    {
        if (Found.TryGetValue(directory, out var found))
        {
            // Another name than the one asked for stays while its directory does
            if (found == directory || Directory.Exists(found))
            {
                return found;
            }

            Found.TryRemove(directory, out _);
        }

        if (Directory.Exists(directory))
        {
            Found[directory] = directory;
            return directory;
        }

        var parent = Path.GetDirectoryName(directory);
        var name = Path.GetFileName(directory);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            return directory;
        }

        var parentFound = Resolve(parent);
        var path = Path.Combine(parentFound, name);
        if (!Directory.Exists(parentFound))
        {
            return path;
        }

        var existing = parentFound != parent && Directory.Exists(path)
            ? path
            : Directory.EnumerateDirectories(parentFound).Where(child => Path.GetFileName(child).Equals(name, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal).FirstOrDefault();
        if (existing == null)
        {
            return path;
        }

        Found[directory] = existing;
        return existing;
    }

    // Makes the directory unless the file system has it in some case, the path it's at either way
    public static string Create(string directory)
    {
        var path = Resolve(directory);
        if (Directory.Exists(path))
        {
            return path;
        }

        lock (Making)
        {
            path = Resolve(directory);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    // For another project, or the same one opened again after its folders changed
    public static void Forget() => Found.Clear();
}
