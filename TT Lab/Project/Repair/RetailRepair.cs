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
    // A repair that failed, its message whether the project's files are as they were
    public sealed class RepairFailedException(string message, Exception inner) : Exception(message, inner);

    // The folder of a version's game files, and whether the user said it's the original disc of the game the project was made from: a PS2 archive
    // TT Lab doesn't know and files of another release than the project's disc folder's are taken then
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

    // Repairs the versions of the project's game packages whose disc is given: a PS2 one only when its archive is a release's and an Xbox one
    // when it has the disc's files, either only of the release the project was made from unless the user confirmed it's the original disc of
    // the project's game. Without a disc a version is skipped and noted, its chunks' values linking what the project doesn't have noted for the
    // user to put back. A second run changes nothing. A failure throws a RepairFailedException saying whether the project's files are as they
    // were. The known archives are RepairDiscs.KnownPs2Archives unless tests give others
    public static Result Repair(string projectFile, IReadOnlyCollection<GamePlatform> versions, IReadOnlyDictionary<GamePlatform, Disc> discs,
        IReadOnlyList<RepairDiscs.Release>? knownArchives = null)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        var stored = Project.ReadProjectFile(projectFile);
        if (stored.Version != Project.CURRENT_VERSION)
        {
            throw new ProjectException($"Only projects of TT Lab {Project.CURRENT_VERSION} get repaired, {stored.Name} is of {stored.Version}");
        }

        DeleteLeftovers(folder);
        var marker = RepairMarker.Read(folder);
        var time = DateTime.Now;
        var backup = Unused(Path.Combine(folder, $"repair_backup_{time:yyyy-MM-dd_HH-mm-ss}"), ".zip");
        var scratch = Unused(Path.Combine(folder, $"{ScratchPrefix}{time:yyyy-MM-dd_HH-mm-ss}"), string.Empty);
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
            // A version repaired, declined or made without 1.1.0's faults isn't offered when the project opens
            var later = marker.IsSettled(platform)
                ? "Project > Repair from the Game's Discs runs it whenever the disc is at hand"
                : "It's offered again the next time the project opens, Project > Repair from the Game's Discs runs it whenever the disc is at hand";
            if (!discs.TryGetValue(platform, out var disc))
            {
                Note($"The {name} version wasn't repaired, no disc of it was given. {later}");
                continue;
            }

            var check = RepairDiscs.Of(platform, disc.Folder, knownArchives);
            var release = check.Verdict == RepairDiscs.Verdict.Missing ? null : RepairDiscs.ProjectsRelease(platform, disc.Folder,
                platform == GamePlatform.Xbox ? stored.DiscContentPathXbox : stored.DiscContentPathPS2);
            if (release == null || (!disc.ConfirmedOriginal && (check.Verdict != RepairDiscs.Verdict.Known || release.Same != true)))
            {
                var why = release == null || check.Verdict != RepairDiscs.Verdict.Known ? check.Text : release.Text;
                Note($"The {name} version wasn't repaired from {disc.Folder}: {why} {later}");
                continue;
            }

            found[platform] = disc.Folder;
        }

        var manager = Locator.Current.GetService<ProjectManager>()!;
        var start = DateTime.Now;
        var context = new MigrationContext(folder, backup);
        try
        {
            List<(string Path, byte[] Content)> changes;
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

                changes = project.Changes();
            }
            catch (Exception exception)
            {
                throw new RepairFailedException($"Nothing of the project was changed: {exception.Message}", exception);
            }
            finally
            {
                // The disc's packages unpacked again go before anything is written, they take most of the room the backup and the files need
                DeleteScratch(scratch);
            }

            WriteChanges(context, folder, backup, changes);
            notes.AddRange(context.Notes);
        }
        finally
        {
            DisposeQuietly(context);
        }

        var written = File.Exists(backup) ? backup : null;
        var result = new Result(found.Keys.Order().ToList(), versions.Distinct().Where(platform => !found.ContainsKey(platform)).Order().ToList(), context.Changed.Count,
            written, notes);
        if (result.Repaired.Count == 0)
        {
            return result;
        }

        foreach (var platform in result.Repaired)
        {
            var entry = marker.For(platform);
            entry.Repair = RepairState.Repaired;
            entry.RepairedAt = time;
            entry.Result = $"{result.Changed} files changed, {notes.Count} notes";
        }

        try
        {
            marker.Write(folder);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The repair is offered again then, which changes nothing
            Log.WriteLine($"{RepairMarker.FileName} couldn't be written, the repair is offered again the next time the project opens: {exception.Message}", Log.LogType.Warning);
        }

        Log.WriteLine($"Repaired the {string.Join(" and ", result.Repaired.Select(RetailAssets.Describe))} game packages in {DateTime.Now - start}, {result.Changed} files " +
                      "changed" + (written != null ? $", kept as they were in {Path.GetFileName(written)}" : string.Empty));
        return result;
    }

    // Every file the repair changes is backed up into the archive, closed and whole, before any of them is written: a write failing puts back
    // what the archive has and deletes the files the repair made, and the exception says whether that worked
    internal static void WriteChanges(MigrationContext context, string folder, string backup, List<(string Path, byte[] Content)> changes)
    {
        var made = changes.Select(change => Path.GetFullPath(change.Path)).Where(path => !File.Exists(path)).ToHashSet(AssetFileStamps.PathComparer);
        try
        {
            context.BackUpAll(changes.Select(change => change.Path));
        }
        catch (Exception exception)
        {
            DisposeQuietly(context);
            DeleteQuietly(backup);
            throw new RepairFailedException($"The files it changes couldn't be backed up, nothing of the project was changed: {exception.Message}", exception);
        }

        try
        {
            foreach (var (path, content) in changes)
            {
                context.WriteAllBytes(path, content);
            }
        }
        catch (Exception exception)
        {
            try
            {
                PutBack(folder, backup, made);
            }
            catch (Exception putBack)
            {
                var added = made.Count > 0 ? $", the files it added are {string.Join(", ", made.Select(path => Path.GetRelativePath(folder, path)))}" : string.Empty;
                throw new RepairFailedException($"Writing the project's files failed ({exception.Message}) and they couldn't all be put back ({putBack.Message}): the files it " +
                                                $"changed are as they were in {Path.GetFileName(backup)} in the project's folder{added}", exception);
            }

            throw new RepairFailedException($"Writing the project's files failed and they're as they were: {exception.Message}", exception);
        }
    }

    // The backup's files back in their place, the files the repair made gone
    private static void PutBack(string folder, string backup, IReadOnlySet<string> made)
    {
        if (File.Exists(backup))
        {
            using var archive = ZipFile.OpenRead(backup);
            foreach (var entry in archive.Entries)
            {
                entry.ExtractToFile(Path.GetFullPath(Path.Combine(folder, entry.FullName)), true);
            }
        }

        foreach (var path in made.Where(File.Exists))
        {
            File.Delete(path);
        }
    }

    private static void DisposeQuietly(MigrationContext context)
    {
        try
        {
            context.Dispose();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An archive that can't be closed is deleted with the rest of a failed repair, a closed one was closed before any file changed
        }
    }

    private static void DeleteQuietly(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Log.WriteLine($"The repair's unfinished backup {file} couldn't be deleted: {exception.Message}", Log.LogType.Warning);
        }
    }

    private const string ScratchPrefix = "repair_scratch_";

    // A shadow a TT Lab that was ended or that couldn't delete it left behind
    private static void DeleteLeftovers(string folder)
    {
        foreach (var leftover in Directory.GetDirectories(folder, ScratchPrefix + "*"))
        {
            DeleteScratch(leftover);
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
