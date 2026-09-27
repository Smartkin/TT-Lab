using System;
using System.Collections.Generic;
using System.Linq;
using TT_Lab.AssetData.Graphics.SubModels;
using Twinsanity.PS2Hardware;

namespace TT_Lab.MeshProcessor;

/// <summary>
/// Packs triangles into the batches of triangle strips the game draws, keeping every triangle facing the way it faced
/// </summary>
/// <remarks>
/// A strip continues into the triangle across the edge made of its last two vertexes. Re-sending the older of the two without
/// drawing (a swap) costs a vertex and turns the strip the other way, which lets a strip wind around a vertex like a fan.
/// Strips only cross edges both triangles share in opposite directions, across any other edge a strip would draw the next triangle flipped.
/// Strips grow greedily from the triangles with the fewest free neighbours so few triangles get left alone, and a new strip starts
/// on a triangle using the last sent vertex when there is one, which skips one vertex instead of two.
/// A few variations of the greedy choices run and the one sending the fewest vertexes is kept
/// </remarks>
public static class Stripifier
{
    private readonly record struct Options(Boolean SwapToSaveLoneTriangles, Boolean JoinStrips);

    private static readonly Options[] Variations = [new(false, true), new(true, true), new(false, false)];

    public static StripLayout Stripify(IReadOnlyList<IndexedFace> faces, StripWinding winding, Int32 maxBatchVertexes = TwinVIFCompiler.MaxBatchVertexes)
    {
        StripLayout? best = null;
        foreach (var options in Variations)
        {
            var layout = Stripify(faces, winding, maxBatchVertexes, options);
            if (best == null || layout.VertexCount < best.VertexCount || layout.VertexCount == best.VertexCount && layout.Batches.Count < best.Batches.Count)
            {
                best = layout;
            }
        }

        return best!;
    }

    private static StripLayout Stripify(IReadOnlyList<IndexedFace> faces, StripWinding winding, Int32 maxBatchVertexes, Options options)
    {
        var mesh = new TriangleMesh(faces);
        var layout = new StripLayout();
        var batch = new StripBatch();
        // A joined strip continues the one it's joined onto, skins count which of their triangles are flipped from its start
        var stripStart = 0;
        while (mesh.TryGetSeed(out var seed))
        {
            var capacity = maxBatchVertexes - batch.Vertexes.Count;
            if (capacity < 3)
            {
                layout.Batches.Add(batch);
                batch = new StripBatch();
                capacity = maxBatchVertexes;
            }

            Strip? strip = null;
            if (options.JoinStrips && batch.Vertexes.Count >= 2)
            {
                strip = BuildJoinedStrip(mesh, batch.Vertexes[^1].Index, batch.Vertexes.Count, stripStart, capacity, winding, options);
            }

            if (strip == null)
            {
                stripStart = batch.Vertexes.Count;
                strip = BuildBestStrip(mesh, seed, stripStart, capacity, winding, options);
            }
            batch.Vertexes.AddRange(strip.Vertexes);
            foreach (var triangle in strip.Triangles)
            {
                mesh.Take(triangle);
            }
        }

        if (batch.Vertexes.Count > 0)
        {
            layout.Batches.Add(batch);
        }

        return layout;
    }

    private sealed class Strip
    {
        public List<StripVertex> Vertexes { get; } = [];
        public List<Int32> Triangles { get; } = [];
    }

    private static Strip BuildBestStrip(TriangleMesh mesh, Int32 seed, Int32 position, Int32 capacity, StripWinding winding, Options options)
    {
        Strip? best = null;
        for (var exit = 0; exit < 3; exit++)
        {
            var strip = BuildStrip(mesh, seed, exit, position, capacity, winding, options);
            if (best == null || strip.Triangles.Count > best.Triangles.Count ||
                strip.Triangles.Count == best.Triangles.Count && strip.Vertexes.Count < best.Vertexes.Count)
            {
                best = strip;
            }
        }

        return best!;
    }

    private static Strip BuildStrip(TriangleMesh mesh, Int32 seed, Int32 exitEdge, Int32 position, Int32 capacity, StripWinding winding, Options options)
    {
        var strip = new Strip();
        var taken = new HashSet<Int32> { seed };
        strip.Triangles.Add(seed);

        // The seed is sent so that the edge the strip continues over ends up as its last two vertexes
        var x = mesh.Vertex(seed, exitEdge + 2);
        var y = mesh.Vertex(seed, exitEdge);
        var z = mesh.Vertex(seed, exitEdge + 1);
        var flipped = StripLayout.IsFlipped(position + 2, position, winding);
        strip.Vertexes.Add(new StripVertex(x, false));
        strip.Vertexes.Add(new StripVertex(flipped ? z : y, false));
        strip.Vertexes.Add(new StripVertex(flipped ? y : z, true));
        var (older, newer) = flipped ? (z, y) : (y, z);
        Continue(mesh, strip, taken, seed, older, newer, capacity, options);
        return strip;
    }

    // The last sent vertex and a skipped one form the first triangle's edge, it's then sent so that its other edge continues the strip
    private static Strip? BuildJoinedStrip(TriangleMesh mesh, Int32 last, Int32 position, Int32 stripStart, Int32 capacity, StripWinding winding, Options options)
    {
        if (capacity < 2)
        {
            return null;
        }

        Strip? best = null;
        foreach (var triangle in mesh.FreeTrianglesWith(last))
        {
            var corner = 0;
            while (mesh.Vertex(triangle, corner) != last)
            {
                corner++;
            }

            var a = mesh.Vertex(triangle, corner + 1);
            var b = mesh.Vertex(triangle, corner + 2);
            var (older, newer) = StripLayout.IsFlipped(position + 1, stripStart, winding) ? (b, a) : (a, b);
            var strip = new Strip();
            var taken = new HashSet<Int32> { triangle };
            strip.Triangles.Add(triangle);
            strip.Vertexes.Add(new StripVertex(older, false));
            strip.Vertexes.Add(new StripVertex(newer, true));
            Continue(mesh, strip, taken, triangle, older, newer, capacity, options);
            if (best == null || strip.Triangles.Count > best.Triangles.Count)
            {
                best = strip;
            }
        }

        return best;
    }

    private static void Continue(TriangleMesh mesh, Strip strip, HashSet<Int32> taken, Int32 current, Int32 older, Int32 newer, Int32 capacity, Options options)
    {
        while (true)
        {
            var next = mesh.BestNeighbour(current, older, newer, taken);
            if (next < 0)
            {
                return;
            }

            var third = mesh.ThirdVertex(next, older, newer);
            var free = mesh.BestNeighbour(next, newer, third, taken);
            var swap = mesh.BestNeighbour(next, older, third, taken);
            var useSwap = swap >= 0 && (free < 0 ||
                options.SwapToSaveLoneTriangles && mesh.FreeNeighbours(swap, taken) <= 1 && mesh.FreeNeighbours(free, taken) > 1);
            if (strip.Vertexes.Count + (useSwap ? 2 : 1) > capacity)
            {
                return;
            }

            if (useSwap)
            {
                strip.Vertexes.Add(new StripVertex(older, false));
                newer = third;
            }
            else
            {
                (older, newer) = (newer, third);
            }

            strip.Vertexes.Add(new StripVertex(third, true));
            strip.Triangles.Add(next);
            taken.Add(next);
            current = next;
        }
    }

    private sealed class TriangleMesh
    {
        private readonly Int32[] _vertexes;
        // Triangles across each triangle's edges, edge i goes from corner i to corner i + 1
        private readonly List<Int32>[] _neighbours;
        private readonly Int32[][] _distinctNeighbours;
        private readonly Dictionary<Int32, List<Int32>> _trianglesByVertex = new();
        private readonly Boolean[] _taken;
        private readonly Int32[] _freeNeighbours;
        private readonly SortedSet<(Int32 Free, Int32 Triangle)> _seeds = new();

        public TriangleMesh(IReadOnlyList<IndexedFace> faces)
        {
            var triangles = faces.Where(f => f.Indexes![0] != f.Indexes[1] && f.Indexes[1] != f.Indexes[2] && f.Indexes[0] != f.Indexes[2]).ToList();
            _vertexes = new Int32[triangles.Count * 3];
            for (var i = 0; i < triangles.Count; i++)
            {
                _vertexes[i * 3] = triangles[i].Indexes![0];
                _vertexes[i * 3 + 1] = triangles[i].Indexes![1];
                _vertexes[i * 3 + 2] = triangles[i].Indexes![2];
            }

            var edges = new Dictionary<(Int32, Int32), List<Int32>>();
            for (var triangle = 0; triangle < triangles.Count; triangle++)
            {
                for (var corner = 0; corner < 3; corner++)
                {
                    GetOrAdd(edges, (Vertex(triangle, corner), Vertex(triangle, corner + 1))).Add(triangle);
                    GetOrAdd(_trianglesByVertex, Vertex(triangle, corner)).Add(triangle);
                }
            }

            _neighbours = new List<Int32>[triangles.Count * 3];
            _distinctNeighbours = new Int32[triangles.Count][];
            _taken = new Boolean[triangles.Count];
            _freeNeighbours = new Int32[triangles.Count];
            for (var triangle = 0; triangle < triangles.Count; triangle++)
            {
                for (var edge = 0; edge < 3; edge++)
                {
                    var reversed = (Vertex(triangle, edge + 1), Vertex(triangle, edge));
                    _neighbours[triangle * 3 + edge] = edges.TryGetValue(reversed, out var list) ? list.Where(t => t != triangle).ToList() : [];
                }

                _distinctNeighbours[triangle] = _neighbours[triangle * 3].Concat(_neighbours[triangle * 3 + 1]).Concat(_neighbours[triangle * 3 + 2]).Distinct().ToArray();
                _freeNeighbours[triangle] = _distinctNeighbours[triangle].Length;
                _seeds.Add((_freeNeighbours[triangle], triangle));
            }
        }

        private static List<Int32> GetOrAdd<TKey>(Dictionary<TKey, List<Int32>> dictionary, TKey key) where TKey : notnull
        {
            if (!dictionary.TryGetValue(key, out var list))
            {
                list = [];
                dictionary.Add(key, list);
            }

            return list;
        }

        public Int32 Vertex(Int32 triangle, Int32 corner)
        {
            return _vertexes[triangle * 3 + corner % 3];
        }

        public Int32 ThirdVertex(Int32 triangle, Int32 a, Int32 b)
        {
            for (var corner = 0; corner < 3; corner++)
            {
                var vertex = Vertex(triangle, corner);
                if (vertex != a && vertex != b)
                {
                    return vertex;
                }
            }

            return -1;
        }

        public IEnumerable<Int32> FreeTrianglesWith(Int32 vertex)
        {
            return _trianglesByVertex.TryGetValue(vertex, out var list) ? list.Where(t => !_taken[t]) : [];
        }

        public Boolean TryGetSeed(out Int32 seed)
        {
            seed = _seeds.Count > 0 ? _seeds.Min.Triangle : -1;
            return seed >= 0;
        }

        /// <summary>
        /// The free triangle across the edge between the two vertexes that has the fewest free neighbours of its own, or -1
        /// </summary>
        public Int32 BestNeighbour(Int32 triangle, Int32 a, Int32 b, HashSet<Int32> taken)
        {
            var best = -1;
            var bestFree = Int32.MaxValue;
            for (var edge = 0; edge < 3; edge++)
            {
                var from = Vertex(triangle, edge);
                var to = Vertex(triangle, edge + 1);
                if (!(from == a && to == b || from == b && to == a))
                {
                    continue;
                }

                foreach (var neighbour in _neighbours[triangle * 3 + edge])
                {
                    if (_taken[neighbour] || taken.Contains(neighbour))
                    {
                        continue;
                    }

                    var free = FreeNeighbours(neighbour, taken);
                    if (free < bestFree)
                    {
                        best = neighbour;
                        bestFree = free;
                    }
                }
            }

            return best;
        }

        public Int32 FreeNeighbours(Int32 triangle, HashSet<Int32> taken)
        {
            return _distinctNeighbours[triangle].Count(n => !_taken[n] && !taken.Contains(n));
        }

        public void Take(Int32 triangle)
        {
            _taken[triangle] = true;
            _seeds.Remove((_freeNeighbours[triangle], triangle));
            foreach (var neighbour in _distinctNeighbours[triangle])
            {
                if (_taken[neighbour])
                {
                    continue;
                }

                _seeds.Remove((_freeNeighbours[neighbour], neighbour));
                _freeNeighbours[neighbour]--;
                _seeds.Add((_freeNeighbours[neighbour], neighbour));
            }
        }
    }
}
