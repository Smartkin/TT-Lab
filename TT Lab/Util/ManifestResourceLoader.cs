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
        var fullPath = GetPathInExe(textFileName);
        Debug.Assert(File.Exists(fullPath), $"Attempting to load file that doesn't exist {fullPath}");
        return File.ReadAllText(fullPath);
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

    public static string[] GetFiledInExeDirectory(string directory)
    {
        return Directory.GetFiles(GetPathInExe(directory));
    }
}