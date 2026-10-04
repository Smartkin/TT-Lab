using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using Scenery = TT_Lab.Assets.Instance.Scenery;

namespace TT_Lab.Project.Prefabs;

/// <summary>
/// Prefabs of every different mesh and LOD placed in the project's sceneries, made when a project gets created like the instances'
/// (<see cref="InstancePrefabs"/>): a mesh's geometry and materials and a LOD's values and levels are what makes it different, where it's
/// placed, how it's turned and how big don't count (the prefab stands at the origin as it is) and neither does the box the game culled it
/// with. Each goes into the <see cref="MeshesFolder"/> or <see cref="LodsFolder"/> of the folder of the chunk it's first found in (in the
/// order of their paths), each version of the game in a folder of its own when the project has both. What the library has a prefab of
/// already, wherever it got moved and whatever it got called, isn't made again
/// </summary>
public static class SceneryPrefabs
{
    public const string MeshesFolder = "Meshes";
    public const string LodsFolder = "LODs";

    /// <summary>
    /// Saves the prefabs of the meshes and LODs the library has none of into it, read from the sceneries' files. How many were made and
    /// how many it had already come back
    /// </summary>
    public static (int Made, int Kept) Make(Project project, PrefabLibrary library)
    {
        var assetManager = project.AssetManager;
        var chunks = assetManager.GetAllAssetsOf<LevelChunk>().Where(chunk => chunk.AdditionalPath != null)
            .OrderBy(chunk => chunk.AdditionalPath, StringComparer.Ordinal).ToList();
        var platforms = chunks.Select(chunk => project.GetPlatform(chunk.Package)).Distinct().ToList();
        var existing = library.Load();
        var known = existing.Where(prefab => prefab.Kind == PrefabKind.Scenery).Select(prefab => prefab.Data[PrefabLibrary.SceneryContentKey]?.ToString())
            .OfType<string>().ToHashSet(StringComparer.Ordinal);
        var namesTaken = PrefabLibrary.NamesByFolder(existing);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var made = 0;
        var kept = 0;
        foreach (var platform in platforms)
        {
            var versionFolder = platforms.Count > 1 ? platform.ToString() : string.Empty;
            foreach (var chunk in chunks.Where(chunk => project.GetPlatform(chunk.Package) == platform))
            {
                var scenery = chunk.ChunkResources.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset).OfType<Scenery>().FirstOrDefault();
                if (scenery == null || !File.Exists(scenery.FullDataPath))
                {
                    continue;
                }

                TlmFile file;
                try
                {
                    file = TlmFile.Load(scenery.FullDataPath);
                }
                catch (Exception e) when (e is IOException or InvalidDataException or JsonException)
                {
                    Log.WriteLine($"The scenery of {chunk.AdditionalPath} couldn't be read for its prefabs: {e.Message}", Log.LogType.Warning);
                    continue;
                }

                var chunkFolder = PrefabLibrary.Join(versionFolder, chunk.AdditionalPath!.Replace('\\', '/'));
                foreach (var (node, isLod) in PlacementsOf(file.Root))
                {
                    var (model, content) = PrefabFile(file, node, isLod);
                    if (!seen.Add(content))
                    {
                        continue;
                    }

                    if (known.Contains(content))
                    {
                        kept++;
                        continue;
                    }

                    var folder = PrefabLibrary.Join(chunkFolder, isLod ? LodsFolder : MeshesFolder);
                    var taken = namesTaken.TryGetValue(folder, out var names) ? names : namesTaken[folder] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    library.Save(new Prefab
                    {
                        Name = NextName(isLod ? "LOD" : "Mesh", taken),
                        Kind = PrefabKind.Scenery,
                        Platform = platform.ToString(),
                        Package = scenery.Package,
                        MadeFrom = chunk.AdditionalPath!,
                        AssetType = typeof(Scenery).FullName!,
                        Data = new JObject { [PrefabLibrary.SceneryCountKey] = 1, [PrefabLibrary.SceneryContentKey] = content },
                        Model = model,
                        Folder = folder,
                    });
                    made++;
                }
            }
        }

        return (made, kept);
    }

    // The placed meshes and LODs of a scenery's file in the scenery's order, found the way the scenery reads them: anywhere under the root
    // but in the collision and the dynamic scenery, a LOD with its levels
    private static List<(JsonObject Node, bool IsLod)> PlacementsOf(JsonObject? root)
    {
        var found = new List<(JsonObject Node, bool IsLod, int Order, int Index)>();
        Walk(root);
        return found.OrderBy(placement => placement.Order).ThenBy(placement => placement.Index).Select(placement => (placement.Node, placement.IsLod)).ToList();

        void Walk(JsonObject? node)
        {
            foreach (var child in node.GetChildren())
            {
                var kind = child.GetKind();
                if (kind is CollisionData.TlmKind or DynamicSceneryData.TlmKind)
                {
                    continue;
                }

                var isLod = kind == SceneryData.LodInstanceKind;
                if (isLod || child[TlmNodes.MeshKey] != null)
                {
                    found.Add((child, isLod, child.GetData().GetInt("Order", Int32.MaxValue), found.Count));
                }

                if (!isLod)
                {
                    Walk(child);
                }
            }
        }
    }

    // The placed mesh or LOD on its own as a scenery prefab's model file, at the origin as it is, and what it's made of: the file without
    // the box the game culled it with, which another placing of the same mesh can have another of
    private static (byte[] Model, string Content) PrefabFile(TlmFile source, JsonObject node, bool isLod)
    {
        var file = new TlmFile(SceneryData.TlmAssetType, isLod ? "LOD" : "Mesh");
        var copy = file.CopyNode(source, node, new Dictionary<int, int>());
        foreach (var key in new[] { "translation", "rotation", "scale" })
        {
            copy.Remove(key);
        }

        copy[TlmNodes.NameKey] = isLod ? "LOD" : "Mesh";
        if (isLod)
        {
            var level = 0;
            foreach (var levelNode in copy.GetChildren())
            {
                levelNode[TlmNodes.NameKey] = $"Level {level++}";
            }
        }
        else
        {
            // What's under a placed mesh is placed on its own
            copy.Remove(TlmNodes.ChildrenKey);
        }

        var data = copy[TlmNodes.DataKey] as JsonObject;
        foreach (var key in new[] { "Order", "Node", "Matrix" })
        {
            data?.Remove(key);
        }

        var box = data?["BoundingBox"];
        data?.Remove("BoundingBox");
        var root = TlmNodes.Create(SceneryData.TlmKind, "Prefab");
        var meshes = root.AddChild(TlmNodes.Create(SceneryData.MeshesKind, "Meshes"));
        var lods = root.AddChild(TlmNodes.Create(SceneryData.LodsKind, "LODs"));
        (isLod ? lods : meshes).AddChild(copy);
        file.Root = root;
        var content = Convert.ToHexString(SHA256.HashData(Bytes(file)));
        if (box != null)
        {
            data!["BoundingBox"] = box;
        }

        return (Bytes(file), content);
    }

    private static byte[] Bytes(TlmFile file)
    {
        using var stream = new MemoryStream();
        file.WriteTo(stream);
        return stream.ToArray();
    }

    // Numbered in their folder, past the names it has
    private static string NextName(string kind, HashSet<string> taken)
    {
        for (var number = 1; ; number++)
        {
            var name = $"{kind} {number}";
            if (taken.Add(name))
            {
                return name;
            }
        }
    }
}
