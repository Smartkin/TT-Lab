using System;
using System.Collections.Generic;
using System.Linq;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// Convex hulls collision is made of where they stay close to the meshes, the add-on's <c>collision_builder.convex_hull</c> and
/// <c>hull_shapes</c>
/// </summary>
internal static class CollisionHulls
{
    /// <summary>
    /// The convex hull of the points (quickhull), its triangles counter-clockwise seen from outside, none when the points are within the
    /// flat distance of a plane
    /// </summary>
    public static (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles)? Build(IReadOnlyList<CollisionVector> points, Double flat = 1e-3)
    {
        if (points.Count < 4)
        {
            return null;
        }

        var low = new Double[3];
        var high = new Double[3];
        for (var axis = 0; axis < 3; axis++)
        {
            low[axis] = points.Min(point => point[axis]);
            high[axis] = points.Max(point => point[axis]);
        }

        var longest = Enumerable.Range(0, 3).MaxBy(axis => high[axis] - low[axis]);
        var epsilon = 1e-7 * Math.Max(Math.Max(low.Concat(high).Max(Math.Abs), high[longest] - low[longest]), 1.0);
        var first = FirstBest(points.Count, index => -points[index][longest]);
        var second = FirstBest(points.Count, index => points[index][longest]);
        var line = points[second] - points[first];
        var third = FirstBest(points.Count, index => CollisionVector.Cross(points[index] - points[first], line).LengthSquared);
        var normal = CollisionVector.Cross(line, points[third] - points[first]);
        var length = normal.Length;
        if (length == 0.0)
        {
            return null;
        }

        var fourth = FirstBest(points.Count, index => Math.Abs(CollisionVector.Dot(points[index] - points[first], normal)));
        if (Math.Abs(CollisionVector.Dot(points[fourth] - points[first], normal)) / length <= flat)
        {
            return null;
        }

        // Faces by the order they were made in, like the add-on's dictionary keeps them
        var faces = new SortedDictionary<Int32, HullFace>();
        var edges = new Dictionary<(Int32, Int32), Int32>();
        var pending = new List<Int32>();
        var made = 0;

        Int32 AddFace(Int32 a, Int32 b, Int32 c)
        {
            var plane = CollisionVector.PlaneThrough(CollisionVector.Cross(points[b] - points[a], points[c] - points[a]), points[a]) ?? (0.0, 0.0, 0.0, 0.0);
            var face = made++;
            faces.Add(face, new HullFace((a, b, c), plane, []));
            edges[(a, b)] = face;
            edges[(b, c)] = face;
            edges[(c, a)] = face;
            return face;
        }

        Double Height(Int32 face, Int32 index)
        {
            var (a, b, c, d) = faces[face].Plane;
            var point = points[index];
            return a * point.X + b * point.Y + c * point.Z + d;
        }

        void Assign(IEnumerable<Int32> indexes, IReadOnlyList<Int32> candidates)
        {
            foreach (var index in indexes)
            {
                foreach (var face in candidates)
                {
                    if (Height(face, index) > epsilon)
                    {
                        if (faces[face].Outside.Count == 0)
                        {
                            pending.Add(face);
                        }

                        faces[face].Outside.Add(index);
                        break;
                    }
                }
            }
        }

        Int32[] simplex = [first, second, third, fourth];
        var centre = (points[first] + points[second] + points[third] + points[fourth]) * 0.25;
        foreach (var (a, b, c) in new[] { (first, second, third), (first, second, fourth), (first, third, fourth), (second, third, fourth) })
        {
            if (CollisionVector.Dot(CollisionVector.Cross(points[b] - points[a], points[c] - points[a]), centre - points[a]) > 0.0)
            {
                AddFace(a, c, b);
            }
            else
            {
                AddFace(a, b, c);
            }
        }

        Assign(Enumerable.Range(0, points.Count).Where(index => !simplex.Contains(index)), faces.Keys.ToList());
        while (pending.Count > 0)
        {
            var face = pending[^1];
            pending.RemoveAt(pending.Count - 1);
            if (!faces.TryGetValue(face, out var current) || current.Outside.Count == 0)
            {
                continue;
            }

            var farthest = current.Outside[FirstBest(current.Outside.Count, index => Height(face, current.Outside[index]))];
            var visible = new HashSet<Int32>();
            var stack = new Stack<Int32>();
            stack.Push(face);
            while (stack.Count > 0)
            {
                var looked = stack.Pop();
                if (!visible.Add(looked))
                {
                    continue;
                }

                var (a, b, c) = faces[looked].Corners;
                foreach (var edge in new[] { (b, a), (c, b), (a, c) })
                {
                    if (edges.TryGetValue(edge, out var twin) && !visible.Contains(twin) && Height(twin, farthest) > epsilon)
                    {
                        stack.Push(twin);
                    }
                }
            }

            var horizon = new List<(Int32, Int32)>();
            var orphans = new List<Int32>();
            foreach (var looked in visible.Order())
            {
                var (a, b, c) = faces[looked].Corners;
                foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                {
                    if (!edges.TryGetValue((q, p), out var twin) || !visible.Contains(twin))
                    {
                        horizon.Add((p, q));
                    }
                }

                orphans.AddRange(faces[looked].Outside.Where(index => index != farthest));
            }

            foreach (var looked in visible)
            {
                var (a, b, c) = faces[looked].Corners;
                foreach (var edge in new[] { (a, b), (b, c), (c, a) })
                {
                    if (edges.TryGetValue(edge, out var owner) && owner == looked)
                    {
                        edges.Remove(edge);
                    }
                }

                faces.Remove(looked);
            }

            Assign(orphans, horizon.Select(edge => AddFace(edge.Item1, edge.Item2, farthest)).ToList());
        }

        return CollisionSimplifier.Compact(points, faces.Values.Select(face => ((Int32, Int32, Int32)?)face.Corners), 0);
    }

    /// <summary>
    /// A convex hull with the planes of its faces left out while it grows by no more than the tolerance past them (floors by the floor
    /// tolerance): bevels and rounded edges go, the planes that make the shape stay. It only grows, so a thin slab stays a slab, the
    /// add-on's <c>simplify_hull</c>
    /// </summary>
    public static (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles) Simplify(IReadOnlyList<CollisionVector> positions,
        IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, Double tolerance, Double? floorTolerance = null)
    {
        var floors = Math.Min(tolerance, floorTolerance ?? tolerance);
        var unique = new Dictionary<(Int64, Int64, Int64, Int64), Plane>();
        foreach (var (a, b, c) in triangles)
        {
            if (CollisionVector.PlaneThrough(CollisionVector.Cross(positions[b] - positions[a], positions[c] - positions[a]), positions[a]) is { } found)
            {
                var key = ((Int64)Math.Round(found.A * 1e6), (Int64)Math.Round(found.B * 1e6), (Int64)Math.Round(found.C * 1e6), (Int64)Math.Round(found.D * 1e5));
                unique.TryAdd(key, new Plane(found.A, found.B, found.C, found.D, found.B >= CollisionSimplifier.Floor));
            }
        }

        var planes = unique.Values.ToList();
        var used = triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).Distinct().ToList();
        var centre = used.Aggregate(new CollisionVector(0, 0, 0), (sum, vertex) => sum + positions[vertex]) * (1.0 / used.Count);
        var original = (positions.ToList(), triangles.ToList());
        while (true)
        {
            if (CornersOfPlanes(planes, centre) is not { } shape)
            {
                return original;
            }

            var growths = Enumerable.Range(0, planes.Count).Select(index => (Growth: Growth(planes, index, shape.Neighbours[index]), Index: index))
                .OrderBy(entry => entry.Growth).ThenBy(entry => entry.Index).ToList();
            // Planes no two of which are neighbours go at once: what grows past one is held by its neighbours, which stay
            var gone = new HashSet<Int32>();
            foreach (var (growth, index) in growths)
            {
                if (growth > (planes[index].IsFloor ? floors : tolerance))
                {
                    continue;
                }

                if (!shape.Neighbours[index].Overlaps(gone) && planes.Count - gone.Count > 4)
                {
                    gone.Add(index);
                }
            }

            if (gone.Count == 0)
            {
                break;
            }

            planes = planes.Where((_, index) => !gone.Contains(index)).ToList();
        }

        return CornersOfPlanes(planes, centre) is { } final && Build(final.Corners) is { } hull ? hull : original;
    }

    // The corners of the convex shape inside the planes (the centre inside all of them) and each plane's neighbours, through the hull of
    // the planes' dual points: a plane a distance h from the centre is the point of its normal over h
    private static (List<CollisionVector> Corners, HashSet<Int32>[] Neighbours)? CornersOfPlanes(IReadOnlyList<Plane> planes, CollisionVector centre)
    {
        var duals = new List<CollisionVector>(planes.Count);
        foreach (var plane in planes)
        {
            var height = -(plane.A * centre.X + plane.B * centre.Y + plane.C * centre.Z + plane.D);
            if (height <= 0.0)
            {
                return null;
            }

            duals.Add(new CollisionVector(plane.A / height, plane.B / height, plane.C / height));
        }

        if (Build(duals, 1e-12) is not { } hull)
        {
            return null;
        }

        var indexOf = new Dictionary<CollisionVector, Int32>();
        for (var index = 0; index < duals.Count; index++)
        {
            indexOf.TryAdd(duals[index], index);
        }

        var neighbours = planes.Select(_ => new HashSet<Int32>()).ToArray();
        var corners = new List<CollisionVector>();
        foreach (var (a, b, c) in hull.Triangles)
        {
            var (first, second, third) = (indexOf[hull.Positions[a]], indexOf[hull.Positions[b]], indexOf[hull.Positions[c]]);
            foreach (var (one, other) in new[] { (first, second), (second, third), (third, first) })
            {
                neighbours[one].Add(other);
                neighbours[other].Add(one);
            }

            var normal = CollisionVector.Cross(duals[second] - duals[first], duals[third] - duals[first]);
            var offset = CollisionVector.Dot(normal, duals[first]);
            if (offset <= 0.0)
            {
                return null;
            }

            corners.Add(centre + normal * (1.0 / offset));
        }

        return (corners, neighbours);
    }

    // How far past the removed plane the shape grows without it, as far as its neighbours let it: infinite when they let it go on
    private static Double Growth(IReadOnlyList<Plane> planes, Int32 removed, IEnumerable<Int32> neighbours)
    {
        var around = neighbours.Select(index => planes[index]).ToList();
        var plane = planes[removed];
        var most = 0.0;
        for (var first = 0; first < around.Count; first++)
        {
            for (var second = first + 1; second < around.Count; second++)
            {
                // A way out along two neighbours' edge the others don't stop
                var edge = CollisionVector.Cross(around[first].Normal, around[second].Normal);
                foreach (var way in new[] { edge, edge * -1.0 })
                {
                    if (CollisionVector.Dot(way, plane.Normal) > 1e-9 && around.All(other => CollisionVector.Dot(way, other.Normal) <= 1e-9))
                    {
                        return Double.PositiveInfinity;
                    }
                }

                for (var third = second + 1; third < around.Count; third++)
                {
                    if (Meet(around[first], around[second], around[third]) is not { } corner ||
                        around.Any(other => CollisionVector.Dot(other.Normal, corner) + other.D > 1e-7))
                    {
                        continue;
                    }

                    most = Math.Max(most, CollisionVector.Dot(plane.Normal, corner) + plane.D);
                }
            }
        }

        return most;
    }

    // Where three planes meet, none when two are parallel
    private static CollisionVector? Meet(Plane first, Plane second, Plane third)
    {
        var (a1, b1, c1, d1) = (first.A, first.B, first.C, first.D);
        var (a2, b2, c2, d2) = (second.A, second.B, second.C, second.D);
        var (a3, b3, c3, d3) = (third.A, third.B, third.C, third.D);
        var det = a1 * (b2 * c3 - c2 * b3) - b1 * (a2 * c3 - c2 * a3) + c1 * (a2 * b3 - b2 * a3);
        if (Math.Abs(det) < 1e-9)
        {
            return null;
        }

        var x = (-d1 * (b2 * c3 - c2 * b3) - b1 * (-d2 * c3 + c2 * d3) + c1 * (-d2 * b3 + b2 * d3)) / det;
        var y = (a1 * (-d2 * c3 + c2 * d3) + d1 * (a2 * c3 - c2 * a3) + c1 * (-a2 * d3 + d2 * a3)) / det;
        var z = (a1 * (-b2 * d3 + d2 * b3) - b1 * (-a2 * d3 + d2 * a3) - d1 * (a2 * b3 - b2 * a3)) / det;
        return new CollisionVector(x, y, z);
    }

    /// <summary>
    /// The convex hulls a mesh's triangles can be made of, and the triangles none fits: the whole mesh's hull when it stays within the
    /// distance of it, else the hulls of its parts that aren't joined to each other, parts near each other in one hull while it stays
    /// within the distance of them, the nearest first (a raft's planks become one)
    /// </summary>
    public static (List<(List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles)> Hulls, List<(Int32 A, Int32 B, Int32 C)> Remaining) Shapes(
        IReadOnlyList<CollisionVector> positions, IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, Double distance)
    {
        if (FittingHull(positions, triangles, distance) is { } whole)
        {
            return ([whole], []);
        }

        var parts = Components(triangles);
        if (parts.Count == 1)
        {
            return ([], triangles.ToList());
        }

        var rest = new List<(Int32, Int32, Int32)>();
        var clusters = new SortedDictionary<Int32, Cluster>();
        foreach (var part in parts)
        {
            if (FittingHull(positions, part, distance) is { } hull)
            {
                clusters.Add(clusters.Count, new Cluster(part, Box(positions, part), hull));
            }
            else
            {
                rest.AddRange(part);
            }
        }

        var pairs = new PriorityQueue<(Int32 First, Int32 Second), (Double Gap, Int32 First, Int32 Second)>();

        void PairWithTheRest(Int32 cluster)
        {
            foreach (var other in clusters.Keys)
            {
                if (other == cluster)
                {
                    continue;
                }

                var gap = Gap(clusters[cluster].Box, clusters[other].Box);
                if (gap <= 2.0 * distance)
                {
                    var (first, second) = (Math.Min(cluster, other), Math.Max(cluster, other));
                    pairs.Enqueue((first, second), (gap, first, second));
                }
            }
        }

        foreach (var cluster in clusters.Keys.ToList())
        {
            PairWithTheRest(cluster);
        }

        var next = clusters.Count;
        while (pairs.TryDequeue(out var pair, out _))
        {
            if (!clusters.TryGetValue(pair.First, out var first) || !clusters.TryGetValue(pair.Second, out var second))
            {
                continue;
            }

            var joined = first.Triangles.Concat(second.Triangles).ToList();
            if (FittingHull(positions, joined, distance) is not { } hull)
            {
                continue;
            }

            clusters.Remove(pair.First);
            clusters.Remove(pair.Second);
            clusters.Add(next, new Cluster(joined, Box(positions, joined), hull));
            PairWithTheRest(next);
            next++;
        }

        return (clusters.Values.Select(cluster => cluster.Hull).ToList(), rest);
    }

    private static (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles)? FittingHull(IReadOnlyList<CollisionVector> positions,
        IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, Double distance)
    {
        var used = triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).Distinct().Order().Select(vertex => positions[vertex]).ToList();
        return Build(used) is { } hull && Fits(hull, positions, triangles, distance) ? hull : null;
    }

    // Whether the hull's surface stays within the distance of the triangles: a hull over a hollow, a gap or between parts far apart
    // doesn't
    private static Boolean Fits((List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles) hull, IReadOnlyList<CollisionVector> positions,
        IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, Double distance)
    {
        var corners = triangles.Select(triangle => (A: positions[triangle.A], B: positions[triangle.B], C: positions[triangle.C])).ToList();
        var (low, high) = Box(corners.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }));
        // Cells of the distance, but not so many that a big mesh's triangles fill too many of them
        var cell = Math.Max(Math.Max(distance, Math.Max(high.X - low.X, Math.Max(high.Y - low.Y, high.Z - low.Z)) / 48.0), 1e-6);
        var grid = new Dictionary<(Int64, Int64, Int64), List<Int32>>();
        for (var index = 0; index < corners.Count; index++)
        {
            var (a, b, c) = corners[index];
            var (boxLow, boxHigh) = Box([a, b, c]);
            var (fromX, fromY, fromZ) = CellOf(boxLow - new CollisionVector(distance, distance, distance), cell);
            var (toX, toY, toZ) = CellOf(boxHigh + new CollisionVector(distance, distance, distance), cell);
            for (var x = fromX; x <= toX; x++)
            {
                for (var y = fromY; y <= toY; y++)
                {
                    for (var z = fromZ; z <= toZ; z++)
                    {
                        if (!grid.TryGetValue((x, y, z), out var list))
                        {
                            list = [];
                            grid.Add((x, y, z), list);
                        }

                        list.Add(index);
                    }
                }
            }
        }

        var limit = distance * distance;

        Boolean Near(CollisionVector point)
        {
            if (!grid.TryGetValue(CellOf(point, cell), out var list))
            {
                return false;
            }

            foreach (var index in list)
            {
                var (a, b, c) = corners[index];
                if ((point - ClosestOnTriangle(point, a, b, c)).LengthSquared <= limit)
                {
                    return true;
                }
            }

            return false;
        }

        var faces = hull.Triangles.Select(triangle => (A: hull.Positions[triangle.A], B: hull.Positions[triangle.B], C: hull.Positions[triangle.C])).ToList();
        // The middles first, a hull that doesn't fit mostly shows it there
        if (!faces.All(face => Near((face.A + face.B + face.C) * (1.0 / 3.0))))
        {
            return false;
        }

        var spacing = distance * 0.5;
        foreach (var (a, b, c) in faces)
        {
            var longest = Math.Sqrt(Math.Max((b - a).LengthSquared, Math.Max((c - a).LengthSquared, (c - b).LengthSquared)));
            var steps = Math.Min(Math.Max((Int32)Math.Ceiling(longest / spacing), 1), 48);
            for (var i = 0; i <= steps; i++)
            {
                for (var j = 0; j <= steps - i; j++)
                {
                    var (s, t) = (i / (Double)steps, j / (Double)steps);
                    if (!Near(a + (b - a) * s + (c - a) * t))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// The point of the triangle closest to p (Ericson's Real-Time Collision Detection, 5.1.5)
    /// </summary>
    public static CollisionVector ClosestOnTriangle(CollisionVector p, CollisionVector a, CollisionVector b, CollisionVector c)
    {
        var (ab, ac, ap) = (b - a, c - a, p - a);
        var (d1, d2) = (CollisionVector.Dot(ab, ap), CollisionVector.Dot(ac, ap));
        if (d1 <= 0.0 && d2 <= 0.0)
        {
            return a;
        }

        var bp = p - b;
        var (d3, d4) = (CollisionVector.Dot(ab, bp), CollisionVector.Dot(ac, bp));
        if (d3 >= 0.0 && d4 <= d3)
        {
            return b;
        }

        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
        {
            return a + ab * (d1 / (d1 - d3));
        }

        var cp = p - c;
        var (d5, d6) = (CollisionVector.Dot(ab, cp), CollisionVector.Dot(ac, cp));
        if (d6 >= 0.0 && d5 <= d6)
        {
            return c;
        }

        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
        {
            return a + ac * (d2 / (d2 - d6));
        }

        var va = d3 * d6 - d5 * d4;
        if (va <= 0.0 && d4 - d3 >= 0.0 && d5 - d6 >= 0.0)
        {
            return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        }

        var denominator = 1.0 / (va + vb + vc);
        return a + ab * (vb * denominator) + ac * (vc * denominator);
    }

    /// <summary>
    /// The triangles in groups joined by their corners
    /// </summary>
    public static List<List<(Int32 A, Int32 B, Int32 C)>> Components(IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles)
    {
        var parents = new Dictionary<Int32, Int32>();

        Int32 Root(Int32 vertex)
        {
            parents.TryAdd(vertex, vertex);
            while (parents[vertex] != vertex)
            {
                parents[vertex] = parents[parents[vertex]];
                vertex = parents[vertex];
            }

            return vertex;
        }

        foreach (var (a, b, c) in triangles)
        {
            foreach (var other in new[] { b, c })
            {
                var (first, second) = (Root(a), Root(other));
                if (first != second)
                {
                    parents[second] = first;
                }
            }
        }

        var groups = new Dictionary<Int32, List<(Int32, Int32, Int32)>>();
        var order = new List<Int32>();
        foreach (var triangle in triangles)
        {
            var root = Root(triangle.A);
            if (!groups.TryGetValue(root, out var group))
            {
                group = [];
                groups.Add(root, group);
                order.Add(root);
            }

            group.Add(triangle);
        }

        return order.Select(root => groups[root]).ToList();
    }

    private static (CollisionVector Low, CollisionVector High) Box(IReadOnlyList<CollisionVector> positions, IEnumerable<(Int32 A, Int32 B, Int32 C)> triangles)
    {
        return Box(triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).Distinct().Select(vertex => positions[vertex]));
    }

    private static (CollisionVector Low, CollisionVector High) Box(IEnumerable<CollisionVector> points)
    {
        var (low, high) = (new CollisionVector(Double.MaxValue, Double.MaxValue, Double.MaxValue), new CollisionVector(Double.MinValue, Double.MinValue, Double.MinValue));
        foreach (var point in points)
        {
            low = new CollisionVector(Math.Min(low.X, point.X), Math.Min(low.Y, point.Y), Math.Min(low.Z, point.Z));
            high = new CollisionVector(Math.Max(high.X, point.X), Math.Max(high.Y, point.Y), Math.Max(high.Z, point.Z));
        }

        return (low, high);
    }

    // How far apart two boxes are, 0 when they overlap
    private static Double Gap((CollisionVector Low, CollisionVector High) first, (CollisionVector Low, CollisionVector High) second)
    {
        var apart = new CollisionVector(Math.Max(Math.Max(first.Low.X - second.High.X, second.Low.X - first.High.X), 0.0),
            Math.Max(Math.Max(first.Low.Y - second.High.Y, second.Low.Y - first.High.Y), 0.0),
            Math.Max(Math.Max(first.Low.Z - second.High.Z, second.Low.Z - first.High.Z), 0.0));
        return apart.Length;
    }

    private static (Int64, Int64, Int64) CellOf(CollisionVector point, Double cell)
    {
        return ((Int64)Math.Floor(point.X / cell), (Int64)Math.Floor(point.Y / cell), (Int64)Math.Floor(point.Z / cell));
    }

    // The first index with the highest value
    private static Int32 FirstBest(Int32 count, Func<Int32, Double> value)
    {
        var best = 0;
        var bestValue = value(0);
        for (var index = 1; index < count; index++)
        {
            var current = value(index);
            if (current > bestValue)
            {
                best = index;
                bestValue = current;
            }
        }

        return best;
    }

    private sealed record HullFace((Int32 A, Int32 B, Int32 C) Corners, (Double A, Double B, Double C, Double D) Plane, List<Int32> Outside);

    private readonly record struct Plane(Double A, Double B, Double C, Double D, Boolean IsFloor)
    {
        public CollisionVector Normal => new(A, B, C);
    }

    private sealed record Cluster(List<(Int32 A, Int32 B, Int32 C)> Triangles, (CollisionVector Low, CollisionVector High) Box,
        (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles) Hull);
}
