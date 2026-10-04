using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.ScenerySubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;
using Vector3 = System.Numerics.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.AssetData.Instance.Scenery;

/// <summary>
/// The tree the game culls a scenery with, made from its placed meshes and LODs when the scenery is built (checked against every tree
/// of the PS2 and Xbox discs, <c>Ghidra Stuff/Engine_RE</c>'s Scenery trees). It's an octree of the root's cell: a node's children are
/// its slots' octants (bit 0 X, 1 Y, 2 Z, a set bit the lower half, <c>FUN_001eeac0</c>), nodes at the tree's depth are leaves, every
/// node has something placed under it. The draw culls a node's subtree by its box, the cell grown to hold everything under it (the
/// root's never grows), and every placed box by its own. A box is in a node while it's within the node's cell grown twice around its
/// middle, which every retail one is. New boxes go down the octants their middle is in for as long as that holds, which is where the
/// tools put 91% of them (the rest depends on the order the tools put them in)
/// </summary>
public static class SceneryTree
{
    /// <summary>
    /// What new sceneries get, the depth of most of the game's trees (2 to 5)
    /// </summary>
    public const UInt32 DefaultDepth = 5;

    private const Int32 MaxDepth = 16;

    // How far a placed box may reach from its node's middle, in the node's half sizes
    private const Single LooseLimit = 2.0001f;

    // The tools rounded some values otherwise, theirs are kept within this share of the node's size
    private const Single KeptTolerance = 1e-5f;

    public readonly record struct Box(Vector3 Min, Vector3 Max)
    {
        public Box Union(Box other) => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
    }

    public sealed class Node(string path, Box cell)
    {
        public string Path { get; } = path;

        /// <summary>
        /// The node's octant of the root's cell
        /// </summary>
        public Box Cell { get; } = cell;

        /// <summary>
        /// The cell grown to hold everything placed under the node
        /// </summary>
        public Box Bounds { get; set; } = cell;

        public ITwinScenery.SceneryType Kind { get; set; }

        public ITwinScenery.SceneryType[] Slots { get; } = Enumerable.Repeat(ITwinScenery.SceneryType.None, 8).ToArray();

        public List<Int32> Meshes { get; } = [];

        public List<Int32> Lods { get; } = [];

        public Vector4 BoundsCenter { get; set; } = new();

        public Vector4 BoundsMin { get; set; } = new();

        public Vector4 BoundsMax { get; set; } = new();

        public Vector4 BoundsHalfSize { get; set; } = new();
    }

    /// <summary>
    /// The placement's box where it is in the scenery
    /// </summary>
    public static Box WorldBox(SceneryPlacement placement)
    {
        var box = placement.Box;
        var min = new Vector3(box.V1.X, box.V1.Y, box.V1.Z);
        var max = new Vector3(box.V2.X, box.V2.Y, box.V2.Z);
        Vector3[] corners =
        [
            new(min.X, min.Y, min.Z), new(max.X, min.Y, min.Z), new(min.X, max.Y, min.Z), new(min.X, min.Y, max.Z),
            new(max.X, max.Y, min.Z), new(max.X, min.Y, max.Z), new(min.X, max.Y, max.Z), new(max.X, max.Y, max.Z)
        ];
        var matrix = placement.Matrix;
        var low = new Vector3(Single.PositiveInfinity);
        var high = new Vector3(Single.NegativeInfinity);
        foreach (var corner in corners)
        {
            var point = new Vector3(
                corner.X * matrix.Column1.X + corner.Y * matrix.Column2.X + corner.Z * matrix.Column3.X + matrix.Column4.X,
                corner.X * matrix.Column1.Y + corner.Y * matrix.Column2.Y + corner.Z * matrix.Column3.Y + matrix.Column4.Y,
                corner.X * matrix.Column1.Z + corner.Y * matrix.Column2.Z + corner.Z * matrix.Column3.Z + matrix.Column4.Z);
            low = Vector3.Min(low, point);
            high = Vector3.Max(high, point);
        }

        return new Box(low, high);
    }

    /// <summary>
    /// The octant of a cell in the game's numbering and float math (<c>FUN_001ebe00</c>)
    /// </summary>
    public static Box Octant(Box parent, Int32 slot)
    {
        var size = parent.Max - parent.Min;
        var quarter = size * 0.5f * 0.5f;
        var offset = new Vector3((slot & 1) != 0 ? -quarter.X : quarter.X, (slot & 2) != 0 ? -quarter.Y : quarter.Y, (slot & 4) != 0 ? -quarter.Z : quarter.Z);
        var center = offset + (parent.Min + parent.Max) * 0.5f;
        return new Box(center - quarter, center + quarter);
    }

    public static Box CellOf(Box root, string path)
    {
        var cell = root;
        foreach (var slot in path)
        {
            cell = Octant(cell, slot - '0');
        }

        return cell;
    }

    /// <summary>
    /// The node a placed box goes in: the one it's kept in while that one still holds it, else the deepest one holding it on the way down
    /// the octants its middle is in
    /// </summary>
    public static string PathOf(Box root, UInt32 depth, Box box, string? kept)
    {
        var limit = (Int32)Math.Min(depth, MaxDepth);
        if (kept != null && kept.Length <= limit && kept.All(slot => slot is >= '0' and <= '7') && (kept.Length == 0 || Holds(CellOf(root, kept), box)))
        {
            return kept;
        }

        var middle = (box.Min + box.Max) * 0.5f;
        var cell = root;
        var path = string.Empty;
        while (path.Length < limit)
        {
            var center = (cell.Min + cell.Max) * 0.5f;
            var slot = (middle.X < center.X ? 1 : 0) | (middle.Y < center.Y ? 2 : 0) | (middle.Z < center.Z ? 4 : 0);
            var next = Octant(cell, slot);
            if (!Holds(next, box))
            {
                break;
            }

            cell = next;
            path += (char)('0' + slot);
        }

        return path;
    }

    private static Boolean Holds(Box cell, Box box)
    {
        var center = (cell.Min + cell.Max) * 0.5f;
        var half = Vector3.Max((cell.Max - cell.Min) * 0.5f, new Vector3(1e-6f));
        var reach = Vector3.Max(Vector3.Abs(box.Min - center), Vector3.Abs(box.Max - center)) / half;
        return MathF.Max(reach.X, MathF.Max(reach.Y, reach.Z)) <= LooseLimit;
    }

    /// <summary>
    /// The nodes of the tree in the order the game stores them, every node followed by its children in slot order, with the values worked
    /// out from what's placed in them
    /// </summary>
    public static List<Node> Layout(Box root, UInt32 depth, IReadOnlyList<SceneryPlacement> placements)
    {
        var limit = (Int32)Math.Min(depth, MaxDepth);
        var nodes = new Dictionary<string, Node>();
        Node Get(string path)
        {
            if (!nodes.TryGetValue(path, out var node))
            {
                node = new Node(path, path.Length == 0 ? root : Octant(Get(path[..^1]).Cell, path[^1] - '0'));
                nodes[path] = node;
            }

            return node;
        }

        Get(string.Empty);
        for (var i = 0; i < placements.Count; i++)
        {
            var box = WorldBox(placements[i]);
            var path = PathOf(root, depth, box, placements[i].Node);
            var node = Get(path);
            (placements[i].IsLod ? node.Lods : node.Meshes).Add(i);
            // Every node on the way grows to hold the box but the root, whose cell is the box the game keeps the chunk's objects in
            for (; path.Length > 0; path = path[..^1])
            {
                nodes[path].Bounds = nodes[path].Bounds.Union(box);
            }
        }

        var ordered = new List<Node>();
        Visit(nodes[string.Empty]);
        foreach (var node in ordered)
        {
            var parentRadius = node.Path.Length == 0 ? 1.0f : ((nodes[node.Path[..^1]].Cell.Max - nodes[node.Path[..^1]].Cell.Min) * 0.5f).Length();
            var (min, max) = (node.Bounds.Min, node.Bounds.Max);
            var half = (max - min) * 0.5f;
            var center = (min + max) * 0.5f;
            node.BoundsMin = new Vector4(min.X, min.Y, min.Z, parentRadius);
            node.BoundsMax = new Vector4(max.X, max.Y, max.Z, parentRadius);
            node.BoundsHalfSize = new Vector4(half.X, half.Y, half.Z, parentRadius);
            node.BoundsCenter = new Vector4(center.X, center.Y, center.Z, half.Length());
        }

        return ordered;

        void Visit(Node node)
        {
            node.Kind = node.Path.Length == 0 ? ITwinScenery.SceneryType.Root : node.Path.Length == limit ? ITwinScenery.SceneryType.Leaf : ITwinScenery.SceneryType.Node;
            ordered.Add(node);
            for (var slot = 0; slot < 8; slot++)
            {
                if (!nodes.TryGetValue(node.Path + (char)('0' + slot), out var child))
                {
                    continue;
                }

                node.Slots[slot] = child.Path.Length == limit ? ITwinScenery.SceneryType.Leaf : ITwinScenery.SceneryType.Node;
                Visit(child);
            }
        }
    }

    /// <summary>
    /// The game's tree, the values a node kept used while they're still within a hair of the ones worked out
    /// </summary>
    /// <param name="rootLights">The lights the root turns on, the nodes without light bits of their own get them as well</param>
    public static List<TwinSceneryBaseType> Build(Box root, UInt32 depth, IReadOnlyList<SceneryPlacement> placements, IEnumerable<SceneryTreeNode> kept,
        Boolean[] rootLights, Func<SceneryPlacement, UInt32> idOf)
    {
        var keptNodes = new Dictionary<string, SceneryTreeNode>();
        foreach (var node in kept)
        {
            keptNodes.TryAdd(node.Path, node);
        }

        var result = new List<TwinSceneryBaseType>();
        foreach (var node in Layout(root, depth, placements))
        {
            TwinSceneryBaseType item = node.Kind switch
            {
                ITwinScenery.SceneryType.Root => new TwinSceneryRoot { TreeDepth = depth },
                ITwinScenery.SceneryType.Node => new TwinSceneryNode(),
                _ => new TwinSceneryLeaf()
            };
            if (item is TwinSceneryNode treeNode)
            {
                Array.Copy(node.Slots, treeNode.SceneryTypes, 8);
            }

            keptNodes.TryGetValue(node.Path, out var stored);
            var tolerance = KeptTolerance * MathF.Max(1.0f, (node.Bounds.Max - node.Bounds.Min).Length());
            item.BoundsCenter = Keep(stored?.BoundsCenter, node.BoundsCenter, tolerance);
            item.BoundsMin = Keep(stored?.BoundsMin, node.BoundsMin, tolerance);
            item.BoundsMax = Keep(stored?.BoundsMax, node.BoundsMax, tolerance);
            item.BoundsHalfSize = Keep(stored?.BoundsHalfSize, node.BoundsHalfSize, tolerance);
            // Only the root's are read (FUN_001c7f50), the lights are the scenery's
            var lights = node.Path.Length > 0 && stored?.LightsEnabler is { } own ? own : rootLights;
            Array.Copy(lights, item.LightsEnabler, Math.Min(lights.Length, item.LightsEnabler.Length));
            foreach (var index in node.Meshes)
            {
                var placement = placements[index];
                item.MeshIDs.Add(idOf(placement));
                item.MeshModelMatrices.Add(placement.Matrix);
                item.BoundingBoxes.Add([placement.Box.V1, placement.Box.V2]);
            }

            // Every box of the node's meshes comes before the LODs'
            foreach (var index in node.Lods)
            {
                var placement = placements[index];
                item.LodIDs.Add(idOf(placement));
                item.LodModelMatrices.Add(placement.Matrix);
                item.BoundingBoxes.Add([placement.Box.V1, placement.Box.V2]);
            }

            result.Add(item);
        }

        return result;
    }

    /// <summary>
    /// Whether the kept values are the ones worked out for the node to the bit, which makes them redundant
    /// </summary>
    public static Boolean IsWorkedOut(SceneryTreeNode kept, Node node)
    {
        return SameBits(kept.BoundsCenter, node.BoundsCenter) && SameBits(kept.BoundsMin, node.BoundsMin) && SameBits(kept.BoundsMax, node.BoundsMax) &&
               SameBits(kept.BoundsHalfSize, node.BoundsHalfSize);
    }

    private static Vector4 Keep(Vector4? stored, Vector4 computed, Single tolerance)
    {
        if (stored != null && MathF.Abs(stored.X - computed.X) <= tolerance && MathF.Abs(stored.Y - computed.Y) <= tolerance &&
            MathF.Abs(stored.Z - computed.Z) <= tolerance && MathF.Abs(stored.W - computed.W) <= tolerance)
        {
            return stored;
        }

        return computed;
    }

    public sealed record Flattened(Box Root, UInt32 Depth, List<SceneryPlacement> Placements, List<SceneryTreeNode> Kept);

    /// <summary>
    /// The game's tree as what's placed in it, in the tree's order with the node each placement is in, and the values of the nodes the
    /// build wouldn't work out to the bit
    /// </summary>
    /// <param name="rootLights">The lights the root turns on, nodes with other light bits keep theirs</param>
    /// <param name="uriOf">The mesh, or LOD when the flag's set, of an ID</param>
    public static Flattened Flatten(IReadOnlyList<TwinSceneryBaseType> tree, Boolean[] rootLights, Func<UInt32, Boolean, LabURI> uriOf)
    {
        if (tree.Count == 0 || tree[0] is not TwinSceneryRoot root)
        {
            return new Flattened(new Box(Vector3.Zero, Vector3.Zero), DefaultDepth, [], []);
        }

        var paths = new string[tree.Count];
        var next = 0;
        Walk(string.Empty);
        var placements = new List<SceneryPlacement>();
        for (var i = 0; i < next; i++)
        {
            var node = tree[i];
            for (var mesh = 0; mesh < node.MeshIDs.Count; mesh++)
            {
                placements.Add(Placement(uriOf(node.MeshIDs[mesh], false), false, node.MeshModelMatrices[mesh], node.BoundingBoxes[mesh], paths[i]));
            }

            for (var lod = 0; lod < node.LodIDs.Count; lod++)
            {
                placements.Add(Placement(uriOf(node.LodIDs[lod], true), true, node.LodModelMatrices[lod], node.BoundingBoxes[node.MeshIDs.Count + lod], paths[i]));
            }
        }

        var rootBox = new Box(new Vector3(root.BoundsMin.X, root.BoundsMin.Y, root.BoundsMin.Z), new Vector3(root.BoundsMax.X, root.BoundsMax.Y, root.BoundsMax.Z));
        var laidOut = Layout(rootBox, root.TreeDepth, placements).ToDictionary(node => node.Path);
        var lit = new Boolean[root.LightsEnabler.Length];
        Array.Copy(rootLights, lit, Math.Min(rootLights.Length, lit.Length));
        var kept = new List<SceneryTreeNode>();
        for (var i = 0; i < next; i++)
        {
            var node = tree[i];
            var lightsDiffer = i > 0 && !node.LightsEnabler.SequenceEqual(lit);
            if (!laidOut.TryGetValue(paths[i], out var worked) || !lightsDiffer && SameBits(node.BoundsCenter, worked.BoundsCenter) &&
                SameBits(node.BoundsMin, worked.BoundsMin) && SameBits(node.BoundsMax, worked.BoundsMax) && SameBits(node.BoundsHalfSize, worked.BoundsHalfSize))
            {
                continue;
            }

            kept.Add(new SceneryTreeNode
            {
                Path = paths[i],
                BoundsCenter = Copy(node.BoundsCenter),
                BoundsMin = Copy(node.BoundsMin),
                BoundsMax = Copy(node.BoundsMax),
                BoundsHalfSize = Copy(node.BoundsHalfSize),
                LightsEnabler = lightsDiffer ? (Boolean[])node.LightsEnabler.Clone() : null
            });
        }

        return new Flattened(rootBox, root.TreeDepth, placements, kept);

        // Every node is followed by its children, in slot order
        void Walk(string path)
        {
            if (next >= tree.Count)
            {
                return;
            }

            var node = tree[next];
            paths[next++] = path;
            if (node is not TwinSceneryNode treeNode)
            {
                return;
            }

            for (var slot = 0; slot < treeNode.SceneryTypes.Length; slot++)
            {
                if (treeNode.SceneryTypes[slot] is ITwinScenery.SceneryType.Node or ITwinScenery.SceneryType.Leaf)
                {
                    Walk(path + (char)('0' + slot));
                }
            }
        }
    }

    private static SceneryPlacement Placement(LabURI model, Boolean isLod, Matrix4 matrix, Vector4[] box, string path)
    {
        return new SceneryPlacement
        {
            Model = model,
            IsLod = isLod,
            Matrix = new Matrix4 { Column1 = Copy(matrix.Column1), Column2 = Copy(matrix.Column2), Column3 = Copy(matrix.Column3), Column4 = Copy(matrix.Column4) },
            Box = new BoundingBox { V1 = Copy(box[0]), V2 = Copy(box[1]) },
            Node = path
        };
    }

    private static Vector4 Copy(Vector4 vector) => new(vector.X, vector.Y, vector.Z, vector.W);

    private static Boolean SameBits(Vector4 a, Vector4 b)
    {
        return BitConverter.SingleToInt32Bits(a.X) == BitConverter.SingleToInt32Bits(b.X) && BitConverter.SingleToInt32Bits(a.Y) == BitConverter.SingleToInt32Bits(b.Y) &&
               BitConverter.SingleToInt32Bits(a.Z) == BitConverter.SingleToInt32Bits(b.Z) && BitConverter.SingleToInt32Bits(a.W) == BitConverter.SingleToInt32Bits(b.W);
    }
}
