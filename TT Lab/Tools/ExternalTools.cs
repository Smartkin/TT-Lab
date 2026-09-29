using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Assets;

namespace TT_Lab.Tools;

/// <summary>
/// The twinstudio tools shipped next to TT Lab, each in its own folder under tools: twinmusic for the MH/MB music archives and twinpss
/// for the PSS videos. They take no file on their command line, so the file's path is handed over and put on the clipboard as well
/// </summary>
public static class ExternalTools
{
    public const string FolderName = "tools";
    public const string ReleasesUrl = "https://github.com/Smartkin/twinstudio/releases";

    // Tests point it at a folder of their own
    internal static string ToolsFolder { get; set; } = Path.Combine(AppContext.BaseDirectory, FolderName);

    public static string ToolName(DiscFileTool tool) => tool == DiscFileTool.Music ? "twinmusic" : "twinpss";

    public static string ToolFolder(DiscFileTool tool) => Path.Combine(ToolsFolder, ToolName(tool));

    public static string? FindExecutable(DiscFileTool tool)
    {
        var name = ToolName(tool);
        var folder = ToolFolder(tool);
        string[] candidates = OperatingSystem.IsWindows() ? [$"{name}.exe"] : [name, $"{name}.exe"];
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(folder, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string MissingMessage(DiscFileTool tool)
    {
        return $"{ToolName(tool)} isn't installed: Tools > Install the tools gets it from the twinstudio releases into {ToolFolder(tool)}";
    }

    private static int _installing;

    /// <summary>
    /// The release asset of a tool for the operating system, none for one twinstudio isn't built for
    /// </summary>
    public static string? ReleaseAssetName(DiscFileTool tool)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"{ToolName(tool)}-windows-latest.zip";
        }

        return OperatingSystem.IsLinux() ? $"{ToolName(tool)}-ubuntu-latest.zip" : null;
    }

    /// <summary>
    /// Downloads the latest release of both tools for this system and puts them into their folders, in place of what's there
    /// </summary>
    public static async Task<bool> InstallAsync(CancellationToken cancellation = default)
    {
        if (Interlocked.Exchange(ref _installing, 1) == 1)
        {
            Log.WriteLine("The tools are being installed already", Log.LogType.Warning);
            return false;
        }

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TT-Lab");
            var installed = true;
            foreach (var tool in new[] { DiscFileTool.Music, DiscFileTool.Video })
            {
                var asset = ReleaseAssetName(tool);
                if (asset == null)
                {
                    Log.WriteLine($"twinstudio has no {ToolName(tool)} build for this system, get one from {ReleasesUrl}", Log.LogType.Warning);
                    installed = false;
                    continue;
                }

                var url = $"{ReleasesUrl}/latest/download/{asset}";
                Log.WriteLine($"Downloading {url}...");
                try
                {
                    await using var download = await client.GetStreamAsync(url, cancellation);
                    var temporary = Path.GetTempFileName();
                    try
                    {
                        await using (var file = File.Create(temporary))
                        {
                            await download.CopyToAsync(file, cancellation);
                        }

                        await using var zip = File.OpenRead(temporary);
                        InstallFromZip(tool, zip);
                    }
                    finally
                    {
                        File.Delete(temporary);
                    }

                    Log.WriteLine($"Installed {ToolName(tool)} into {ToolFolder(tool)}");
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    Log.WriteLine($"Couldn't install {ToolName(tool)}: {ex.Message}", Log.LogType.Error);
                    installed = false;
                }
            }

            return installed;
        }
        finally
        {
            Interlocked.Exchange(ref _installing, 0);
        }
    }

    /// <summary>
    /// Unpacks a tool's release zip into its folder, which gets emptied first, and makes its executable runnable
    /// </summary>
    public static void InstallFromZip(DiscFileTool tool, Stream zip)
    {
        var folder = ToolFolder(tool);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }

        Directory.CreateDirectory(folder);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
        archive.ExtractToDirectory(folder, overwriteFiles: true);
        var executable = FindExecutable(tool);
        if (executable == null)
        {
            throw new InvalidDataException($"The zip has no {ToolName(tool)} executable");
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(executable, File.GetUnixFileMode(executable) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    /// <summary>
    /// Starts the tool in its own folder, with the file to open as its argument
    /// </summary>
    public static bool Launch(DiscFileTool tool, string? filePath = null)
    {
        var executable = FindExecutable(tool);
        if (executable == null)
        {
            Log.WriteLine(MissingMessage(tool), Log.LogType.Warning);
            return false;
        }

        try
        {
            // Zips extracted by other tools lose the executable bit
            if (!OperatingSystem.IsWindows() && !File.GetUnixFileMode(executable).HasFlag(UnixFileMode.UserExecute))
            {
                File.SetUnixFileMode(executable, File.GetUnixFileMode(executable) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
            }

            var info = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable), UseShellExecute = false };
            if (filePath != null)
            {
                info.ArgumentList.Add(filePath);
            }

            Process.Start(info);
            Log.WriteLine(filePath == null ? $"Started {ToolName(tool)}" : $"Started {ToolName(tool)} for {filePath}, the path is on the clipboard");
            return true;
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Couldn't start {ToolName(tool)}: {ex.Message}", Log.LogType.Error);
            return false;
        }
    }

    public static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Couldn't open {url}: {ex.Message}", Log.LogType.Warning);
        }
    }

    public static void ShowInFileManager(string path)
    {
        try
        {
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path)!;
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.WriteLine($"Couldn't open {path}: {ex.Message}", Log.LogType.Warning);
        }
    }
}
