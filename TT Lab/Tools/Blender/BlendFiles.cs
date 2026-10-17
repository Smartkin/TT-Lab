using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using TT_Lab.Assets;
using TT_Lab.Project;

namespace TT_Lab.Tools.Blender;

/// <summary>
/// The blend file each model of a project is edited in, kept on this computer only (the state folder's blend-files.json, by the project's
/// UUID and the model's URI): a project shared between people has every person's own. A new one goes into the project's source folder
/// unless the user picks another place
/// </summary>
public static class BlendFiles
{
    public const string FileName = "blend-files.json";
    public const string SourceFolder = "source";

    // The state folder, tests have their own
    internal static string Folder { get; set; } = LinkProtocol.StateFolder();

    private static string Path => System.IO.Path.Combine(Folder, FileName);

    /// <summary>
    /// The model's blend file, none when there's none or it's gone
    /// </summary>
    public static string? Of(Guid project, LabURI model)
    {
        var path = Read()[project.ToString()]?[model.ToString()]?.GetValue<string>();
        return path != null && File.Exists(path) ? path : null;
    }

    public static void Remember(Guid project, LabURI model, string blend)
    {
        Update(model, all =>
        {
            if (all[project.ToString()] is not JsonObject models)
            {
                models = new JsonObject();
                all[project.ToString()] = models;
            }

            models[model.ToString()] = blend;
            return true;
        });
    }

    /// <summary>
    /// The model's blend file is the blend file no longer (its model in it was made another model's): Edit in Blender makes a new one
    /// </summary>
    public static void Forget(Guid project, LabURI model, string blend)
    {
        Update(model, all => all[project.ToString()] is JsonObject models && models[model.ToString()]?.GetValue<string>() is { } remembered && IsSameFile(remembered, blend)
                             && models.Remove(model.ToString()));
    }

    /// <summary>
    /// The model moved in the project (another folder or package): its blend file is the one of its new URI
    /// </summary>
    public static void Move(Guid project, LabURI from, LabURI to)
    {
        Update(to, all =>
        {
            if (all[project.ToString()] is not JsonObject models || models[from.ToString()]?.GetValue<string>() is not { } blend)
            {
                return false;
            }

            models.Remove(from.ToString());
            models[to.ToString()] = blend;
            return true;
        });
    }

    /// <summary>
    /// The blend file has no model of the project any more (TT Lab of another project made its model its own): none of the project's models
    /// opens it from now on
    /// </summary>
    public static void ForgetProject(Guid project, string blend)
    {
        Update(LabURI.Empty, all =>
        {
            if (all[project.ToString()] is not JsonObject models)
            {
                return false;
            }

            var changed = false;
            foreach (var (model, remembered) in models.ToList())
            {
                if (remembered?.GetValue<string>() is { } file && IsSameFile(file, blend))
                {
                    models.Remove(model);
                    changed = true;
                }
            }

            return changed;
        });
    }

    public static bool IsSameFile(string path, string other)
    {
        try
        {
            return AssetFileStamps.PathComparer.Equals(RealPath(path), RealPath(other));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    // The path with its symbolic links followed: a file reached through a link to its folder is that file
    private static string RealPath(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        var root = System.IO.Path.GetPathRoot(full) ?? string.Empty;
        var current = root;
        foreach (var part in full[root.Length..].Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var next = System.IO.Path.Combine(current, part);
            try
            {
                FileSystemInfo info = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
                current = info.ResolveLinkTarget(true)?.FullName ?? next;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                current = next;
            }
        }

        return current;
    }

    // Read, changed and written with the file kept to itself: every TT Lab of this computer writes it, one after the other
    private static void Update(LabURI model, Func<JsonObject, bool> change)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                using var file = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                var all = Parse(file);
                if (!change(all))
                {
                    return;
                }

                var bytes = Encoding.UTF8.GetBytes(all.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                file.SetLength(0);
                file.Write(bytes);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                // Another TT Lab has it
                Thread.Sleep(25);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Log.WriteLine($"Couldn't remember the blend file of {model}: {exception.Message}", Log.LogType.Warning);
                return;
            }
        }
    }

    private static JsonObject Parse(Stream file)
    {
        try
        {
            return file.Length > 0 && JsonNode.Parse(file) is JsonObject all ? all : new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
        finally
        {
            file.Position = 0;
        }
    }

    /// <summary>
    /// Where a model's new blend file goes unless the user picks another place: the project's source folder, named after the model
    /// </summary>
    public static string DefaultFor(string projectFolder, IAsset model)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var name = new string(model.Alias.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return System.IO.Path.Combine(projectFolder, SourceFolder, $"{(name.Length == 0 ? "Model" : name)}.blend");
    }

    private static JsonObject Read()
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (!File.Exists(Path))
                {
                    return new JsonObject();
                }

                using var file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return Parse(file);
            }
            catch (IOException) when (attempt < 20)
            {
                // Another TT Lab is writing it
                Thread.Sleep(25);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new JsonObject();
            }
        }
    }
}
