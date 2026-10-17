using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// Installs the add-on TT Lab comes with (BlenderTools/dist) into a Blender as an extension, through Blender's own command line, enabled.
/// TT Lab 1.0.0's add-on was a legacy add-on of the same module, whose operators would clash with the extension's: it's turned off first
/// (its files stay)
/// </summary>
public static class BlenderAddonInstaller
{
    // Turned off in the user's preferences, which Blender saves: the extension's operators have the legacy add-on's names
    private const string DisableLegacy = "import addon_utils, bpy\n" +
                                         "if 'twin_tech_tools' in bpy.context.preferences.addons:\n" +
                                         "    addon_utils.disable('twin_tech_tools', default_set=True)\n" +
                                         "    bpy.ops.wm.save_userpref()\n" +
                                         "    print('TTLAB: legacy add-on turned off')\n";

    /// <summary>
    /// The add-on next to TT Lab, the newest when there are several
    /// </summary>
    public static string? BundledZip(string? folder = null)
    {
        folder ??= Path.Combine(AppContext.BaseDirectory, "BlenderTools", "dist");
        try
        {
            return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "twin_tech_tools-*.zip").OrderBy(file => VersionOf(file) ?? new Version(0, 0)).LastOrDefault() : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // twin_tech_tools-1.0.0.zip
    internal static Version? VersionOf(string zip)
    {
        var name = Path.GetFileNameWithoutExtension(zip);
        return Version.TryParse(name["twin_tech_tools-".Length..], out var version) ? version : null;
    }

    /// <summary>
    /// Installs the add-on into the Blender, what went wrong when it didn't
    /// </summary>
    public static async Task<string?> InstallAsync(BlenderInstall blender, string zip)
    {
        var version = await blender.ReadVersionAsync();
        if (version == null)
        {
            return $"{blender} didn't say what version it is";
        }

        if (version < BlenderInstall.Oldest)
        {
            return $"{blender} is Blender {version}, the add-on needs Blender {BlenderInstall.Oldest} or newer";
        }

        var (legacyCode, legacyOutput) = await RunAsync(blender, "--background", "--python-exit-code", "1", "--python-expr", DisableLegacy);
        if (legacyCode != 0)
        {
            return $"Blender couldn't turn TT Lab 1.0.0's add-on off: {Last(legacyOutput)}";
        }

        if (legacyOutput.Contains("TTLAB: legacy add-on turned off"))
        {
            Log.WriteLine("Turned off TT Lab 1.0.0's Blender add-on, its files stay");
        }

        var (code, output) = await RunAsync(blender, "--command", "extension", "install-file", "--repo", "user_default", "--enable", zip);
        if (code != 0)
        {
            return $"Blender didn't install the add-on: {Last(output)}";
        }

        RememberInstalled(blender, VersionOf(zip));
        return null;
    }

    // The state folder's record of what TT Lab installed into which Blender, tests have their own
    internal static string Folder { get; set; } = LinkProtocol.StateFolder();

    private const string InstalledFile = "installed-addons.json";

    /// <summary>
    /// The add-on's version TT Lab installed into the Blender, none when it didn't
    /// </summary>
    public static Version? InstalledVersion(BlenderInstall blender)
    {
        try
        {
            var path = Path.Combine(Folder, InstalledFile);
            var recorded = File.Exists(path) ? System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))?[blender.ToString()]?.GetValue<string>() : null;
            return Version.TryParse(recorded, out var version) ? version : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static void RememberInstalled(BlenderInstall blender, Version? version)
    {
        try
        {
            var path = Path.Combine(Folder, InstalledFile);
            var all = File.Exists(path) && System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) is System.Text.Json.Nodes.JsonObject read ? read : new System.Text.Json.Nodes.JsonObject();
            all[blender.ToString()] = version?.ToString() ?? string.Empty;
            Directory.CreateDirectory(Folder);
            File.WriteAllText(path, all.ToJsonString());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Log.WriteLine($"Couldn't remember the add-on installed into {blender}: {exception.Message}", Log.LogType.Warning);
        }
    }

    /// <summary>
    /// Installs the add-on next to TT Lab into the Blender of the preferences (or the one found), saying how it went in the log
    /// </summary>
    /// <returns>What went wrong, none when it's installed</returns>
    public static async Task<string?> InstallIntoPreferredBlender()
    {
        var blender = BlenderInstall.Find(Preferences.GetPreference<string>(Preferences.BlenderPath));
        var zip = BundledZip();
        var problem = blender == null ? "No Blender found: pick its executable in the Preferences' Blender section"
            : zip == null ? "TT Lab has no add-on next to it (BlenderTools/dist)"
            : null;
        if (problem == null)
        {
            Log.WriteLine($"Installing the Blender add-on into {blender}...");
            problem = await InstallAsync(blender!, zip!);
        }

        Log.WriteLine(problem ?? $"The Blender add-on {VersionOf(zip!)} is installed into {blender}: Edit in Blender opens models with it", problem == null ? Log.LogType.Info : Log.LogType.Warning);
        return problem;
    }

    private static string Last(string output) => output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? "it said nothing";

    private static async Task<(int Code, string Output)> RunAsync(BlenderInstall blender, params string[] arguments)
    {
        var info = blender.StartInfo(arguments);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        try
        {
            using var process = Process.Start(info) ?? throw new InvalidOperationException("no process");
            var output = process.StandardOutput.ReadToEndAsync();
            var errors = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output + await errors);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return (-1, exception.Message);
        }
    }
}
