using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;

namespace TT_Lab.Util;

/// <summary>
/// Reads the files embedded in TT Lab (shaders, icons, pictures) by their path in the project, and finds the files kept next to its executable
/// </summary>
public static class ManifestResourceLoader
{
    // Named after their path in the project, with the separators of the OS that built TT Lab
    private static readonly Dictionary<string, string> EmbeddedFiles = typeof(ManifestResourceLoader).Assembly.GetManifestResourceNames()
        .ToDictionary(name => name.Replace('\\', '/'));

    public static Stream Open(string path)
    {
        var name = Normalize(path);
        if (!EmbeddedFiles.TryGetValue(name, out var resource))
        {
            throw new FileNotFoundException($"TT Lab has no embedded {name}", name);
        }

        return typeof(ManifestResourceLoader).Assembly.GetManifestResourceStream(resource)!;
    }

    public static string LoadTextFile(string path)
    {
        using var reader = new StreamReader(Open(path));
        return reader.ReadToEnd();
    }

    public static Bitmap LoadBitmap(string path)
    {
        using var stream = Open(path);
        return new Bitmap(stream);
    }

    public static IEnumerable<string> GetFilesIn(string directory)
    {
        var prefix = Normalize(directory) + '/';
        return EmbeddedFiles.Keys.Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.IndexOf('/', prefix.Length) < 0).Order();
    }

    public static string GetPathInExe(string pathToFile)
    {
        var assemblyLocation = AppContext.BaseDirectory;
#if _WINDOWS
        UriBuilder uri = new(assemblyLocation);
        var path = Uri.UnescapeDataString(uri.Path);

        return Path.Combine(Path.GetDirectoryName(path)!, pathToFile);
#else
        return Path.Combine(assemblyLocation, pathToFile);
#endif
    }

    // Shaders include each other by relative paths
    private static string Normalize(string path)
    {
        var parts = new List<string>();
        foreach (var part in path.Split('/', '\\'))
        {
            if (part is "" or ".")
            {
                continue;
            }

            if (part == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return string.Join('/', parts);
    }
}
