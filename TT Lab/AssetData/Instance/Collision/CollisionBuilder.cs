using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// Collision made of the triangles of meshes, the add-on's <c>collision_builder.py</c>: corners closer than the weld distance become one
/// vertex (the collision's own vertexes stay as they are, new corners join them), triangles that come out flat are dropped and ones the
/// collision already has are skipped. The game winds most of its collision so a triangle's right-handed normal points into the solid
/// (a floor's down), the other way from the meshes drawn on it, so the triangles get turned around unless told otherwise.
/// <para>
/// The game's collision is much coarser than its meshes (a platform's mesh of 167 triangles stands on a box of 12), and it has to be:
/// the player only collides with <see cref="MostTriangles"/> triangles at a time (the decomp's GatherTriangleContacts). With more in the
/// box around Crash it cuts his motion down to a fifth and still keeps only the first 32 it finds, so on collision as fine as a mesh he
/// crawls and gets stuck. <see cref="AddMeshes"/> makes meshes into collision like the game's: convex hulls where they stay close to
/// the mesh, the rest made coarser by <see cref="CollisionSimplifier"/>
/// </para>
/// </summary>
public static class CollisionBuilder
{
    public const Single DefaultWeld = 1e-3f;

    /// <summary>
    /// How far, in the game's units, the collision may be from the meshes it's made of
    /// </summary>
    public const Double DefaultTolerance = 0.1;

    /// <summary>
    /// A mesh becomes its convex hull while every point of the hull is within this distance of it: gaps narrower than about twice it get
    /// filled, Crash is 1.2 units across
    /// </summary>
    public const Double DefaultHullDistance = 0.5;

    /// <summary>
    /// The most triangles the game collides the player with at a time
    /// </summary>
    public const Int32 MostTriangles = 32;

    // How much coarser a mesh is made, its tolerance times these, while the player would still touch more than Crowded triangles
    // somewhere on it: a margin below the game's MostTriangles for what's around it and for running faster. Its floors stay within the
    // tolerance until the last, and the hull distance stays: hulls filling wider gaps would close passages Crash fits through
    private static readonly Double[] Coarser = [1.0, 2.0, 4.0];
    private const Int32 Crowded = 24;

    // A part lies on another where the other's triangles face within about 25 degrees of its own
    private const Double SameWay = 0.9;

    // The box the game gathers them in around the player: Crash's hull's box (half his width to either side, his height up from his
    // feet) grown by 0.3 and a frame of running
    private const Double ReachSide = 0.62 + 0.3 + 0.15;
    private const Double ReachBelow = 0.3 + 0.15;
    private const Double ReachAbove = 1.95 + 0.3 + 0.15;

    // A triangle whose area is this share of its longest edge squared or less is a line
    private const Double Flat = 1e-6;

    /// <param name="Positions">The new vertexes, numbered after the collision's own</param>
    /// <param name="Triangles">The new triangles, by the collision's vertexes and the new ones</param>
    /// <param name="Skipped">Triangles the collision already had</param>
    /// <param name="Dropped">Triangles that were flat once their corners were welded</param>
    /// <param name="Sources">Triangles of the meshes that went in, before they were made coarser</param>
    /// <param name="Hulls">Meshes and parts of meshes made into their convex hulls</param>
    /// <param name="Coarsened">Meshes made coarser than the tolerance because the player would touch too many of their triangles</param>
    /// <param name="Covered">Parts of meshes left out because they lie on others (<see cref="DropCovered"/>)</param>
    public sealed record Result(List<Vector3> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles, Int32 Skipped, Int32 Dropped, Int32 Sources = 0,
        Int32 Hulls = 0, Int32 Coarsened = 0, Int32 Covered = 0);

    /// <param name="Places">Floor triangles where the player would touch the limit of triangles or more</param>
    /// <param name="Most">The most triangles the player would touch on any of them</param>
    /// <param name="Worst">Where on the floors the player would touch the most, none without floors</param>
    public readonly record struct Crowding(Int32 Places, Int32 Most, Vector3? Worst = null);

    /// <summary>
    /// The triangles of the sources (three corners each, counter-clockwise seen from outside) added to a collision of the positions and
    /// triangles as they are. A source triangle with the same three vertexes as one there, whichever way round, is skipped
    /// </summary>
    public static Result AddTriangles(IReadOnlyList<Vector3> positions, IEnumerable<(Int32 A, Int32 B, Int32 C)> triangles,
        IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> sources, Single weld = DefaultWeld, Boolean flip = true)
    {
        var welder = new Welder(weld, positions.Select(CollisionVector.Of));
        var counts = new Counts();
        var added = WeldSources(welder, triangles.Select(SortedKey).ToHashSet(), sources, counts);
        return new Result(welder.Positions.Skip(positions.Count).Select(position => position.ToSingle()).ToList(), flip ? added.Select(Flipped).ToList() : added,
            counts.Skipped, counts.Dropped, counts.Sources);
    }

    /// <summary>
    /// The meshes' triangles (three corners each, counter-clockwise seen from outside) made into collision like the game's and added to a
    /// collision of the positions and triangles. Parts of the meshes lying on others are left out (<see cref="DropCovered"/>), a mesh
    /// whose convex hull stays within the hull distance of it becomes the hull, other meshes' parts that aren't joined to each other the
    /// hulls of them that stay within it (<see cref="CollisionHulls.Shapes"/>), like a table's top and legs. The rest is made coarser
    /// within the tolerance, the meshes' triangles together so meshes that meet stay joined. A mesh on which the player would still touch
    /// more triangles than the game takes is made coarser again. Source triangles the collision has are skipped like
    /// <see cref="AddTriangles"/> skips them, the collision's own vertexes stay as they are
    /// </summary>
    public static Result AddMeshes(IReadOnlyList<Vector3> positions, IEnumerable<(Int32 A, Int32 B, Int32 C)> triangles,
        IEnumerable<IEnumerable<(Vector3 A, Vector3 B, Vector3 C)>> meshes, Single weld = DefaultWeld, Boolean flip = true, Double tolerance = DefaultTolerance,
        Double hullDistance = DefaultHullDistance, Boolean coarserWhereCrowded = true)
    {
        var welder = new Welder(weld, positions.Select(CollisionVector.Of));
        var own = triangles.ToList();
        var known = own.Select(SortedKey).ToHashSet();
        var existing = known.ToHashSet();
        var counts = new Counts();
        var rest = new List<(Int32, Int32, Int32)>();
        var tolerances = new Dictionary<Int32, Double>();
        var floorTolerances = new Dictionary<Int32, Double>();
        var hulls = new List<(List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles)>();
        var hullCount = 0;
        var coarsened = 0;
        var scales = coarserWhereCrowded && (tolerance > 0.0 || hullDistance > 0.0) ? Coarser : Coarser[..1];
        var weldedMeshes = meshes.Select(mesh => WeldSources(welder, known, mesh, counts)).ToList();
        var covered = 0;
        if (tolerance > 0.0 || hullDistance > 0.0)
        {
            // As close as a hull is made coarser: layers drawn over the ground a little apart (grass, water edges) are no floor of their own.
            // The collision's own triangles are wound like the game's, turned back to compare them with the meshes
            var covers = own.Select(triangle => flip ? (CollisionVector.Of(positions[triangle.A]), CollisionVector.Of(positions[triangle.C]), CollisionVector.Of(positions[triangle.B]))
                : (CollisionVector.Of(positions[triangle.A]), CollisionVector.Of(positions[triangle.B]), CollisionVector.Of(positions[triangle.C]))).ToList();
            (weldedMeshes, covered) = DropCovered(welder.Positions, weldedMeshes, Math.Max(tolerance, 0.5 * hullDistance), covers);
        }

        foreach (var welded in weldedMeshes)
        {
            if (welded.Count == 0)
            {
                continue;
            }

            var step = 0;
            Double meshTolerance;
            Double floorTolerance;
            List<(List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles)> meshHulls;
            List<(Int32 A, Int32 B, Int32 C)> meshRest;
            while (true)
            {
                meshTolerance = tolerance * scales[step];
                floorTolerance = step == Coarser.Length - 1 ? meshTolerance : tolerance;
                (meshHulls, meshRest) = MeshShapes(welder.Positions, welded, meshTolerance, hullDistance, floorTolerance);
                if (step == scales.Length - 1)
                {
                    break;
                }

                var alone = meshTolerance > 0.0
                    ? CollisionSimplifier.Simplify(welder.Positions, meshRest, meshTolerance, floorTolerance: floorTolerance)
                    : CollisionSimplifier.Compact(welder.Positions, meshRest.Select(triangle => ((Int32, Int32, Int32)?)triangle), 0);
                var (alonePositions, aloneTriangles) = WithHulls(alone.Positions, alone.Triangles, meshHulls, weld);
                // Made as the meshes are, wound like them: its floors face up
                if (Crowd(alonePositions, aloneTriangles, Enumerable.Range(0, aloneTriangles.Count), flipped: false, limit: Crowded + 1).Places == 0)
                {
                    break;
                }

                step++;
            }

            coarsened += step > 0 ? 1 : 0;
            hullCount += meshHulls.Count;
            hulls.AddRange(meshHulls);
            rest.AddRange(meshRest);
            foreach (var (a, b, c) in meshRest)
            {
                foreach (var vertex in new[] { a, b, c })
                {
                    tolerances[vertex] = Math.Min(tolerances.GetValueOrDefault(vertex, meshTolerance), meshTolerance);
                    floorTolerances[vertex] = Math.Min(floorTolerances.GetValueOrDefault(vertex, floorTolerance), floorTolerance);
                }
            }
        }

        var (made, added) = tolerance > 0.0 || coarsened > 0
            ? CollisionSimplifier.Simplify(welder.Positions, rest, Enumerable.Range(0, welder.Positions.Count).Select(index => tolerances.GetValueOrDefault(index, 0.0)).ToArray(),
                positions.Count, Enumerable.Range(0, welder.Positions.Count).Select(index => floorTolerances.GetValueOrDefault(index, 0.0)).ToArray())
            : CollisionSimplifier.Compact(welder.Positions, rest.Select(triangle => ((Int32, Int32, Int32)?)triangle), positions.Count);
        // Made coarser onto the collision's own vertexes, a triangle can come out as one the collision has
        var kept = added.Where(triangle => !existing.Contains(SortedKey(triangle))).ToList();
        counts.Skipped += added.Count - kept.Count;
        (made, added) = WithHulls(made, kept, hulls, weld);
        return new Result(made.Skip(positions.Count).Select(position => position.ToSingle()).ToList(), flip ? added.Select(Flipped).ToList() : added,
            counts.Skipped, counts.Dropped, counts.Sources, hullCount, coarsened, covered);
    }

    /// <summary>
    /// The meshes without their parts (triangles joined by their corners) that lie on other parts or on the covers (triangles wound like
    /// the meshes that stay, the collision's own): every point of them within the distance of triangles facing the same way, like the
    /// layers of grass and water the game's meshes draw just over the ground, or a mesh the collision was made of already. The smallest
    /// go first, of two alike one stays. Comes with how many parts were left out
    /// </summary>
    internal static (List<List<(Int32 A, Int32 B, Int32 C)>> Meshes, Int32 Covered) DropCovered(IReadOnlyList<CollisionVector> positions,
        IReadOnlyList<List<(Int32 A, Int32 B, Int32 C)>> meshes, Double distance, IReadOnlyList<(CollisionVector A, CollisionVector B, CollisionVector C)>? covers = null)
    {
        covers ??= [];
        var parts = new List<(Double Area, Int32 Index, Int32 Mesh, List<(Int32 A, Int32 B, Int32 C)> Triangles)>();
        for (var mesh = 0; mesh < meshes.Count; mesh++)
        {
            foreach (var part in CollisionHulls.Components(meshes[mesh]))
            {
                var area = part.Sum(triangle => 0.5 * CollisionVector.Cross(positions[triangle.B] - positions[triangle.A], positions[triangle.C] - positions[triangle.A]).Length);
                parts.Add((area, parts.Count, mesh, part));
            }
        }

        if (parts.Count == 0 || parts.Count + covers.Count < 2 || distance <= 0.0)
        {
            return (meshes.Select(triangles => triangles.ToList()).ToList(), 0);
        }

        var corners = new List<(CollisionVector A, CollisionVector B, CollisionVector C)>();
        var owners = new List<Int32>();
        var normals = new List<CollisionVector>();
        var starts = new Int32[parts.Count];
        foreach (var (_, index, _, part) in parts)
        {
            starts[index] = corners.Count;
            foreach (var (a, b, c) in part)
            {
                corners.Add((positions[a], positions[b], positions[c]));
                owners.Add(index);
                normals.Add(UnitNormal(positions[a], positions[b], positions[c]));
            }
        }

        foreach (var (a, b, c) in covers)
        {
            corners.Add((a, b, c));
            owners.Add(-1);
            normals.Add(UnitNormal(a, b, c));
        }

        var low = new CollisionVector(corners.Min(t => Math.Min(t.A.X, Math.Min(t.B.X, t.C.X))), corners.Min(t => Math.Min(t.A.Y, Math.Min(t.B.Y, t.C.Y))),
            corners.Min(t => Math.Min(t.A.Z, Math.Min(t.B.Z, t.C.Z))));
        var high = new CollisionVector(corners.Max(t => Math.Max(t.A.X, Math.Max(t.B.X, t.C.X))), corners.Max(t => Math.Max(t.A.Y, Math.Max(t.B.Y, t.C.Y))),
            corners.Max(t => Math.Max(t.A.Z, Math.Max(t.B.Z, t.C.Z))));
        var cell = Math.Max(1.0, Math.Max(high.X - low.X, Math.Max(high.Y - low.Y, high.Z - low.Z)) / 128.0);
        var grid = new Dictionary<(Int64, Int64, Int64), List<Int32>>();
        var reach = new CollisionVector(distance, distance, distance);
        for (var index = 0; index < corners.Count; index++)
        {
            var (a, b, c) = corners[index];
            var boxLow = new CollisionVector(Math.Min(a.X, Math.Min(b.X, c.X)), Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Min(a.Z, Math.Min(b.Z, c.Z))) - reach;
            var boxHigh = new CollisionVector(Math.Max(a.X, Math.Max(b.X, c.X)), Math.Max(a.Y, Math.Max(b.Y, c.Y)), Math.Max(a.Z, Math.Max(b.Z, c.Z))) + reach;
            var triangle = index;
            ForCells(boxLow, boxHigh, cell, key =>
            {
                if (!grid.TryGetValue(key, out var list))
                {
                    list = [];
                    grid.Add(key, list);
                }

                list.Add(triangle);
            });
        }

        var limit = distance * distance;
        var dropped = new HashSet<Int32>();

        Boolean LiesOnOthers(CollisionVector point, CollisionVector normal, Int32 part)
        {
            if (!grid.TryGetValue(((Int64)Math.Floor(point.X / cell), (Int64)Math.Floor(point.Y / cell), (Int64)Math.Floor(point.Z / cell)), out var list))
            {
                return false;
            }

            foreach (var index in list)
            {
                if (owners[index] == part || dropped.Contains(owners[index]) || CollisionVector.Dot(normals[index], normal) < SameWay)
                {
                    continue;
                }

                var (a, b, c) = corners[index];
                if ((point - CollisionHulls.ClosestOnTriangle(point, a, b, c)).LengthSquared <= limit)
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var (_, index, _, part) in parts.OrderBy(part => part.Area).ThenBy(part => part.Index))
        {
            var triangles = Enumerable.Range(starts[index], part.Count).Select(offset => (Corners: corners[offset], Normal: normals[offset])).ToList();
            // The middles first, a part that doesn't lie on others mostly shows it there
            if (!triangles.All(triangle => LiesOnOthers((triangle.Corners.A + triangle.Corners.B + triangle.Corners.C) * (1.0 / 3.0), triangle.Normal, index)))
            {
                continue;
            }

            var covered = triangles.All(triangle =>
            {
                var (a, b, c) = triangle.Corners;
                var longest = Math.Sqrt(Math.Max((b - a).LengthSquared, Math.Max((c - a).LengthSquared, (c - b).LengthSquared)));
                var steps = Math.Min(Math.Max((Int32)Math.Ceiling(longest / 0.5), 1), 16);
                for (var i = 0; i <= steps; i++)
                {
                    for (var j = 0; j <= steps - i; j++)
                    {
                        if (!LiesOnOthers(a + (b - a) * (i / (Double)steps) + (c - a) * (j / (Double)steps), triangle.Normal, index))
                        {
                            return false;
                        }
                    }
                }

                return true;
            });
            if (covered)
            {
                dropped.Add(index);
            }
        }

        var kept = meshes.Select(_ => new List<(Int32 A, Int32 B, Int32 C)>()).ToList();
        foreach (var (_, index, mesh, part) in parts)
        {
            if (!dropped.Contains(index))
            {
                kept[mesh].AddRange(part);
            }
        }

        return (kept, dropped.Count);
    }

    /// <summary>
    /// Where the player standing on the floors (triangles by their index, the ones facing up of them) would touch the limit of the
    /// collision's triangles or more, by default as many as the game takes at a time
    /// </summary>
    public static Crowding Crowd(IReadOnlyList<Vector3> positions, IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, IEnumerable<Int32> floors,
        Boolean flipped = true, Int32 limit = MostTriangles)
    {
        return Crowd(positions.Select(CollisionVector.Of).ToList(), triangles, floors, flipped, limit);
    }

    internal static Crowding Crowd(IReadOnlyList<CollisionVector> positions, IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, IEnumerable<Int32> floors,
        Boolean flipped, Int32 limit)
    {
        const Double cell = 2.0 * ReachSide;
        var grid = new Dictionary<(Int64, Int64, Int64), List<Int32>>();
        var boxes = new List<(CollisionVector Low, CollisionVector High)>(triangles.Count);
        foreach (var (a, b, c) in triangles)
        {
            var (pa, pb, pc) = (positions[a], positions[b], positions[c]);
            var low = new CollisionVector(Math.Min(pa.X, Math.Min(pb.X, pc.X)), Math.Min(pa.Y, Math.Min(pb.Y, pc.Y)), Math.Min(pa.Z, Math.Min(pb.Z, pc.Z)));
            var high = new CollisionVector(Math.Max(pa.X, Math.Max(pb.X, pc.X)), Math.Max(pa.Y, Math.Max(pb.Y, pc.Y)), Math.Max(pa.Z, Math.Max(pb.Z, pc.Z)));
            boxes.Add((low, high));
            ForCells(low, high, cell, key =>
            {
                if (!grid.TryGetValue(key, out var list))
                {
                    list = [];
                    grid.Add(key, list);
                }

                list.Add(boxes.Count - 1);
            });
        }

        var places = 0;
        var most = 0;
        Vector3? worst = null;
        foreach (var floor in floors)
        {
            var (a, b, c) = triangles[floor];
            var (pa, pb, pc) = (positions[a], positions[b], positions[c]);
            var normal = CollisionVector.Cross(pb - pa, pc - pa);
            var length = normal.Length;
            if (length == 0.0 || (flipped ? -normal.Y : normal.Y) < CollisionSimplifier.Floor * length)
            {
                continue;
            }

            var foot = (pa + pb + pc) * (1.0 / 3.0);
            var low = new CollisionVector(foot.X - ReachSide, foot.Y - ReachBelow, foot.Z - ReachSide);
            var high = new CollisionVector(foot.X + ReachSide, foot.Y + ReachAbove, foot.Z + ReachSide);
            var centre = (low + high) * 0.5;
            var half = (high - low) * 0.5;
            var seen = new HashSet<Int32>();
            ForCells(low, high, cell, key =>
            {
                if (grid.TryGetValue(key, out var list))
                {
                    seen.UnionWith(list);
                }
            });

            var touching = 0;
            foreach (var index in seen)
            {
                var (boxLow, boxHigh) = boxes[index];
                if (boxLow.X > high.X || boxLow.Y > high.Y || boxLow.Z > high.Z || boxHigh.X < low.X || boxHigh.Y < low.Y || boxHigh.Z < low.Z)
                {
                    continue;
                }

                var (ta, tb, tc) = triangles[index];
                if (TouchesBox(positions[ta], positions[tb], positions[tc], centre, half))
                {
                    touching++;
                }
            }

            if (worst == null || touching > most)
            {
                most = touching;
                worst = foot.ToSingle();
            }

            if (touching >= limit)
            {
                places++;
            }
        }

        return new Crowding(places, most, worst);
    }

    public static Boolean IsFlat(Vector3 a, Vector3 b, Vector3 c) => IsFlat(CollisionVector.Of(a), CollisionVector.Of(b), CollisionVector.Of(c));

    internal static Boolean IsFlat(CollisionVector a, CollisionVector b, CollisionVector c)
    {
        var ab = b - a;
        var ac = c - a;
        var bc = c - b;
        var area = CollisionVector.Cross(ab, ac).Length;
        var longest = Math.Max(ab.LengthSquared, Math.Max(ac.LengthSquared, bc.LengthSquared));
        return area <= Flat * longest || area == 0.0;
    }

    internal static (Int32, Int32, Int32) SortedKey((Int32 A, Int32 B, Int32 C) triangle)
    {
        Span<Int32> sorted = [triangle.A, triangle.B, triangle.C];
        sorted.Sort();
        return (sorted[0], sorted[1], sorted[2]);
    }

    private static (Int32, Int32, Int32) Flipped((Int32 A, Int32 B, Int32 C) triangle) => (triangle.A, triangle.C, triangle.B);

    private static CollisionVector UnitNormal(CollisionVector a, CollisionVector b, CollisionVector c)
    {
        var normal = CollisionVector.Cross(b - a, c - a);
        var length = normal.Length;
        return length > 0.0 ? normal * (1.0 / length) : default;
    }

    // The sources' triangles by the welder's vertexes, without the flat ones and the ones known
    private static List<(Int32 A, Int32 B, Int32 C)> WeldSources(Welder welder, HashSet<(Int32, Int32, Int32)> known, IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> sources,
        Counts counts)
    {
        var welded = new List<(Int32, Int32, Int32)>();
        foreach (var (a, b, c) in sources)
        {
            counts.Sources++;
            var before = welder.Count;
            var corners = (A: welder.IndexOf(CollisionVector.Of(a)), B: welder.IndexOf(CollisionVector.Of(b)), C: welder.IndexOf(CollisionVector.Of(c)));
            if (corners.A == corners.B || corners.B == corners.C || corners.A == corners.C ||
                IsFlat(welder.Positions[corners.A], welder.Positions[corners.B], welder.Positions[corners.C]))
            {
                welder.ForgetAfter(before);
                counts.Dropped++;
                continue;
            }

            if (!known.Add(SortedKey(corners)))
            {
                welder.ForgetAfter(before);
                counts.Skipped++;
                continue;
            }

            welded.Add(corners);
        }

        return welded;
    }

    // A mesh's hulls, made coarser like the rest gets made (a hull is already as far from the mesh as the hull distance, half of that
    // more is still close, but its floors), and the rest of its triangles as they are
    private static (List<(List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles)> Hulls, List<(Int32 A, Int32 B, Int32 C)> Remaining) MeshShapes(
        IReadOnlyList<CollisionVector> positions, IReadOnlyList<(Int32 A, Int32 B, Int32 C)> triangles, Double tolerance, Double hullDistance, Double floorTolerance)
    {
        if (hullDistance <= 0.0)
        {
            return ([], triangles.ToList());
        }

        var (hulls, rest) = CollisionHulls.Shapes(positions, triangles, hullDistance);
        return (hulls.Select(hull => CollisionHulls.Simplify(hull.Positions, hull.Triangles, Math.Max(tolerance, 0.5 * hullDistance), floorTolerance)).ToList(), rest);
    }

    // The hulls' triangles added to the triangles, their corners joining the vertexes within the weld distance
    private static (List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles) WithHulls(IReadOnlyList<CollisionVector> positions,
        List<(Int32 A, Int32 B, Int32 C)> triangles, IEnumerable<(List<CollisionVector> Positions, List<(Int32 A, Int32 B, Int32 C)> Triangles)> hulls, Single weld)
    {
        var joiner = new Welder(weld, positions);
        var known = triangles.Select(SortedKey).ToHashSet();
        foreach (var hull in hulls)
        {
            foreach (var (a, b, c) in hull.Triangles)
            {
                var corners = (A: joiner.IndexOf(hull.Positions[a]), B: joiner.IndexOf(hull.Positions[b]), C: joiner.IndexOf(hull.Positions[c]));
                if (corners.A == corners.B || corners.B == corners.C || corners.A == corners.C || !known.Add(SortedKey(corners)) ||
                    IsFlat(joiner.Positions[corners.A], joiner.Positions[corners.B], joiner.Positions[corners.C]))
                {
                    continue;
                }

                triangles.Add(corners);
            }
        }

        return (joiner.Positions, triangles);
    }

    // Whether the triangle and the box overlap, by the separating axes of Akenine-Moller's test
    private static Boolean TouchesBox(CollisionVector a, CollisionVector b, CollisionVector c, CollisionVector centre, CollisionVector half)
    {
        var (v0, v1, v2) = (a - centre, b - centre, c - centre);
        CollisionVector[] edges = [v1 - v0, v2 - v1, v0 - v2];
        var axes = new List<CollisionVector>(13) { new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), CollisionVector.Cross(edges[0], edges[1]) };
        foreach (var edge in edges)
        {
            axes.Add(new CollisionVector(0, -edge.Z, edge.Y));
            axes.Add(new CollisionVector(edge.Z, 0, -edge.X));
            axes.Add(new CollisionVector(-edge.Y, edge.X, 0));
        }

        foreach (var axis in axes)
        {
            var (p0, p1, p2) = (CollisionVector.Dot(v0, axis), CollisionVector.Dot(v1, axis), CollisionVector.Dot(v2, axis));
            var reach = half.X * Math.Abs(axis.X) + half.Y * Math.Abs(axis.Y) + half.Z * Math.Abs(axis.Z);
            if (Math.Min(p0, Math.Min(p1, p2)) > reach || Math.Max(p0, Math.Max(p1, p2)) < -reach)
            {
                return false;
            }
        }

        return true;
    }

    private static void ForCells(CollisionVector low, CollisionVector high, Double cell, Action<(Int64, Int64, Int64)> action)
    {
        for (var x = (Int64)Math.Floor(low.X / cell); x <= (Int64)Math.Floor(high.X / cell); x++)
        {
            for (var y = (Int64)Math.Floor(low.Y / cell); y <= (Int64)Math.Floor(high.Y / cell); y++)
            {
                for (var z = (Int64)Math.Floor(low.Z / cell); z <= (Int64)Math.Floor(high.Z / cell); z++)
                {
                    action((x, y, z));
                }
            }
        }
    }

    private sealed class Counts
    {
        public Int32 Sources;
        public Int32 Skipped;
        public Int32 Dropped;
    }

    // Vertexes in cells of the weld distance, a corner joins the nearest one within the distance in its cell and the ones around it
    private sealed class Welder
    {
        private readonly Double _distance;
        private readonly Double _cell;
        private readonly Dictionary<(Int64, Int64, Int64), List<Int32>> _cells = new();

        public Welder(Single distance, IEnumerable<CollisionVector> positions)
        {
            _distance = Math.Max(distance, 0.0);
            _cell = Math.Max(_distance, 1e-6);
            foreach (var position in positions)
            {
                Add(position);
            }
        }

        public List<CollisionVector> Positions { get; } = [];

        public Int32 Count => Positions.Count;

        private (Int64, Int64, Int64) KeyOf(CollisionVector position) =>
            ((Int64)Math.Floor(position.X / _cell), (Int64)Math.Floor(position.Y / _cell), (Int64)Math.Floor(position.Z / _cell));

        private Int32 Add(CollisionVector position)
        {
            var index = Positions.Count;
            Positions.Add(position);
            var key = KeyOf(position);
            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = [];
                _cells.Add(key, cell);
            }

            cell.Add(index);
            return index;
        }

        public Int32 IndexOf(CollisionVector position)
        {
            var (x, y, z) = KeyOf(position);
            var nearest = -1;
            var best = _distance * _distance;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        if (!_cells.TryGetValue((x + dx, y + dy, z + dz), out var cell))
                        {
                            continue;
                        }

                        foreach (var index in cell)
                        {
                            var distance = (position - Positions[index]).LengthSquared;
                            if (distance <= best)
                            {
                                nearest = index;
                                best = distance;
                            }
                        }
                    }
                }
            }

            return nearest >= 0 ? nearest : Add(position);
        }

        public void ForgetAfter(Int32 count)
        {
            for (var index = Positions.Count - 1; index >= count; index--)
            {
                _cells[KeyOf(Positions[index])].Remove(index);
            }

            Positions.RemoveRange(count, Positions.Count - count);
        }
    }
}

/// <summary>
/// A position of collision being made, in doubles like the add-on's Python
/// </summary>
internal readonly record struct CollisionVector(Double X, Double Y, Double Z)
{
    public static CollisionVector Of(Vector3 vector) => new(vector.X, vector.Y, vector.Z);

    public Vector3 ToSingle() => new((Single)X, (Single)Y, (Single)Z);

    public Double this[Int32 axis] => axis switch
    {
        0 => X,
        1 => Y,
        _ => Z,
    };

    public static CollisionVector operator +(CollisionVector a, CollisionVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static CollisionVector operator -(CollisionVector a, CollisionVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static CollisionVector operator *(CollisionVector a, Double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);

    public static CollisionVector Cross(CollisionVector a, CollisionVector b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public static Double Dot(CollisionVector a, CollisionVector b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public Double LengthSquared => X * X + Y * Y + Z * Z;

    public Double Length => Math.Sqrt(LengthSquared);

    /// <summary>
    /// The plane with the normal through the point (A, B, C the unit normal, D the offset), none for a normal of no length
    /// </summary>
    public static (Double A, Double B, Double C, Double D)? PlaneThrough(CollisionVector normal, CollisionVector point)
    {
        var length = normal.Length;
        if (length == 0.0)
        {
            return null;
        }

        var (x, y, z) = (normal.X / length, normal.Y / length, normal.Z / length);
        return (x, y, z, -(x * point.X + y * point.Y + z * point.Z));
    }
}
