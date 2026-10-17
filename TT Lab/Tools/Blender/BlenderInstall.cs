using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// The Blender TT Lab opens models in and installs the add-on into: the one the preferences name, else one on the path, the Flatpak,
/// Steam's (in any of its libraries) or one of Program Files. The add-on needs Blender 4.5 or newer
/// </summary>
public sealed class BlenderInstall
{
    public const string FlatpakId = "org.blender.Blender";
    public static readonly Version Oldest = new(4, 5);

    /// <summary>
    /// Where TT Lab looks for Blender, tests give folders of their own
    /// </summary>
    internal sealed record Places(string Home, string PathVariable, IReadOnlyList<string> ProgramFolders, bool IsWindows, bool IsLinux)
    {
        public static Places OfThisSystem()
        {
            var programs = new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
                .Select(Environment.GetFolderPath).Where(folder => !string.IsNullOrEmpty(folder)).ToList();
            return new Places(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.GetEnvironmentVariable("PATH") ?? string.Empty, programs,
                OperatingSystem.IsWindows(), OperatingSystem.IsLinux());
        }
    }

    private BlenderInstall(string? executable)
    {
        Executable = executable;
    }

    /// <summary>
    /// Its executable, none for the Flatpak
    /// </summary>
    public string? Executable { get; }

    public bool IsFlatpak => Executable == null;

    public override string ToString() => Executable ?? $"the Flatpak {FlatpakId}";

    public static BlenderInstall? Find(string configured) => Find(configured, Places.OfThisSystem());

    internal static BlenderInstall? Find(string configured, Places places)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (configured == FlatpakId)
            {
                return places.IsLinux ? new BlenderInstall(null) : null;
            }

            return File.Exists(configured) ? new BlenderInstall(configured) : null;
        }

        var name = places.IsWindows ? "blender.exe" : "blender";
        var onPath = places.PathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(folder => Path.Combine(folder, name)).FirstOrDefault(File.Exists);
        if (onPath != null)
        {
            return new BlenderInstall(onPath);
        }

        if (places.IsLinux && (Directory.Exists(Path.Combine(places.Home, ".local/share/flatpak/app", FlatpakId)) || Directory.Exists(Path.Combine("/var/lib/flatpak/app", FlatpakId))))
        {
            return new BlenderInstall(null);
        }

        var steam = SteamLibraries(places).Select(library => Path.Combine(library, "steamapps", "common", "Blender", name)).FirstOrDefault(File.Exists);
        if (steam != null)
        {
            return new BlenderInstall(steam);
        }

        // Blender's installer puts every version in a folder of its own, the newest last by name
        var installed = places.ProgramFolders.Select(folder => Path.Combine(folder, "Blender Foundation")).Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateDirectories(folder, "Blender*")).OrderBy(folder => VersionOfFolder(folder) ?? new Version(0, 0))
            .Select(folder => Path.Combine(folder, name)).LastOrDefault(File.Exists);
        return installed == null ? null : new BlenderInstall(installed);
    }

    private static Version? VersionOfFolder(string folder)
    {
        var match = Regex.Match(Path.GetFileName(folder), @"(\d+)\.(\d+)");
        return match.Success ? new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)) : null;
    }

    // Steam keeps its libraries in libraryfolders.vdf, its own folder the first of them
    internal static IEnumerable<string> SteamLibraries(Places places)
    {
        var steamFolders = places.IsWindows
            ? places.ProgramFolders.Select(folder => Path.Combine(folder, "Steam"))
            : new[] { Path.Combine(places.Home, ".local/share/Steam"), Path.Combine(places.Home, ".steam/steam") };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var steam in steamFolders)
        {
            if (Directory.Exists(steam) && seen.Add(Path.GetFullPath(steam)))
            {
                yield return steam;
            }

            var listing = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(listing))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(listing);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\""))
            {
                var library = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(library) && seen.Add(Path.GetFullPath(library)))
                {
                    yield return library;
                }
            }
        }
    }

    public ProcessStartInfo StartInfo(params string[] arguments)
    {
        var info = new ProcessStartInfo { UseShellExecute = false };
        if (IsFlatpak)
        {
            info.FileName = "flatpak";
            info.ArgumentList.Add("run");
            info.ArgumentList.Add(FlatpakId);
        }
        else
        {
            info.FileName = Executable!;
        }

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    /// <summary>
    /// The version its first line of --version says ("Blender 5.2.2 LTS")
    /// </summary>
    public static Version? VersionOf(string output)
    {
        var match = Regex.Match(output, @"Blender (\d+)\.(\d+)(?:\.(\d+))?");
        return match.Success ? new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0) : null;
    }

    public async Task<Version?> ReadVersionAsync()
    {
        var info = StartInfo("--version");
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        try
        {
            using var process = Process.Start(info);
            if (process == null)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return VersionOf(output);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            Log.WriteLine($"Couldn't start Blender ({this}): {exception.Message}", Log.LogType.Warning);
            return null;
        }
    }
}
