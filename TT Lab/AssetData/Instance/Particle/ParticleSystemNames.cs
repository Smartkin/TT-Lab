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

// Emitters play the system they name, their chunk's own or the default chunk's, so a system's name is its own in its chunk: a level's
// system of a default system's name is played there in that one's place, the level's own version of it (retail levels have such copies).
// The loaded particle assets are checked as they are, the default chunk's of a version nobody has loaded by the names in its file, read
// off the UI thread when the first name gets checked and again when the file changes
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
    /// Reads the names of the default chunks' particles nobody has loaded in the background
    /// </summary>
    public static void Prepare()
    {
        if (_reading is { IsCompleted: false } || OpenedProject() is not { } project)
        {
            return;
        }

        _reading = Task.Run(() =>
        {
            foreach (var asset in ParticleAssets(project, null).OfType<DefaultParticles>().Where(asset => !asset.IsLoaded))
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
    /// The system's particles when another of their systems has the name, none when the name is free in the chunk
    /// </summary>
    public static IAsset? FindOther(string name, IAsset owner, ParticleSystem system)
    {
        return owner is SerializableAsset particles && NamesOf(particles, system).Contains(name) ? owner : null;
    }

    /// <summary>
    /// The name, or the name with a number no other system of the chunk has. Names given to systems not in their particles yet (several
    /// pasted at once) are taken too. A level's new system doesn't take a default system's name either (<paramref name="avoidsDefaults"/>),
    /// it would be played in that one's place; one pasted keeps it, the level's own version of that system
    /// </summary>
    public static string MakeUnique(string name, IAsset owner, ParticleSystem system, IEnumerable<string>? alsoTaken = null, bool avoidsDefaults = false)
    {
        var taken = owner is SerializableAsset particles ? NamesOf(particles, system).ToHashSet() : [];
        if (avoidsDefaults && owner is not DefaultParticles && OpenedProject() is { } project)
        {
            foreach (var defaults in ParticleAssets(project, project.GetPlatform(owner.Package)).OfType<DefaultParticles>())
            {
                taken.UnionWith(NamesOf(defaults, system));
            }
        }

        if (alsoTaken != null)
        {
            taken.UnionWith(alsoTaken);
        }

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
