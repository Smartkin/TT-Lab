using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Instance;

namespace TT_Lab.Project.Prefabs;

/// <summary>
/// Prefabs of every different object instance of the project's chunks, made when a project gets created: an instance's object, script,
/// flags and properties are what makes it different, where it is and how it's turned don't count and its links to the chunk's instances,
/// positions and paths are left out like any prefab's. A prefab goes into the folder of the chunk it's first found in (in the order of
/// their paths), the ones of the global package's objects (the default chunk's) into <see cref="GlobalFolder"/>, each version of the
/// game in a folder of its own when the project has both. What the library has a prefab of already, wherever it got moved and whatever
/// it got called, isn't made again
/// </summary>
public static class InstancePrefabs
{
    public const string GlobalFolder = "Global";

    // Where it is and how it's turned are its placing, the list growths the tools' leftovers
    private static readonly string[] NotConfiguration = [nameof(ObjectInstanceData.Position), nameof(ObjectInstanceData.Rotation),
        nameof(ObjectInstanceData.InstancesGrowth), nameof(ObjectInstanceData.PositionsGrowth), nameof(ObjectInstanceData.PathsGrowth)];

    /// <summary>
    /// Saves the prefabs of the configurations the library has none of into it. How many were made and how many it had already come back
    /// </summary>
    public static (int Made, int Kept) Make(Project project, PrefabLibrary library)
    {
        var assetManager = project.AssetManager;
        var chunks = assetManager.GetAllAssetsOf<LevelChunk>().Where(chunk => chunk.AdditionalPath != null)
            .OrderBy(chunk => chunk.AdditionalPath, StringComparer.Ordinal).ToList();
        var platforms = chunks.Select(chunk => project.GetPlatform(chunk.Package)).Distinct().ToList();
        var objectNames = new Dictionary<LabURI, string>();
        var existing = library.Load();
        var known = existing.Where(IsObjectInstance).Select(prefab => (prefab.Platform, ConfigurationOf(prefab.Data))).ToHashSet();
        var made = 0;
        var kept = 0;
        foreach (var platform in platforms)
        {
            var versionFolder = platforms.Count > 1 ? platform.ToString() : string.Empty;
            var configurations = new HashSet<string>(StringComparer.Ordinal);
            // A new prefab doesn't take the place of one the folder has
            var namesTaken = PrefabLibrary.NamesByFolder(existing);
            foreach (var chunk in chunks.Where(chunk => project.GetPlatform(chunk.Package) == platform))
            {
                foreach (var instance in chunk.ChunkResources.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset).OfType<ObjectInstance>())
                {
                    var (dataType, data) = PrefabLibrary.CaptureInstanceData(instance);
                    var configuration = ConfigurationOf(data);
                    if (!configurations.Add(configuration))
                    {
                        continue;
                    }

                    if (known.Contains((platform.ToString(), configuration)))
                    {
                        kept++;
                        continue;
                    }

                    data[nameof(ObjectInstanceData.Position)] = JObject.FromObject(new Twinsanity.TwinsanityInterchange.Common.Vector3());
                    data[nameof(ObjectInstanceData.Rotation)] = JObject.FromObject(new Twinsanity.TwinsanityInterchange.Common.Vector3());
                    var objectUri = data[nameof(ObjectInstanceData.ObjectId)]?.ToObject<LabURI>() ?? LabURI.Empty;
                    var isGlobal = assetManager.DoesAssetExist(objectUri) && assetManager.GetAsset(objectUri).Package is var package &&
                                   (package == project.GlobalPackagePS2.URI || package == project.GlobalPackageXbox.URI);
                    var folder = PrefabLibrary.Join(versionFolder, isGlobal ? GlobalFolder : chunk.AdditionalPath!.Replace('\\', '/'));
                    var taken = namesTaken.TryGetValue(folder, out var names) ? names : namesTaken[folder] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var name = UniqueName(NameOf(objectUri, objectNames), taken);
                    library.Save(new Prefab
                    {
                        Name = name,
                        Kind = PrefabKind.Instance,
                        Platform = platform.ToString(),
                        Package = instance.Package,
                        MadeFrom = $"{instance.Alias} of {chunk.AdditionalPath}",
                        AssetType = instance.GetType().FullName!,
                        DataType = dataType,
                        LayoutID = instance.LayoutID,
                        Data = data,
                        Folder = folder,
                    });
                    made++;
                }
            }
        }

        return (made, kept);
    }

    private static bool IsObjectInstance(Prefab prefab) => prefab.Kind == PrefabKind.Instance && prefab.AssetType == typeof(ObjectInstance).FullName;

    // What the instance is made of without where it is, as the prefab's file has it: the data captured and the data read back write
    // their values the same way
    private static string ConfigurationOf(JObject data)
    {
        var configuration = JObject.Parse(data.ToString(Formatting.None));
        foreach (var name in NotConfiguration)
        {
            configuration.Remove(name);
        }

        return configuration.ToString(Formatting.None);
    }

    // The game names objects of a level by their path in its tools ("|L05river|act_ICE_RIVER_PLATFORM"), the last part without its
    // act_ is what it is
    private static string NameOf(LabURI objectUri, Dictionary<LabURI, string> names)
    {
        if (names.TryGetValue(objectUri, out var name))
        {
            return name;
        }

        var assetManager = AssetManager.Get();
        if (!assetManager.DoesAssetExist(objectUri) || assetManager.GetAsset(objectUri) is not GameObject gameObject)
        {
            return names[objectUri] = "Object instance";
        }

        var wasLoaded = gameObject.IsLoaded;
        var gameName = ((IAsset)gameObject).GetData<GameObjectData>().Name;
        if (!wasLoaded)
        {
            gameObject.UnloadData();
        }

        var last = (string.IsNullOrWhiteSpace(gameName) ? gameObject.Alias : gameName).Split('|', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? gameObject.Alias;
        // Copies of actors are "ActorCopy_act_..."; the tools wrote the prefix in lower case, the names in upper case (IMPACT_ stays)
        var actor = last.StartsWith("act_", StringComparison.Ordinal) ? 0 : last.IndexOf("_act_", StringComparison.Ordinal) is >= 0 and var inner ? inner + 1 : -1;
        if (actor >= 0 && last.Length > actor + 4)
        {
            last = last[(actor + 4)..];
        }

        return names[objectUri] = last;
    }

    private static string UniqueName(string name, HashSet<string> taken)
    {
        var candidate = name;
        for (var number = 2; !taken.Add(candidate); number++)
        {
            candidate = $"{name} {number}";
        }

        return candidate;
    }
}
