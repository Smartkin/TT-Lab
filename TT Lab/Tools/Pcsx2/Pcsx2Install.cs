using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace TT_Lab.Tools.Pcsx2;

/// <summary>
/// How PCSX2 gets started and where it keeps its settings: its Flatpak, or an executable (an AppImage or install on Linux, Windows).
/// The game settings of the patched executable's CRC turn the host file system and PINE on only for it, and TT Lab talks to PINE on
/// a slot of its own, out of the way of a PCSX2 the user runs with it on
/// </summary>
public sealed class Pcsx2Install
{
    public const string FlatpakId = "net.pcsx2.PCSX2";
    public const int PineSlot = 28111;
    private const int DefaultPineSlot = 28011;

    private Pcsx2Install(string? executable, string settingsFolder)
    {
        Executable = executable;
        SettingsFolder = settingsFolder;
    }

    /// <summary>
    /// None for the Flatpak
    /// </summary>
    public string? Executable { get; }

    public bool IsFlatpak => Executable == null;

    public string SettingsFolder { get; }

    // PCSX2 names an overridden executable's game settings after its CRC alone
    public string GameSettingsPath(GameRelease release) => Path.Combine(SettingsFolder, "gamesettings", $"{release.PatchedCrc:X8}.ini");

    // PCSX2 loads the patches of a CRC from the files named after it, the lines outside a group are always on
    public string PatchesPath(GameRelease release) => Path.Combine(SettingsFolder, "patches", $"{release.PatchedCrc:X8}.pnach");

    /// <summary>
    /// The PCSX2 the preferences name, or the Flatpak, or pcsx2-qt on the path
    /// </summary>
    public static Pcsx2Install? Find(string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (configured == FlatpakId)
            {
                return OperatingSystem.IsLinux() ? Flatpak() : null;
            }

            return File.Exists(configured) ? new Pcsx2Install(configured, NativeSettingsFolder(configured)) : null;
        }

        if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (Directory.Exists(Path.Combine(home, ".local/share/flatpak/app", FlatpakId)) || Directory.Exists(Path.Combine("/var/lib/flatpak/app", FlatpakId)))
            {
                return Flatpak();
            }
        }

        var names = OperatingSystem.IsWindows() ? new[] { "pcsx2-qt.exe", "pcsx2.exe" } : ["pcsx2-qt", "pcsx2"];
        var found = SearchFolders().SelectMany(folder => names.Select(name => Path.Combine(folder, name))).FirstOrDefault(File.Exists);
        return found == null ? null : new Pcsx2Install(found, NativeSettingsFolder(found));
    }

    // PCSX2's installer doesn't put itself on the path
    private static IEnumerable<string> SearchFolders()
    {
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return folder;
        }

        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        foreach (var root in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
        {
            var folder = Environment.GetFolderPath(root);
            if (!string.IsNullOrEmpty(folder))
            {
                yield return Path.Combine(folder, "PCSX2");
                yield return Path.Combine(folder, "Programs", "PCSX2");
            }
        }
    }

    private static Pcsx2Install Flatpak()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new Pcsx2Install(null, Path.Combine(home, ".var/app", FlatpakId, "config/PCSX2"));
    }

    // A portable PCSX2 keeps its settings next to itself
    private static string NativeSettingsFolder(string executable)
    {
        var folder = Path.GetDirectoryName(executable)!;
        if (File.Exists(Path.Combine(folder, "portable.ini")) || File.Exists(Path.Combine(folder, "portable.txt")))
        {
            return folder;
        }

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PCSX2");
        }

        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return Path.Combine(string.IsNullOrEmpty(config) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config") : config, "PCSX2");
    }

    public void WriteGameSettings(GameRelease release)
    {
        var path = GameSettingsPath(release);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existing = File.Exists(path) ? File.ReadAllText(path) : "";
        var keys = new Dictionary<string, string>
        {
            ["HostFs"] = "true",
            ["EnablePINE"] = "true",
            ["PINESlot"] = PineSlot.ToString(),
            // A run is booted fresh every time, a state saved for resuming it would never be used
            ["SaveStateOnShutdown"] = "false",
        };
        if (release.LevelSelectPatch != null)
        {
            keys["EnablePatches"] = "true";
        }

        var updated = WithKeys(existing, "EmuCore", keys);
        // Closing the game's window ends the run without asking first (the game settings layer over PCSX2.ini, UI keys included)
        updated = WithKeys(updated, "UI", new Dictionary<string, string> { ["ConfirmShutdown"] = "false" });
        if (updated != existing)
        {
            File.WriteAllText(path, updated);
        }
    }

    /// <summary>
    /// The release's level select and save state patch, for the patched executable's CRC
    /// </summary>
    public void WritePatches(GameRelease release)
    {
        if (release.LevelSelectPatch == null)
        {
            return;
        }

        using var stream = typeof(Pcsx2Install).Assembly.GetManifestResourceStream(release.LevelSelectPatch)
            ?? throw new InvalidOperationException($"TT Lab has no {release.LevelSelectPatch}");
        using var reader = new StreamReader(stream);
        var patches = reader.ReadToEnd();
        var path = PatchesPath(release);
        if (File.Exists(path) && File.ReadAllText(path) == patches)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, patches);
    }

    // The keys TT Lab needs set in a section of the file, whatever else the user keeps there stays
    internal static string WithKeys(string ini, string sectionName, IReadOnlyDictionary<string, string> keys)
    {
        var lines = ini.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[^1] == "")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var section = lines.FindIndex(line => line.Trim() == $"[{sectionName}]");
        if (section == -1)
        {
            if (lines.Count > 0)
            {
                lines.Add("");
            }

            lines.Add($"[{sectionName}]");
            section = lines.Count - 1;
        }

        var end = lines.FindIndex(section + 1, line => line.TrimStart().StartsWith('['));
        if (end == -1)
        {
            end = lines.Count;
        }

        foreach (var (key, value) in keys)
        {
            var index = lines.FindIndex(section + 1, end - section - 1, line => line.Split('=')[0].Trim() == key);
            if (index != -1)
            {
                lines[index] = $"{key} = {value}";
                continue;
            }

            // After the section's last setting, before the blank lines leading to the next one
            var insertAt = end;
            while (insertAt > section + 1 && lines[insertAt - 1].Trim() == "")
            {
                insertAt--;
            }

            lines.Insert(insertAt, $"{key} = {value}");
            end++;
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    /// Boots the executable with the disc image in, in a window of its own that closes PCSX2 with it
    /// </summary>
    public ProcessStartInfo StartInfo(string executable, string discImage)
    {
        var info = new ProcessStartInfo { UseShellExecute = false };
        if (IsFlatpak)
        {
            info.FileName = "flatpak";
            info.ArgumentList.Add("run");
            info.ArgumentList.Add($"--filesystem={Path.GetDirectoryName(executable)}");
            info.ArgumentList.Add($"--filesystem={Path.GetDirectoryName(discImage)}:ro");
            info.ArgumentList.Add(FlatpakId);
        }
        else
        {
            info.FileName = Executable!;
        }

        foreach (var argument in new[] { "-nogui", "-fastboot", "-elf", executable, "--", discImage })
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }

    /// <summary>
    /// PINE's socket once PCSX2 opened it: a TCP port on Windows, a socket in the runtime folder on Linux, the Flatpak's reached through
    /// the root of its sandbox
    /// </summary>
    public PineClient ConnectPine(Process process)
    {
        if (OperatingSystem.IsWindows())
        {
            return PineClient.ConnectTcp(PineSlot);
        }

        var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        var socket = string.IsNullOrEmpty(runtime) ? "/tmp/pcsx2.sock" : $"{runtime}/pcsx2.sock";
        if (PineSlot != DefaultPineSlot)
        {
            socket += $".{PineSlot}";
        }

        if (!IsFlatpak)
        {
            return PineClient.ConnectUnix(socket);
        }

        var sandboxed = FindDescendant(process.Id, "pcsx2") ?? throw new IOException("PCSX2 hasn't started in its sandbox yet");
        return PineClient.ConnectUnix($"/proc/{sandboxed}/root{socket}");
    }

    private static int? FindDescendant(int ancestor, string namePrefix)
    {
        foreach (var directory in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(directory), out var pid))
            {
                continue;
            }

            try
            {
                if (!File.ReadAllText(Path.Combine(directory, "comm")).StartsWith(namePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                for (var current = pid; current > 1; current = ParentOf(current))
                {
                    if (current == ancestor)
                    {
                        return pid;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
            {
                // Gone or not ours
            }
        }

        return null;
    }

    // The parent's the fourth field of /proc/<pid>/stat, after the name in parentheses (which may hold spaces)
    private static int ParentOf(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        return int.Parse(stat[(stat.LastIndexOf(')') + 2)..].Split(' ')[1]);
    }
}
