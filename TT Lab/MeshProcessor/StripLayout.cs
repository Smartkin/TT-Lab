using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Graphics.SubModels;
using Twinsanity.PS2Hardware;

namespace TT_Lab.MeshProcessor;

/// <summary>
/// Which of a strip's triangles the game draws flipped
/// </summary>
/// <remarks>
/// Every triangle of a strip is made of the last 3 vertexes, every second one is drawn with its first two vertexes swapped.
/// Rigid models flip the triangles ending on an even vertex of their batch. Skins flip the ones ending on an odd vertex counted
/// from the start of their strip, which starts two vertexes before one that draws after two that don't: counted from the batch,
/// every strip of a skin starting on an odd vertex faced away from its normals
/// </remarks>
public enum StripWinding
{
    EvenFlipped,
    OddFlipped
}

public readonly record struct StripVertex(Int32 Index, Boolean Draws);

/// <summary>
/// Vertexes of one batch the VU program draws as triangle strips
/// </summary>
public class StripBatch
{
    public List<StripVertex> Vertexes { get; set; } = [];
    /// <summary>
    /// Scale a blend skin's batch packs the offsets of its shapes with
    /// </summary>
    public Twinsanity.TwinsanityInterchange.Common.Vector3? BlendShape { get; set; }
    /// <summary>
    /// Joints the strip's vertexes can use, the Xbox version's skins keep a palette of them for every strip
    /// </summary>
    public List<Int32>? Joints { get; set; }

    public StripBatch Clone()
    {
        return new StripBatch
        {
            Vertexes = [..Vertexes],
            BlendShape = BlendShape == null ? null : new Twinsanity.TwinsanityInterchange.Common.Vector3(BlendShape.X, BlendShape.Y, BlendShape.Z),
            Joints = Joints == null ? null : [..Joints]
        };
    }
}

/// <summary>
/// How a part of a model is packed into the game's triangle strips. Vertexes that don't draw the triangle they end are the
/// first two of every strip and the swaps that turn a strip around
/// </summary>
public class StripLayout
{
    public List<StripBatch> Batches { get; set; } = [];
    public TwinVifPadding Padding { get; set; }
    /// <summary>
    /// Every batch is one strip of the Xbox version, whose tools joined strips without keeping their triangles facing one way. Faces
    /// read from them are turned by their normals and only which triangles they draw has to match
    /// </summary>
    public Boolean IgnoresFacing { get; set; }

    public Int32 VertexCount => Batches.Sum(b => b.Vertexes.Count);

    /// <summary>
    /// The drawn triangles in the order the game draws them, facing the way their normals point
    /// </summary>
    public List<IndexedFace> GetFaces(StripWinding winding)
    {
        return GetFaces(winding, false);
    }

    private List<IndexedFace> GetFaces(StripWinding winding, Boolean skinsCountFromBatch)
    {
        var faces = new List<IndexedFace>();
        foreach (var batch in Batches)
        {
            var vertexes = batch.Vertexes;
            var stripStart = 0;
            for (var k = 2; k < vertexes.Count; k++)
            {
                if (!vertexes[k].Draws)
                {
                    continue;
                }

                stripStart = GetStripStart(vertexes, k, stripStart);
                var flipped = IsFlipped(k, skinsCountFromBatch ? 0 : stripStart, winding);
                var first = vertexes[flipped ? k - 1 : k - 2].Index;
                var second = vertexes[flipped ? k - 2 : k - 1].Index;
                faces.Add(new IndexedFace(first, second, vertexes[k].Index));
            }
        }

        return faces;
    }

    /// <summary>
    /// Whether the triangle ending at the batch's vertex is drawn with its first two vertexes swapped
    /// </summary>
    /// <param name="position">The vertex in the batch</param>
    /// <param name="stripStart">Where the vertex's strip starts in the batch</param>
    public static Boolean IsFlipped(Int32 position, Int32 stripStart, StripWinding winding)
    {
        return winding == StripWinding.EvenFlipped ? position % 2 == 0 : (position - stripStart) % 2 == 1;
    }

    /// <summary>
    /// Where the strip of a vertex that draws starts, given where the strip before it started. A lone vertex that doesn't draw joins
    /// a strip onto the last one and keeps its start
    /// </summary>
    public static Int32 GetStripStart(IReadOnlyList<StripVertex> vertexes, Int32 position, Int32 previousStart)
    {
        return position >= 2 && !vertexes[position - 1].Draws && !vertexes[position - 2].Draws ? position - 2 : previousStart;
    }

    /// <summary>
    /// Whether the strips draw exactly the given triangles facing the same way. Triangles without an area are ignored since they draw nothing
    /// </summary>
    public Boolean Draws(IReadOnlyList<IndexedFace> faces, Int32 vertexCount, StripWinding winding)
    {
        return Draws(faces, vertexCount, GetFaces(winding));
    }

    /// <summary>
    /// Whether the strips draw the triangles facing the way skins were read before their strips' own start was counted from
    /// </summary>
    public Boolean DrawsWithOldSkinWinding(IReadOnlyList<IndexedFace> faces, Int32 vertexCount)
    {
        return Draws(faces, vertexCount, GetFaces(StripWinding.OddFlipped, true));
    }

    private Boolean Draws(IReadOnlyList<IndexedFace> faces, Int32 vertexCount, List<IndexedFace> drawn)
    {
        if (Batches.Any(b => (!IgnoresFacing && b.Vertexes.Count > TwinVIFCompiler.MaxBatchVertexes) || b.Vertexes.Any(v => v.Index < 0 || v.Index >= vertexCount)))
        {
            return false;
        }

        var expected = new Dictionary<(Int32, Int32, Int32), Int32>();
        foreach (var face in faces)
        {
            var key = Normalize(face);
            if (key == null)
            {
                continue;
            }

            expected[key.Value] = expected.GetValueOrDefault(key.Value) + 1;
        }

        foreach (var face in drawn)
        {
            var key = Normalize(face);
            if (key == null)
            {
                continue;
            }

            if (!expected.TryGetValue(key.Value, out var count) || count == 0)
            {
                return false;
            }

            expected[key.Value] = count - 1;
        }

        return expected.Values.All(count => count == 0);
    }

    // Rotates the triangle so its smallest index is first, which keeps the facing. Strips that don't keep it sort the indexes
    private (Int32, Int32, Int32)? Normalize(IndexedFace face)
    {
        var (a, b, c) = (face.Indexes![0], face.Indexes[1], face.Indexes[2]);
        if (a == b || b == c || a == c)
        {
            return null;
        }

        if (IgnoresFacing)
        {
            var sorted = new[] { a, b, c };
            Array.Sort(sorted);
            return (sorted[0], sorted[1], sorted[2]);
        }

        if (a < b && a < c)
        {
            return (a, b, c);
        }

        return b < c ? (b, c, a) : (c, a, b);
    }

    /// <summary>
    /// Builds the layout of strips read from the game, merging the strip vertexes that are the same into one
    /// </summary>
    /// <param name="groupSizes">Amount of vertexes in each batch</param>
    /// <param name="draws">Whether the vertex at a strip position draws the triangle it ends</param>
    /// <param name="comparer">Tells whether the vertexes at two strip positions are the same</param>
    /// <param name="firstPositions">Strip position of every merged vertex's first use</param>
    public static StripLayout FromStrips(IReadOnlyList<Int32> groupSizes, Func<Int32, Boolean> draws, IEqualityComparer<Int32> comparer, out List<Int32> firstPositions)
    {
        var layout = new StripLayout();
        var indexes = new Dictionary<Int32, Int32>(comparer);
        firstPositions = [];
        var position = 0;
        foreach (var size in groupSizes)
        {
            var batch = new StripBatch();
            for (var i = 0; i < size; i++, position++)
            {
                if (!indexes.TryGetValue(position, out var index))
                {
                    index = firstPositions.Count;
                    indexes.Add(position, index);
                    firstPositions.Add(position);
                }

                batch.Vertexes.Add(new StripVertex(index, draws(position)));
            }

            layout.Batches.Add(batch);
        }

        return layout;
    }

    public StripLayout Clone()
    {
        return new StripLayout
        {
            Batches = Batches.Select(b => b.Clone()).ToList(),
            Padding = Padding,
            IgnoresFacing = IgnoresFacing
        };
    }

    /// <summary>
    /// The layout as flat lists: every batch's vertexes one after another, with the ones that don't draw stored as -(index + 1)
    /// </summary>
    public (Int32[] Vertexes, Int32[] BatchSizes) ToArrays()
    {
        var vertexes = Batches.SelectMany(b => b.Vertexes.Select(v => v.Draws ? v.Index : -(v.Index + 1))).ToArray();
        return (vertexes, Batches.Select(b => b.Vertexes.Count).ToArray());
    }

    public static StripLayout? FromArrays(IReadOnlyList<Int32> vertexes, IReadOnlyList<Int32> batchSizes, TwinVifPadding padding)
    {
        if (batchSizes.Any(s => s < 0) || batchSizes.Sum() != vertexes.Count)
        {
            return null;
        }

        var layout = new StripLayout { Padding = padding };
        var position = 0;
        foreach (var size in batchSizes)
        {
            var batch = new StripBatch();
            for (var i = 0; i < size; i++, position++)
            {
                var value = vertexes[position];
                batch.Vertexes.Add(value >= 0 ? new StripVertex(value, true) : new StripVertex(-value - 1, false));
            }

            layout.Batches.Add(batch);
        }

        return layout;
    }
}
