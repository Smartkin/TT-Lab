using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.TlModel;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.DynamicScenery;

namespace TT_Lab.Project.Migration;

/// <summary>
/// 1.0.0 to 1.1.0. 1.1.0 keeps a scenery's placed meshes and LODs as a list and builds the tree the game culls them with, 1.0.0's file
/// had them in that tree: every placement goes under the root's Meshes or LODs with the tree node it was in (its slots from the root,
/// <c>Node</c>) and its place among them (<c>Order</c>), the tree's root cell and depth go into the root's data and every tree node's
/// values into its <c>TreeNodes</c>, which TT Lab keeps where it doesn't work them out to the bit. <c>HasLighting</c> follows the lights
/// </summary>
internal static class Version110
{
    // 1.0.0's tree nodes, the game's octree as it read it
    private const string TreeNodeKind = "tree_node";
    private const string SlotKey = "Slot";
    private const string LightsKey = "LightsEnabler";
    private static readonly string[] TreeValues = ["BoundsCenter", "BoundsMin", "BoundsMax", "BoundsHalfSize"];
    // 1.0.0 named the placements by their tree node and place in it (Mesh 2.0), files written while 1.1.0 was made by their place among
    // all of them (Mesh 3): TT Lab numbers them in their group, the way it writes them
    private static readonly Regex OldPlacementName = new(@"^(Mesh|LOD) \d+(\.\d+)?$");
    private static readonly Regex OldLevelName = new(@"^LOD \d+(\.\d+)? Level (\d+)$");

    public static void Apply(MigrationContext context)
    {
        if (!Directory.Exists(context.AssetsFolder))
        {
            return;
        }

        // Sceneries' files are in their type's folders
        foreach (var path in Directory.EnumerateFiles(context.AssetsFolder, "*.tlm", SearchOption.AllDirectories)
                     .Where(path => Path.GetDirectoryName(path)!.Split(Path.DirectorySeparatorChar).Contains("Scenery")))
        {
            var file = TlmFile.Load(path);
            if (file.Root is not { } root || root.GetKind() != SceneryData.TlmKind || !MigrateScenery(root))
            {
                continue;
            }

            context.Save(file, path);
        }

        ReportSkeletons(context);
    }

    // 1.1.0 refuses models whose skeleton the game can't have (several roots, joints out of the order the game walks them in), which
    // 1.0.0's add-on exported: what the project has of them is left as it is, they need exporting again from Blender
    private static void ReportSkeletons(MigrationContext context)
    {
        foreach (var path in Directory.EnumerateFiles(context.AssetsFolder, "*.tlm", SearchOption.AllDirectories)
                     .Where(path => Path.GetDirectoryName(path)!.Split(Path.DirectorySeparatorChar).Contains("OGI")))
        {
            if (ReadJson(path)?["root"] is JsonObject root && root.GetKind() == OGIData.TlmKind && OGIData.SkeletonProblem(root) is { } problem)
            {
                Log.WriteLine($"TT Lab {Project.CURRENT_VERSION} can't read {Path.GetRelativePath(context.ProjectFolder, path)} until it's exported again from Blender: {problem}",
                    Log.LogType.Warning);
            }
        }
    }

    // A model file's JSON without its binary data
    private static JsonNode? ReadJson(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        if (reader.BaseStream.Length < 12 || !reader.ReadBytes(4).AsSpan().SequenceEqual("TLM\0"u8))
        {
            return null;
        }

        reader.ReadUInt32();
        var length = reader.ReadInt32();
        return JsonNode.Parse(System.Text.Encoding.UTF8.GetString(reader.ReadBytes(length)).TrimEnd(' ', '\0'));
    }

    /// <summary>
    /// Makes a scenery's file what 1.1.0 reads, returns whether anything changed
    /// </summary>
    internal static bool MigrateScenery(JsonObject root)
    {
        var flattened = Flatten(root);
        return Group(root) || flattened;
    }

    private static bool Flatten(JsonObject root)
    {
        var data = DataOf(root);
        var changed = data.Remove("HasLighting");
        var tree = root.GetChildren().FirstOrDefault(child => child.GetKind() == TreeNodeKind);
        if (tree == null)
        {
            return changed;
        }

        var treeData = tree.GetData();
        // The build gives the root a bit for each of the scenery's lights, a node keeps bits of its own where they differ from those
        var lightCount = root.Traverse().Count(node => node.GetKind() is SceneryData.AmbientLightKind or SceneryData.DirectionalLightKind or SceneryData.PointLightKind or SceneryData.SpotLightKind);
        data["TreeDepth"] = treeData["TreeDepth"]?.DeepClone() ?? 5;
        data["BoundsMin"] = FirstThree(treeData["BoundsMin"]);
        data["BoundsMax"] = FirstThree(treeData["BoundsMax"]);
        var kept = new List<JsonObject>();
        var placed = new List<(JsonObject Node, string Path, Matrix4x4 Matrix)>();
        var loose = new List<(JsonObject Node, Matrix4x4 Matrix)>();

        void Walk(JsonObject node, string path, Matrix4x4 matrix)
        {
            var values = node.GetData();
            // A node made in Blender has none of the game's values to keep
            if (TreeValues.All(values.ContainsKey))
            {
                var entry = new JsonObject { ["Path"] = path };
                foreach (var key in TreeValues)
                {
                    entry[key] = values[key]?.DeepClone();
                }

                if (path.Length > 0 && values[LightsKey] is JsonArray lights && !HasRootLights(lights, lightCount))
                {
                    entry[LightsKey] = lights.DeepClone();
                }

                kept.Add(entry);
            }

            var meshes = new List<(Int32 Order, Int32 Index, JsonObject Node)>();
            var lods = new List<(Int32 Order, Int32 Index, JsonObject Node)>();
            var children = new SortedDictionary<Int32, JsonObject>();
            var index = 0;
            foreach (var child in node.GetChildren().ToList())
            {
                if (child.GetKind() == TreeNodeKind)
                {
                    var slot = child.GetData()[SlotKey] is JsonValue value && value.TryGetValue<Int32>(out var stored) ? stored : -1;
                    if (slot is < 0 or >= 8 || children.ContainsKey(slot))
                    {
                        slot = Enumerable.Range(0, 8).Where(free => !children.ContainsKey(free)).DefaultIfEmpty(-1).First();
                    }

                    if (slot < 0)
                    {
                        // A ninth child: what's under it goes where the build puts new meshes
                        loose.Add((child, child.GetTransform() * matrix));
                    }
                    else
                    {
                        children[slot] = child;
                    }
                }
                else if (IsPlacement(child))
                {
                    (child.GetKind() == SceneryData.LodInstanceKind ? lods : meshes).Add((child.GetData().GetInt("Order", Int32.MaxValue), index, child));
                }
                else
                {
                    loose.Add((child, matrix));
                }

                index++;
            }

            foreach (var (_, _, child) in meshes.OrderBy(mesh => mesh.Order).ThenBy(mesh => mesh.Index).Concat(lods.OrderBy(lod => lod.Order).ThenBy(lod => lod.Index)))
            {
                placed.Add((child, path, matrix));
            }

            foreach (var (slot, child) in children)
            {
                Walk(child, path + slot, child.GetTransform() * matrix);
            }
        }

        Walk(tree, string.Empty, tree.GetTransform());
        var flat = new List<JsonObject>();
        for (var order = 0; order < placed.Count; order++)
        {
            var (child, path, matrix) = placed[order];
            var childData = DataOf(child);
            childData["Order"] = order;
            childData["Node"] = path;
            MoveUnder(child, matrix);
            flat.Add(child);
        }

        // Whatever else was under the tree goes under the root where it was, the meshes in it get new nodes
        foreach (var (child, matrix) in loose)
        {
            MoveUnder(child, matrix);
            flat.Add(child);
        }

        if (kept.Count > 0)
        {
            var treeNodes = new JsonObject();
            foreach (var entry in kept)
            {
                treeNodes[treeNodes.Count.ToString()] = entry;
            }

            data["TreeNodes"] = treeNodes;
        }

        var rootChildren = (JsonArray)root[TlmNodes.ChildrenKey]!;
        var at = rootChildren.IndexOf(tree);
        foreach (var child in flat)
        {
            Detach(child);
        }

        rootChildren.RemoveAt(at);
        for (var i = 0; i < flat.Count; i++)
        {
            rootChildren.Insert(at + i, flat[i]);
        }

        // Placements that weren't in the tree (made in Blender) go after the tree's in the order of the file, their own orders counted
        // the tree node they were in
        var next = placed.Count;
        var inTree = placed.Select(placement => placement.Node).ToHashSet();
        foreach (var other in root.Traverse().Where(node => IsPlacement(node) && !inTree.Contains(node)).ToList())
        {
            DataOf(other)["Order"] = next++;
        }

        return true;
    }

    private static Boolean HasRootLights(JsonArray lights, Int32 lightCount)
    {
        var bits = lights.Select(light => light is JsonValue value && value.TryGetValue<Boolean>(out var on) && on).ToList();
        return Enumerable.Range(0, Math.Max(bits.Count, SceneryData.MaxLights)).All(i => (i < bits.Count && bits[i]) == i < lightCount);
    }

    // The placements right under the scenery's root moved into its Meshes and LODs, the way Blender shows them
    private static bool Group(JsonObject root)
    {
        var children = root.GetChildren().ToList();
        var loose = children.Where(IsPlacement).ToList();
        var meshes = children.FirstOrDefault(child => child.GetKind() == SceneryData.MeshesKind);
        var lods = children.FirstOrDefault(child => child.GetKind() == SceneryData.LodsKind);
        if (loose.Count == 0 && meshes != null && lods != null)
        {
            return false;
        }

        meshes ??= TlmNodes.Create(SceneryData.MeshesKind, "Meshes");
        lods ??= TlmNodes.Create(SceneryData.LodsKind, "LODs");
        var rest = children.Where(child => child != meshes && child != lods && !loose.Contains(child)).ToList();
        foreach (var child in children)
        {
            Detach(child);
        }

        foreach (var child in loose)
        {
            var group = child.GetKind() == SceneryData.LodInstanceKind ? lods : meshes;
            var number = group.GetChildren().Count();
            if (OldPlacementName.IsMatch(child.GetString(TlmNodes.NameKey) ?? string.Empty))
            {
                child[TlmNodes.NameKey] = $"{(group == lods ? "LOD" : "Mesh")} {number}";
                foreach (var level in child.GetChildren())
                {
                    var oldLevel = OldLevelName.Match(level.GetString(TlmNodes.NameKey) ?? string.Empty);
                    if (oldLevel.Success)
                    {
                        level[TlmNodes.NameKey] = $"LOD {number} Level {oldLevel.Groups[2].Value}";
                    }
                }
            }

            group.AddChild(child);
        }

        root[TlmNodes.ChildrenKey] = new JsonArray([meshes, lods, .. rest]);
        return true;
    }

    // What the tree held: placed meshes and LODs, anything with a mesh but the parts of other things
    private static bool IsPlacement(JsonObject node)
    {
        var kind = node.GetKind();
        return kind is SceneryData.MeshInstanceKind or SceneryData.LodInstanceKind || node.ContainsKey(TlmNodes.MeshKey) &&
            kind is not (SceneryData.LodMeshKind or SkydomeData.SkydomeMeshKind or DynamicSceneryModelData.TlmKind or CollisionData.TlmKind or TlmHulls.Kind);
    }

    // The node takes the transform the tree's nodes above it gave it, it's going right under the root
    private static void MoveUnder(JsonObject node, Matrix4x4 matrix)
    {
        if (TlmNodes.IsIdentity(matrix, 1e-7f))
        {
            return;
        }

        var world = node.GetTransform() * matrix;
        node.Remove("translation");
        node.Remove("rotation");
        node.Remove("scale");
        node.SetTransform(world);
    }

    private static void Detach(JsonNode node)
    {
        if (node.Parent is JsonArray array)
        {
            array.Remove(node);
        }
    }

    private static JsonObject DataOf(JsonObject node)
    {
        if (node[TlmNodes.DataKey] is JsonObject data)
        {
            return data;
        }

        data = new JsonObject();
        node[TlmNodes.DataKey] = data;
        return data;
    }

    private static JsonArray FirstThree(JsonNode? values)
    {
        var numbers = values is JsonArray array ? array.Take(3).Select(value => value?.DeepClone()).ToArray() : [];
        return numbers.Length == 3 ? new JsonArray(numbers) : new JsonArray(0.0f, 0.0f, 0.0f);
    }
}
