using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TT_Lab.AssetData.Graphics.TlModel;

namespace TT_Lab.Project.Migration;

/// <summary>
/// Brings a project an older TT Lab wrote to the version this one reads (<see cref="Project.CURRENT_VERSION"/>), a step per version,
/// keeping every file it changes in a backup archive in the project's folder first
/// </summary>
public static class ProjectMigration
{
    private static readonly Regex VersionValue = new("(\"Version\"\\s*:\\s*)\"[^\"]*\"");

    // Every version a released TT Lab wrote, with the step that takes its projects to the next one
    private static readonly IReadOnlyList<(string From, string To, Action<MigrationContext> Apply)> Steps =
    [
        ("1.0.0", "1.1.0", Version110.Apply),
    ];

    /// <summary>
    /// The version the project file says its project is, null when it says none
    /// </summary>
    public static string? ReadVersion(string projectFile)
    {
        try
        {
            return JsonNode.Parse(File.ReadAllText(projectFile))?["Version"]?.GetValue<string>();
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            throw new ProjectException($"Failed to read the project file: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Whether TT Lab can bring a project of the version to its own
    /// </summary>
    public static bool CanMigrate(string? version) => version != null && StepsFrom(version) != null;

    // The steps from the version to TT Lab's, null when there's no way there
    private static List<(string From, string To, Action<MigrationContext> Apply)>? StepsFrom(string version)
    {
        var result = new List<(string From, string To, Action<MigrationContext> Apply)>();
        while (version != Project.CURRENT_VERSION)
        {
            var step = Steps.FirstOrDefault(candidate => candidate.From == version);
            if (step.Apply == null)
            {
                return null;
            }

            result.Add(step);
            version = step.To;
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Migrates the project of the project file to TT Lab's version. The project file's version changes last, so a migration that
    /// stopped halfway runs again on what it left: every step leaves what it already did alone
    /// </summary>
    /// <returns>How many files changed, the archive holding them as they were (none when nothing changed) and what's left to the user</returns>
    public static MigrationResult Migrate(string projectFile)
    {
        var version = ReadVersion(projectFile) ?? throw new ProjectException($"The project file has no version, TT Lab can't bring it to {Project.CURRENT_VERSION}");
        var steps = StepsFrom(version) ?? throw new ProjectException($"TT Lab can't bring a project of version {version} to {Project.CURRENT_VERSION}");
        var folder = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        var backup = Path.Combine(folder, $"migration_backup_{version}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.zip");
        var changed = 0;
        List<string> notes;
        using (var context = new MigrationContext(folder, backup))
        {
            foreach (var (from, to, apply) in steps)
            {
                Log.WriteLine($"Migrating the project from {from} to {to}...");
                apply(context);
            }

            // Only the version changes, the rest of the file stays as it was written
            var text = File.ReadAllText(projectFile);
            context.WriteAllText(projectFile, VersionValue.Replace(text, match => $"{match.Groups[1].Value}\"{Project.CURRENT_VERSION}\"", 1));
            changed = context.Changed.Count;
            notes = context.Notes;
        }

        Log.WriteLine($"Migrated the project from {version} to {Project.CURRENT_VERSION}, {changed} files changed, kept as they were in {Path.GetFileName(backup)}");
        return new MigrationResult(version, changed, backup, notes);
    }
}

/// <param name="From">The version the project was</param>
/// <param name="Notes">What the migration couldn't do or the user has to know, like models to export again from Blender</param>
public sealed record MigrationResult(string From, int Changed, string? Backup, IReadOnlyList<string> Notes);

/// <summary>
/// What a migration step works with: the project's folder, and the writes that back up a file before its first change
/// </summary>
internal sealed class MigrationContext(string projectFolder, string backupPath) : IDisposable
{
    private readonly HashSet<string> _backedUp = new(StringComparer.Ordinal);
    private ZipArchive? _backup;

    public string ProjectFolder { get; } = projectFolder;

    public string AssetsFolder => Path.Combine(ProjectFolder, "assets");

    public IReadOnlyCollection<string> Changed => _backedUp;

    public List<string> Notes { get; } = [];

    /// <summary>
    /// Something the user has to know or do, logged and listed when the migration is done
    /// </summary>
    public void Note(string note, Log.LogType type = Log.LogType.Info)
    {
        Log.WriteLine(note, type);
        Notes.Add(note);
    }

    public void WriteAllText(string path, string text)
    {
        BackUp(path);
        File.WriteAllText(path, text);
    }

    public void Save(TlmFile file, string path)
    {
        BackUp(path);
        file.Save(path);
    }

    private void BackUp(string path)
    {
        var full = Path.GetFullPath(path);
        if (!_backedUp.Add(full) || !File.Exists(full))
        {
            return;
        }

        _backup ??= ZipFile.Open(backupPath, ZipArchiveMode.Create);
        _backup.CreateEntryFromFile(full, Path.GetRelativePath(ProjectFolder, full).Replace(Path.DirectorySeparatorChar, '/'), CompressionLevel.Fastest);
    }

    public void Dispose() => _backup?.Dispose();
}
