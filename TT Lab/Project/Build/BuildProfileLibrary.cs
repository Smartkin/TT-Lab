using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Splat;

namespace TT_Lab.Project.Build;

/// <summary>
/// The project's build profiles, one JSON file each in the project's profiles folder
/// </summary>
public sealed class BuildProfileLibrary
{
    public const string FolderName = "profiles";
    private static readonly Regex UnsafeCharacters = new("[^A-Za-z0-9 _.-]+", RegexOptions.Compiled);

    public BuildProfileLibrary(Project project)
    {
        Folder = Path.Combine(project.ProjectPath, FolderName);
    }

    public string Folder { get; }

    public static BuildProfileLibrary? ForOpenedProject()
    {
        return Locator.Current.GetService<ProjectManager>()?.OpenedProject is Project project ? new BuildProfileLibrary(project) : null;
    }

    public List<BuildProfile> Load()
    {
        var profiles = new List<BuildProfile>();
        if (!Directory.Exists(Folder))
        {
            return profiles;
        }

        foreach (var file in Directory.EnumerateFiles(Folder, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var profile = JsonConvert.DeserializeObject<BuildProfile>(File.ReadAllText(file));
                if (profile == null)
                {
                    continue;
                }

                profile.FilePath = file;
                profiles.Add(profile);
            }
            catch (Exception e)
            {
                Log.WriteLine($"Build profile {Path.GetFileName(file)} couldn't be read: {e.Message}", Log.LogType.Warning);
            }
        }

        return profiles.OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Writes the profile's file, a profile saved under the name of another replaces it
    /// </summary>
    public void Save(BuildProfile profile)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, $"{FileName(profile.Name)}.json");
        if (profile.FilePath != null && profile.FilePath != path && File.Exists(profile.FilePath))
        {
            File.Delete(profile.FilePath);
        }

        profile.FilePath = path;
        File.WriteAllText(path, JsonConvert.SerializeObject(profile, Formatting.Indented));
    }

    public void Delete(BuildProfile profile)
    {
        if (profile.FilePath != null && File.Exists(profile.FilePath))
        {
            File.Delete(profile.FilePath);
        }

        profile.FilePath = null;
    }

    private static string FileName(string name)
    {
        var safe = UnsafeCharacters.Replace(name, "_").Trim();
        return safe.Length == 0 ? "profile" : safe;
    }
}
