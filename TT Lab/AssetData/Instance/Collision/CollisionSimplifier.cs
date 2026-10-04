using System;
using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// Collapses edges of triangles, cheapest first, while every corner stays within the tolerance of the planes of the triangles it took
/// the place of (Ronfard and Rossignac's measure, the collapses in the order of Garland and Heckbert's quadrics), a vertex's own or
/// the one all vertexes have, and within the floor tolerance of floors. The first fixed vertexes (the collision's own) don't move. The
/// add-on's <c>collision_builder._Simplifier</c>
/// </summary>
internal sealed class CollisionSimplifier
{
    // A plane whose normal's Y is this or more is a floor (Y is up in the game): the player stands on them
    public const Double Floor = 0.7;

    private readonly List<CollisionVector> _points;
    private readonly (Int32 A, Int32 B, Int32 C)?[] _faces;
    private readonly Int32 _fixed;
    private readonly Double[] _tolerances;
    private readonly Double[] _floorTolerances;
    private readonly HashSet<Int32>[] _vertexFaces;
    private readonly Double[][] _quadrics;
    // Planes by a rounded key, most of a flat area's triangles are one plane
    private readonly Dictionary<(Int64, Int64, Int64, Int64), (Double A, Double B, Double C, Double D, Boolean IsFloor)>[] _planes;
    private readonly Int32[] _versions;
    private readonly PriorityQueue<Entry, (Double Cost, Double Length, Int32 U, Int32 V)> _heap = new();

    private sealed record Entry(Int32 U, Int32 V, Int32 VersionU, Int32 VersionV, List<(Double Cost, CollisionVector Place)> Candidates);

    private CollisionSimplifier(IReadOnlyList<CollisionVector> positions, IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, Int32 fixedCount,
        IReadOnlyList<Double> tolerances, IReadOnlyList<Double>? floorTolerances)
    {
        _points = positions.ToList();
        _faces = triangles.Select(triangle => ((Int32, Int32, Int32)?)triangle).ToArray();
        _fixed = fixedCount;
        _tolerances = tolerances.ToArray();
        _floorTolerances = floorTolerances?.ToArray() ?? Enumerable.Repeat(Double.PositiveInfinity, _points.Count).ToArray();
        var count = _points.Count;
        _vertexFaces = Enumerable.Range(0, count).Select(_ => new HashSet<Int32>()).ToArray();
        _quadrics = Enumerable.Range(0, count).Select(_ => new Double[11]).ToArray();
        _planes = Enumerable.Range(0, count).Select(_ => new Dictionary<(Int64, Int64, Int64, Int64), (Double, Double, Double, Double, Boolean)>()).ToArray();
        _versions = new Int32[count];
        var edges = new Dictionary<(Int32, Int32), List<Int32>>();
        for (var index = 0; index < _faces.Length; index++)
        {
            var (a, b, c) = _faces[index]!.Value;
            foreach (var vertex in new[] { a, b, c })
            {
                _vertexFaces[vertex].Add(index);
            }

            var normal = Normal(index);
            var plane = CollisionVector.PlaneThrough(normal, _points[a]);
            var area = 0.5 * normal.Length;
            foreach (var vertex in new[] { a, b, c })
            {
                AddPlane(vertex, plane, area);
            }

            foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
            {
                var key = p < q ? (p, q) : (q, p);
                if (!edges.TryGetValue(key, out var faces))
                {
                    faces = [];
                    edges.Add(key, faces);
                }

                faces.Add(index);
            }
        }

        // A plane through a boundary edge standing on its triangle keeps the edge where it is
        foreach (var ((p, q), faces) in edges)
        {
            if (faces.Count != 1)
            {
                continue;
            }

            var edge = _points[q] - _points[p];
            var plane = CollisionVector.PlaneThrough(CollisionVector.Cross(edge, Normal(faces[0])), _points[p]);
            if (plane != null)
            {
                AddPlane(p, plane, edge.LengthSquared);
                AddPlane(q, plane, edge.LengthSquared);
            }
        }

        foreach (var (p, q) in edges.Keys)
        {
            Push(p, q);
        }
    }

    /// <summary>
    /// The triangles made coarser: their edges collapsed while every corner stays within the tolerance (each position's own) of the
    /// planes of the triangles it took the place of, of floors' planes within the floor tolerance (likewise) when it's smaller. The
    /// first fixed positions stay where and what they are, the others are numbered again after them and the ones no triangle uses any
    /// more are left out
    /// </summary>
    public static (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles) Simplify(IReadOnlyList<CollisionVector> positions,
        IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, IReadOnlyList<Double> tolerances, Int32 fixedCount = 0, IReadOnlyList<Double>? floorTolerances = null)
    {
        var simplifier = new CollisionSimplifier(positions, triangles, fixedCount, tolerances, floorTolerances);
        simplifier.Run();
        return Compact(simplifier._points, simplifier._faces, fixedCount);
    }

    public static (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles) Simplify(IReadOnlyList<CollisionVector> positions,
        IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, Double tolerance, Int32 fixedCount = 0, Double? floorTolerance = null)
    {
        return Simplify(positions, triangles, Enumerable.Repeat(tolerance, positions.Count).ToArray(), fixedCount,
            floorTolerance is { } floors ? Enumerable.Repeat(floors, positions.Count).ToArray() : null);
    }

    /// <summary>
    /// The first fixed positions as they are, the ones the triangles use after them in the order they're first used
    /// </summary>
    public static (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles) Compact(IReadOnlyList<CollisionVector> positions,
        IEnumerable<(Int32 A, Int32 B, Int32 C)?> triangles, Int32 fixedCount)
    {
        var renumbered = new Dictionary<Int32, Int32>();
        var keptPositions = positions.Take(fixedCount).ToList();
        var keptTriangles = new List<(Int32, Int32, Int32)>();
        foreach (var triangle in triangles)
        {
            if (triangle is not { } corners)
            {
                continue;
            }

            keptTriangles.Add((Renumber(corners.A), Renumber(corners.B), Renumber(corners.C)));
        }

        return (keptPositions, keptTriangles);

        Int32 Renumber(Int32 vertex)
        {
            if (vertex < fixedCount)
            {
                return vertex;
            }

            if (!renumbered.TryGetValue(vertex, out var index))
            {
                index = keptPositions.Count;
                renumbered.Add(vertex, index);
                keptPositions.Add(positions[vertex]);
            }

            return index;
        }
    }

    private CollisionVector Normal(Int32 face)
    {
        var (a, b, c) = _faces[face]!.Value;
        return CollisionVector.Cross(_points[b] - _points[a], _points[c] - _points[a]);
    }

    private void AddPlane(Int32 vertex, (Double A, Double B, Double C, Double D)? plane, Double weight)
    {
        if (plane is not { } found)
        {
            return;
        }

        var key = ((Int64)Math.Round(found.A * 1e4), (Int64)Math.Round(found.B * 1e4), (Int64)Math.Round(found.C * 1e4), (Int64)Math.Round(found.D * 1e3));
        _planes[vertex].TryAdd(key, (found.A, found.B, found.C, found.D, found.B >= Floor));
        var quadric = _quadrics[vertex];
        var (a, b, c, d) = found;
        quadric[0] += weight * a * a;
        quadric[1] += weight * a * b;
        quadric[2] += weight * a * c;
        quadric[3] += weight * a * d;
        quadric[4] += weight * b * b;
        quadric[5] += weight * b * c;
        quadric[6] += weight * b * d;
        quadric[7] += weight * c * c;
        quadric[8] += weight * c * d;
        quadric[9] += weight * d * d;
        quadric[10] += weight;
    }

    private static Double QuadricError(Double[] q, CollisionVector p)
    {
        var (x, y, z) = (p.X, p.Y, p.Z);
        return q[0] * x * x + 2.0 * q[1] * x * y + 2.0 * q[2] * x * z + 2.0 * q[3] * x + q[4] * y * y + 2.0 * q[5] * y * z + 2.0 * q[6] * y
               + q[7] * z * z + 2.0 * q[8] * z + q[9];
    }

    // Where the quadric is smallest, none when that's a line or a plane (fewer than three planes that aren't parallel)
    private static CollisionVector? QuadricMinimum(Double[] q)
    {
        var (a, b, c, e, f, i) = (q[0], q[1], q[2], q[4], q[5], q[7]);
        var det = a * (e * i - f * f) - b * (b * i - f * c) + c * (b * f - e * c);
        var trace = a + e + i;
        if (trace <= 0.0 || Math.Abs(det) <= 1e-9 * trace * trace * trace)
        {
            return null;
        }

        var (rx, ry, rz) = (-q[3], -q[6], -q[8]);
        var x = (rx * (e * i - f * f) - b * (ry * i - f * rz) + c * (ry * f - e * rz)) / det;
        var y = (a * (ry * i - f * rz) - rx * (b * i - f * c) + c * (b * rz - ry * c)) / det;
        var z = (a * (e * rz - ry * f) - b * (b * rz - ry * c) + rx * (b * f - e * c)) / det;
        return new CollisionVector(x, y, z);
    }

    private HashSet<Int32> Neighbours(Int32 vertex)
    {
        var found = new HashSet<Int32>();
        foreach (var face in _vertexFaces[vertex])
        {
            var (a, b, c) = _faces[face]!.Value;
            found.Add(a);
            found.Add(b);
            found.Add(c);
        }

        found.Remove(vertex);
        return found;
    }

    // Where the collapsed vertex can go with each place's mean squared distance from the planes, the best first
    private List<(Double Cost, CollisionVector Place)> Candidates(Int32 u, Int32 v)
    {
        var tolerance = Math.Min(_tolerances[u], _tolerances[v]);
        var quadric = new Double[11];
        for (var index = 0; index < quadric.Length; index++)
        {
            quadric[index] = _quadrics[u][index] + _quadrics[v][index];
        }

        List<CollisionVector> places;
        if (u < _fixed)
        {
            places = [_points[u]];
        }
        else if (v < _fixed)
        {
            places = [_points[v]];
        }
        else
        {
            var (pu, pv) = (_points[u], _points[v]);
            places = [pu, pv];
            var edge = pv - pu;
            // The best place along the edge
            var ax = quadric[0] * edge.X + quadric[1] * edge.Y + quadric[2] * edge.Z;
            var ay = quadric[1] * edge.X + quadric[4] * edge.Y + quadric[5] * edge.Z;
            var az = quadric[2] * edge.X + quadric[5] * edge.Y + quadric[7] * edge.Z;
            var alpha = ax * edge.X + ay * edge.Y + az * edge.Z;
            if (alpha > 1e-12)
            {
                var gx = quadric[0] * pu.X + quadric[1] * pu.Y + quadric[2] * pu.Z + quadric[3];
                var gy = quadric[1] * pu.X + quadric[4] * pu.Y + quadric[5] * pu.Z + quadric[6];
                var gz = quadric[2] * pu.X + quadric[5] * pu.Y + quadric[7] * pu.Z + quadric[8];
                var t = -(gx * edge.X + gy * edge.Y + gz * edge.Z) / alpha;
                if (t is > 0.0 and < 1.0)
                {
                    places.Add(pu + edge * t);
                }
            }

            // The best place of all, while it's near the edge: with planes almost parallel it runs away
            if (QuadricMinimum(quadric) is { } best)
            {
                var away = best - (pu + pv) * 0.5;
                if (away.LengthSquared <= Math.Max(edge.LengthSquared, tolerance * tolerance))
                {
                    places.Add(best);
                }
            }
        }

        var weight = quadric[10] > 0.0 ? quadric[10] : 1.0;
        return places.Select((place, order) => (Cost: Math.Max(QuadricError(quadric, place), 0.0) / weight, Order: order, Place: place))
            .OrderBy(scored => scored.Cost).ThenBy(scored => scored.Order).Select(scored => (scored.Cost, scored.Place)).ToList();
    }

    private void Push(Int32 u, Int32 v)
    {
        if (u < _fixed && v < _fixed)
        {
            return;
        }

        var candidates = Candidates(u, v);
        var length = (_points[u] - _points[v]).LengthSquared;
        _heap.Enqueue(new Entry(u, v, _versions[u], _versions[v], candidates), (candidates[0].Cost, length, u, v));
    }

    private Boolean IsClose(Int32 u, Int32 v, CollisionVector place, Double tolerance)
    {
        var floorTolerance = Math.Min(tolerance, Math.Min(_floorTolerances[u], _floorTolerances[v]));
        foreach (var planes in new[] { _planes[u], _planes[v] })
        {
            foreach (var (a, b, c, d, isFloor) in planes.Values)
            {
                if (Math.Abs(a * place.X + b * place.Y + c * place.Z + d) > (isFloor ? floorTolerance : tolerance))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private Boolean OnBoundary(Int32 vertex)
    {
        var counts = new Dictionary<Int32, Int32>();
        foreach (var face in _vertexFaces[vertex])
        {
            var (a, b, c) = _faces[face]!.Value;
            foreach (var other in new[] { a, b, c })
            {
                if (other != vertex)
                {
                    counts[other] = counts.GetValueOrDefault(other) + 1;
                }
            }
        }

        return counts.Values.Any(count => count == 1);
    }

    private Boolean CanCollapse(Int32 u, Int32 v, CollisionVector place)
    {
        var shared = _vertexFaces[u].Intersect(_vertexFaces[v]).ToHashSet();
        if (shared.Count is 0 or > 2)
        {
            return false;
        }

        // Their common neighbours have to be the corners across the edge, or the collapse pinches the surface
        var across = new HashSet<Int32>();
        foreach (var face in shared)
        {
            var (a, b, c) = _faces[face]!.Value;
            across.Add(a);
            across.Add(b);
            across.Add(c);
        }

        across.Remove(u);
        across.Remove(v);
        var common = Neighbours(u);
        common.IntersectWith(Neighbours(v));
        if (!common.SetEquals(across))
        {
            return false;
        }

        if (shared.Count == 2 && OnBoundary(u) && OnBoundary(v))
        {
            return false;
        }

        var made = new HashSet<(Int32, Int32, Int32)>();
        foreach (var face in _vertexFaces[u].Union(_vertexFaces[v]))
        {
            if (shared.Contains(face))
            {
                continue;
            }

            var (a, b, c) = _faces[face]!.Value;
            var corners = (A: a == v ? u : a, B: b == v ? u : b, C: c == v ? u : c);
            if (!made.Add(CollisionBuilder.SortedKey(corners)))
            {
                return false;
            }

            var pa = corners.A == u ? place : _points[corners.A];
            var pb = corners.B == u ? place : _points[corners.B];
            var pc = corners.C == u ? place : _points[corners.C];
            if (CollisionBuilder.IsFlat(pa, pb, pc))
            {
                return false;
            }

            // A triangle turning over folds the surface
            if (CollisionVector.Dot(CollisionVector.Cross(pb - pa, pc - pa), Normal(face)) <= 0.0)
            {
                return false;
            }
        }

        return true;
    }

    private void Collapse(Int32 u, Int32 v, CollisionVector place)
    {
        if (v < _fixed)
        {
            (u, v) = (v, u);
        }

        foreach (var face in _vertexFaces[u].Intersect(_vertexFaces[v]).ToList())
        {
            var (a, b, c) = _faces[face]!.Value;
            _vertexFaces[a].Remove(face);
            _vertexFaces[b].Remove(face);
            _vertexFaces[c].Remove(face);
            _faces[face] = null;
        }

        foreach (var face in _vertexFaces[v])
        {
            var (a, b, c) = _faces[face]!.Value;
            _faces[face] = (a == v ? u : a, b == v ? u : b, c == v ? u : c);
            _vertexFaces[u].Add(face);
        }

        _vertexFaces[v].Clear();
        _points[u] = place;
        _tolerances[u] = Math.Min(_tolerances[u], _tolerances[v]);
        _floorTolerances[u] = Math.Min(_floorTolerances[u], _floorTolerances[v]);
        for (var index = 0; index < 11; index++)
        {
            _quadrics[u][index] += _quadrics[v][index];
        }

        foreach (var (key, plane) in _planes[v])
        {
            _planes[u].TryAdd(key, plane);
        }

        _planes[v].Clear();
        _versions[u]++;
        _versions[v] = -1;
        foreach (var other in Neighbours(u))
        {
            Push(u, other);
        }
    }

    private void Run()
    {
        var most = _tolerances.Skip(_fixed).DefaultIfEmpty(0.0).Max();
        most *= most;
        while (_heap.TryDequeue(out var entry, out var priority))
        {
            if (priority.Cost > most)
            {
                break;
            }

            var (u, v) = (entry.U, entry.V);
            if (_versions[u] != entry.VersionU || _versions[v] != entry.VersionV)
            {
                continue;
            }

            var tolerance = Math.Min(_tolerances[u], _tolerances[v]);
            foreach (var (cost, place) in entry.Candidates)
            {
                if (cost > tolerance * tolerance)
                {
                    break;
                }

                if (IsClose(u, v, place, tolerance) && CanCollapse(u, v, place))
                {
                    Collapse(u, v, place);
                    break;
                }
            }
        }
    }
}
