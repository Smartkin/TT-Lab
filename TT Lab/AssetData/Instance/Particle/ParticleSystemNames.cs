using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Project;
using GamePlatform = TT_Lab.Project.Project.GamePlatform;

namespace TT_Lab.AssetData.Instance.Particle;

// A particle system's name is its own in its version of the game: emitters play the system they name, their chunk's own or the default
// chunk's. The loaded particle assets are checked as they are, the others by the names in their files, read off the UI thread when the
// first name gets checked and again when a file changes
public static partial class ParticleSystemNames
{
    private sealed record FileNames(DateTime Written, string[] Names);

    private static readonly ConcurrentDictionary<string, FileNames> Files = new();
    private static Task? _reading;

    /// <summary>
    /// Forgets the names read of the closed project's files
    /// </summary>
    public static void Clear() => Files.Clear();

    /// <summary>
    /// Reads the names of every particle asset nobody has loaded in the background
    /// </summary>
    public static void Prepare()
    {
        if (_reading is { IsCompleted: false } || OpenedProject() is not { } project)
        {
            return;
        }

        _reading = Task.Run(() =>
        {
            foreach (var asset in ParticleAssets(project, null).Where(asset => !asset.IsLoaded))
            {
                try
                {
                    ReadNames(asset);
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
                {
                    Log.WriteLine($"The particle systems of {asset.Alias} couldn't be read: {ex.Message}", Log.LogType.Warning);
                }
            }
        });
    }

    /// <summary>
    /// The particle asset of the system's version of the game with another system of the name, the system's own when another of its
    /// systems has it, none when the name is free
    /// </summary>
    public static IAsset? FindOther(string name, IAsset owner, ParticleSystem system)
    {
        if (OpenedProject() is not { } project)
        {
            return null;
        }

        return ParticleAssets(project, project.GetPlatform(owner.Package)).FirstOrDefault(asset => NamesOf(asset, system).Contains(name));
    }

    /// <summary>
    /// The name, or the name with a number the game has no system of yet
    /// </summary>
    public static string MakeUnique(string name, IAsset owner, ParticleSystem system)
    {
        if (OpenedProject() is not { } project)
        {
            return name;
        }

        var taken = ParticleAssets(project, project.GetPlatform(owner.Package)).SelectMany(asset => NamesOf(asset, system)).ToHashSet();
        if (!taken.Contains(name))
        {
            return name;
        }

        var stem = NumberSuffix().Replace(name, string.Empty);
        for (var number = 2; ; number++)
        {
            var suffix = $"_{number}";
            var candidate = (stem.Length + suffix.Length > ParticleNames.Length ? stem[..(ParticleNames.Length - suffix.Length)] : stem) + suffix;
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    internal static IEnumerable<SerializableAsset> ParticleAssets(TT_Lab.Project.Project project, GamePlatform? platform)
    {
        var assetManager = project.AssetManager;
        return assetManager.GetAllAssetsOf<Particles>().Concat<SerializableAsset>(assetManager.GetAllAssetsOf<DefaultParticles>()).Distinct()
            .Where(asset => platform == null || project.GetPlatform(asset.Package) == platform);
    }

    private static IEnumerable<string> NamesOf(SerializableAsset asset, ParticleSystem except)
    {
        if (asset.IsLoaded && asset.AssetData is ParticleData data)
        {
            return data.ParticleSystems.Where(system => !ReferenceEquals(system, except)).Select(system => system.Name).ToList();
        }

        return ReadNames(asset);
    }

    private static string[] ReadNames(SerializableAsset asset)
    {
        var path = asset.FullDataPath;
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return [];
        }

        if (Files.TryGetValue(path, out var known) && known.Written == file.LastWriteTimeUtc)
        {
            return known.Names;
        }

        using var reader = new JsonTextReader(File.OpenText(path));
        var names = JObject.Load(reader)[nameof(ParticleData.ParticleSystems)]?.Select(system => (string?)system[nameof(ParticleSystem.Name)] ?? string.Empty).ToArray() ?? [];
        Files[path] = new FileNames(file.LastWriteTimeUtc, names);
        return names;
    }

    private static TT_Lab.Project.Project? OpenedProject() => Locator.Current.GetService<ProjectManager>()?.OpenedProject as TT_Lab.Project.Project;

    [GeneratedRegex(@"_\d+$")]
    private static partial Regex NumberSuffix();
}
