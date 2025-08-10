using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace TT_Lab.Util;

/// <summary>
/// Helper class to search for files in exe's directory
/// </summary>
public static class ManifestResourceLoader
{
    public static string LoadTextFile(string textFileName)
    {
        Debug.Assert(File.Exists(textFileName), $"Attempting to load file that doesn't exist {textFileName}");
        return File.ReadAllText(GetPathInExe(textFileName));
    }

    public static string GetPathInExe(string pathToFile)
    {
        var assemblyLocation = AppContext.BaseDirectory;
        UriBuilder uri = new(assemblyLocation);
        var path = Uri.UnescapeDataString(uri.Path);

        return Path.Combine(Path.GetDirectoryName(path)!, pathToFile);
    }
}