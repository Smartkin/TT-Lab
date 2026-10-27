using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Splat;
using TT_Lab.Project.Migration;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.Project.Repair;

/// <summary>
/// What the user says to the repair offered as a project opens
/// </summary>
public enum RepairAnswer
{
    Repair,
    // Asked again the next time the project opens
    NotNow,
    // Written into the project's marker, the Project menu still repairs it
    DontAskAgain
}

/// <summary>
/// The repair of what TT Lab 1.1.0's unpacking of the game's discs (and 1.0.0's, whose projects got migrated) wrote wrong into a project's game
/// packages, which only the disc can say: A1 objects running another chunk's graph of a reused ID or none, and graphs without their starter's
/// own ID; A5 chunks drawing another chunk's picture of a reused texture ID; C7 chunks' own values linking variants the merge deleted. The
/// game's packages of the versions repaired are unpacked again into a folder in the project's (the shadow), reading the disc where it is,
/// and the project's files take the shadow's values field by field while they're still what 1.1.0 made: what the user changed stays and is
/// noted. Every file it changes is kept as it was in a backup in the project's folder, and the project stays a 1.1.0 project
/// </summary>
public static class RetailRepair
{
    // The folder of a version's game files, and whether the user said it's the original disc: a PS2 archive TT Lab doesn't know is taken then
    public sealed record Disc(string Folder, bool ConfirmedOriginal = false);

    // The versions whose packages took the disc's values and the ones without a disc the repair takes (offered again the next time the project
    // opens), how many files changed or got added, the archive of the files as they were (none when nothing changed) and what the repair left as
    // it was or couldn't do, which the log has too
    public sealed record Result(IReadOnlyList<GamePlatform> Repaired, IReadOnlyList<GamePlatform> Skipped, int Changed, string? Backup, IReadOnlyList<string> Notes);

    // The versions of the game whose packages the project has unpacked: its version's package has assets
    public static IReadOnlyList<GamePlatform> UnpackedVersions(string projectFile)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        var name = Project.ReadProjectFile(projectFile).Name;
        return new[] { GamePlatform.PS2, GamePlatform.Xbox }.Where(platform =>
        {
            var package = Path.Combine(folder, "assets", RetailAssets.PackageName(platform, name));
            return Directory.Exists(package) && Directory.EnumerateFiles(package, "*.json", SearchOption.AllDirectories).Skip(1).Any();
        }).ToList();
    }

    // The versions the repair is offered for when the project opens: unpacked, and neither repaired, declined nor made without 1.1.0's faults
    public static IReadOnlyList<GamePlatform> VersionsToOffer(string projectFile)
    {
        var marker = RepairMarker.Read(Path.GetDirectoryName(Path.GetFullPath(projectFile))!);
        return UnpackedVersions(projectFile).Where(platform => !marker.IsSettled(platform)).ToList();
    }

    // Don't Ask Again: the versions are offered no more, the Project menu still repairs them
    public static void Decline(string projectFile, IEnumerable<GamePlatform> versions)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        var marker = RepairMarker.Read(folder);
        foreach (var platform in versions)
        {
            marker.For(platform).Repair = RepairState.Declined;
        }

        marker.Write(folder);
    }

    // Repairs the versions of the project's game packages whose disc is given, a PS2 one only when its archive is a release's or the user
    // confirmed it's the original disc. Without a disc a version is skipped and noted, its chunks' values linking what the project doesn't have
    // noted for the user to put back, and it's offered again the next time the project opens. A second run changes nothing
    public static Result Repair(string projectFile, IReadOnlyCollection<GamePlatform> versions, IReadOnlyDictionary<GamePlatform, Disc> discs)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        var stored = Project.ReadProjectFile(projectFile);
        var marker = RepairMarker.Read(folder);
        var time = DateTime.Now;
        var backup = Unused(Path.Combine(folder, $"repair_backup_{time:yyyy-MM-dd_HH-mm-ss}"), ".zip");
        var scratch = Unused(Path.Combine(folder, $"repair_scratch_{time:yyyy-MM-dd_HH-mm-ss}"), string.Empty);
        var notes = new List<string>();
        void Note(string note)
        {
            Log.WriteLine(note, Log.LogType.Warning);
            notes.Add(note);
        }

        var found = new Dictionary<GamePlatform, string>();
        foreach (var platform in versions.Distinct().Order())
        {
            var name = RetailAssets.Describe(platform);
            if (!discs.TryGetValue(platform, out var disc))
            {
                Note($"The {name} version wasn't repaired, no disc of it was given. It's offered again the next time the project opens, Project > Repair from the Game's " +
                     "Discs runs it whenever the disc is at hand");
                continue;
            }

            var check = RepairDiscs.Of(platform, disc.Folder);
            if (!check.Takes(disc.ConfirmedOriginal))
            {
                Note($"The {name} version wasn't repaired from {disc.Folder}: {check.Text}");
                continue;
            }

            found[platform] = disc.Folder;
        }

        var manager = Locator.Current.GetService<ProjectManager>()!;
        var start = DateTime.Now;
        var changed = 0;
        string? written = null;
        try
        {
            RepairFiles? shadow = null;
            if (found.Count > 0)
            {
                Log.WriteLine($"Unpacking the {string.Join(" and ", found.Keys.Select(RetailAssets.Describe))} game packages again for the repair into {scratch}...");
                manager.UnpackShadow(stored, found, scratch, !marker.HasNewXboxFileOrder);
                shadow = new RepairFiles(Path.Combine(scratch, "assets"));
            }

            Log.WriteLine("Reading the project's files...");
            var project = new RepairFiles(Path.Combine(folder, "assets"));
            using (var context = new MigrationContext(folder, backup))
            {
                foreach (var platform in versions.Distinct().Order())
                {
                    var version = new RepairVersion(platform, stored.Name);
                    if (shadow != null && found.ContainsKey(platform))
                    {
                        Log.WriteLine($"Repairing the {version.Describe()} version's behaviours...");
                        new GraphRepair(shadow, project, version, context).Run();
                        Log.WriteLine($"Repairing the {version.Describe()} version's chunks' pictures...");
                        new TextureRepair(shadow, project, version, context, new RepairFingerprints(project, shadow)).Run();
                    }

                    new OverrideLinkRepair(found.ContainsKey(platform) ? shadow : null, project, version, context).Run();
                }

                try
                {
                    project.Write(context);
                }
                catch (Exception)
                {
                    context.Dispose();
                    PutBack(folder, backup, context.Changed);
                    throw;
                }

                changed = context.Changed.Count;
                notes.AddRange(context.Notes);
            }

            written = File.Exists(backup) ? backup : null;
        }
        finally
        {
            DeleteScratch(scratch);
        }

        var result = new Result(found.Keys.Order().ToList(), versions.Distinct().Where(platform => !found.ContainsKey(platform)).Order().ToList(), changed, written, notes);
        foreach (var platform in result.Repaired)
        {
            var entry = marker.For(platform);
            entry.Repair = RepairState.Repaired;
            entry.RepairedAt = time;
            entry.Result = $"{changed} files changed, {notes.Count} notes";
        }

        if (result.Repaired.Count > 0)
        {
            marker.Write(folder);
            Log.WriteLine($"Repaired the {string.Join(" and ", result.Repaired.Select(RetailAssets.Describe))} game packages in {DateTime.Now - start}, {changed} files changed" +
                          (written != null ? $", kept as they were in {Path.GetFileName(written)}" : string.Empty));
        }

        return result;
    }

    // A repair that couldn't write all of its changes leaves the files as they were: the backup's back in their place, the files it added gone
    private static void PutBack(string folder, string backup, IReadOnlyCollection<string> changed)
    {
        var restored = new HashSet<string>(StringComparer.Ordinal);
        if (File.Exists(backup))
        {
            using var archive = ZipFile.OpenRead(backup);
            foreach (var entry in archive.Entries)
            {
                var path = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                entry.ExtractToFile(path, true);
                restored.Add(path);
            }
        }

        foreach (var path in changed.Where(path => !restored.Contains(path) && File.Exists(path)))
        {
            File.Delete(path);
        }
    }

    // Two repairs within a second don't share a backup or a shadow
    private static string Unused(string path, string extension)
    {
        var unused = path + extension;
        for (var number = 2; File.Exists(unused) || Directory.Exists(unused); number++)
        {
            unused = $"{path}_{number}{extension}";
        }

        return unused;
    }

    private static void DeleteScratch(string scratch)
    {
        try
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"The repair's unpacked game packages couldn't be deleted, delete {scratch}: {exception.Message}", Log.LogType.Warning);
        }
    }
}
