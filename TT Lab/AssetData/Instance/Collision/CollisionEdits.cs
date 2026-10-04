using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using TT_Lab.Assets;

namespace TT_Lab.AssetData.Instance.Collision;

/// <summary>
/// Edits of a collision's triangles, each making a new <see cref="CollisionGeometry"/> out of the one before
/// </summary>
public static class CollisionEdits
{
    /// <summary>
    /// The collision without the triangles. Vertexes only they had go with them, the ones no triangle had (the tools' leftovers) stay
    /// </summary>
    public static CollisionGeometry Delete(CollisionGeometry geometry, IReadOnlyCollection<Int32> triangles)
    {
        var removed = triangles.ToHashSet();
        var kept = geometry.Triangles.Where((_, index) => !removed.Contains(index)).ToList();
        var stillUsed = kept.SelectMany(triangle => triangle.Corners).ToHashSet();
        var dropped = removed.Where(index => index >= 0 && index < geometry.Triangles.Count)
            .SelectMany(index => geometry.Triangles[index].Corners).Where(corner => !stillUsed.Contains(corner)).ToHashSet();
        var remap = new Int32[geometry.Vertexes.Count];
        var vertexes = new List<Vector4>(geometry.Vertexes.Count - dropped.Count);
        for (var vertex = 0; vertex < geometry.Vertexes.Count; vertex++)
        {
            if (dropped.Contains(vertex))
            {
                remap[vertex] = -1;
                continue;
            }

            remap[vertex] = vertexes.Count;
            vertexes.Add(geometry.Vertexes[vertex]);
        }

        return new CollisionGeometry(vertexes, kept.Select(t => t with { A = remap[t.A], B = remap[t.B], C = remap[t.C] }).ToArray());
    }

    /// <summary>
    /// Copies of the triangles where they are, with vertexes of their own, after the collision's. The copies' indexes come with it
    /// </summary>
    public static (CollisionGeometry Geometry, Int32[] Copies) Duplicate(CollisionGeometry geometry, IReadOnlyCollection<Int32> triangles)
    {
        var vertexes = geometry.Vertexes.ToList();
        var faces = geometry.Triangles.ToList();
        var copiedVertexes = new Dictionary<Int32, Int32>();
        var copies = new List<Int32>();
        Int32 CopyOf(Int32 vertex)
        {
            if (!copiedVertexes.TryGetValue(vertex, out var copy))
            {
                copy = vertexes.Count;
                vertexes.Add(geometry.Vertexes[vertex]);
                copiedVertexes.Add(vertex, copy);
            }

            return copy;
        }

        foreach (var index in triangles.Where(index => index >= 0 && index < geometry.Triangles.Count).Order())
        {
            var triangle = geometry.Triangles[index];
            copies.Add(faces.Count);
            faces.Add(triangle with { A = CopyOf(triangle.A), B = CopyOf(triangle.B), C = CopyOf(triangle.C) });
        }

        return (new CollisionGeometry(vertexes, faces), copies.ToArray());
    }

    public static CollisionGeometry SetSurface(CollisionGeometry geometry, IReadOnlyCollection<Int32> triangles, LabURI surface)
    {
        var set = triangles.ToHashSet();
        return new CollisionGeometry(geometry.Vertexes, geometry.Triangles.Select((triangle, index) => set.Contains(index) ? triangle with { Surface = surface } : triangle).ToArray());
    }

    /// <summary>
    /// Moves the corners of the triangles by the transform. Triangles sharing a corner with them stretch along, the collision stays closed
    /// </summary>
    public static CollisionGeometry Transform(CollisionGeometry geometry, IReadOnlyCollection<Int32> triangles, Matrix4x4 transform)
    {
        var corners = CornersOf(geometry, triangles).ToHashSet();
        var vertexes = geometry.Vertexes.Select((vertex, index) =>
        {
            if (!corners.Contains(index))
            {
                return vertex;
            }

            var moved = Vector3.Transform(new Vector3(vertex.X, vertex.Y, vertex.Z), transform);
            return new Vector4(moved, vertex.W);
        }).ToArray();
        return new CollisionGeometry(vertexes, geometry.Triangles);
    }

    /// <summary>
    /// The sources' triangles (counter-clockwise seen from outside) added on the surface the way the add-on adds them
    /// (<see cref="CollisionBuilder"/>): corners within the weld distance of the collision's vertexes join them, flat triangles and ones the
    /// collision has are left out, the rest turned around to the game's winding. The added triangles' indexes come with it
    /// </summary>
    public static (CollisionGeometry Geometry, Int32[] Added, CollisionBuilder.Result Result) Add(CollisionGeometry geometry,
        IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> sources, LabURI surface, Single weld = CollisionBuilder.DefaultWeld, Boolean flip = true)
    {
        var positions = Enumerable.Range(0, geometry.Vertexes.Count).Select(geometry.Position).ToList();
        var result = CollisionBuilder.AddTriangles(positions, geometry.Triangles.Select(t => (t.A, t.B, t.C)), sources, weld, flip);
        return Added(geometry, result, surface);
    }

    /// <summary>
    /// The meshes' triangles (counter-clockwise seen from outside, a list for each mesh) made into collision as coarse as the game's
    /// (<see cref="CollisionBuilder.AddMeshes"/>: convex hulls where they stay close to the meshes, the rest made coarser, more where the
    /// player would touch too many triangles) and added on the surface. The added triangles' indexes come with it, nothing added leaves
    /// the collision as it was
    /// </summary>
    public static (CollisionGeometry Geometry, Int32[] Added, CollisionBuilder.Result Result) AddMeshes(CollisionGeometry geometry,
        IEnumerable<IEnumerable<(Vector3 A, Vector3 B, Vector3 C)>> meshes, LabURI surface, Double tolerance = CollisionBuilder.DefaultTolerance,
        Double hullDistance = CollisionBuilder.DefaultHullDistance)
    {
        var positions = Enumerable.Range(0, geometry.Vertexes.Count).Select(geometry.Position).ToList();
        var result = CollisionBuilder.AddMeshes(positions, geometry.Triangles.Select(t => (t.A, t.B, t.C)), meshes, tolerance: tolerance, hullDistance: hullDistance);
        return result.Triangles.Count == 0 ? (geometry, [], result) : Added(geometry, result, surface);
    }

    /// <summary>
    /// Where the player standing on the triangles would touch more of the collision's triangles than the game takes at a time
    /// </summary>
    public static CollisionBuilder.Crowding Crowding(CollisionGeometry geometry, IEnumerable<Int32> floors)
    {
        var positions = Enumerable.Range(0, geometry.Vertexes.Count).Select(geometry.Position).ToList();
        return CollisionBuilder.Crowd(positions, geometry.Triangles.Select(t => (t.A, t.B, t.C)).ToList(), floors);
    }

    private static (CollisionGeometry Geometry, Int32[] Added, CollisionBuilder.Result Result) Added(CollisionGeometry geometry, CollisionBuilder.Result result,
        LabURI surface)
    {
        var vertexes = geometry.Vertexes.Concat(result.Positions.Select(position => new Vector4(position, 1.0f))).ToArray();
        var triangles = geometry.Triangles.Concat(result.Triangles.Select(t => new CollisionFace(t.A, t.B, t.C, surface))).ToArray();
        var added = Enumerable.Range(geometry.Triangles.Count, result.Triangles.Count).ToArray();
        return (new CollisionGeometry(vertexes, triangles), added, result);
    }

    public static IEnumerable<Int32> CornersOf(CollisionGeometry geometry, IEnumerable<Int32> triangles)
    {
        return triangles.Where(index => index >= 0 && index < geometry.Triangles.Count).SelectMany(index => geometry.Triangles[index].Corners).Distinct();
    }

    /// <summary>
    /// The box around the triangles' corners, none without any
    /// </summary>
    public static (Vector3 Min, Vector3 Max)? Bounds(CollisionGeometry geometry, IEnumerable<Int32> triangles)
    {
        (Vector3 Min, Vector3 Max)? bounds = null;
        foreach (var corner in CornersOf(geometry, triangles))
        {
            var position = geometry.Position(corner);
            bounds = bounds == null ? (position, position) : (Vector3.Min(bounds.Value.Min, position), Vector3.Max(bounds.Value.Max, position));
        }

        return bounds;
    }

    /// <summary>
    /// The triangle the ray hits first and where, from both sides
    /// </summary>
    public static Int32? Pick(CollisionGeometry geometry, Vector3 origin, Vector3 direction, out Single distance)
    {
        Int32? picked = null;
        distance = Single.MaxValue;
        for (var index = 0; index < geometry.Triangles.Count; index++)
        {
            var (a, b, c) = geometry.Corners(index);
            if (RayTriangle(origin, direction, a, b, c) is { } hit && hit < distance)
            {
                distance = hit;
                picked = index;
            }
        }

        return picked;
    }

    /// <summary>
    /// The distance along the ray to the triangle (Möller-Trumbore, either side), none when it misses
    /// </summary>
    public static Single? RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        const Single epsilon = 1e-9f;
        var ab = b - a;
        var ac = c - a;
        var p = Vector3.Cross(direction, ac);
        var determinant = Vector3.Dot(ab, p);
        if (MathF.Abs(determinant) < epsilon)
        {
            return null;
        }

        var inverse = 1.0f / determinant;
        var toOrigin = origin - a;
        var u = Vector3.Dot(toOrigin, p) * inverse;
        if (u is < 0.0f or > 1.0f)
        {
            return null;
        }

        var q = Vector3.Cross(toOrigin, ab);
        var v = Vector3.Dot(direction, q) * inverse;
        if (v < 0.0f || u + v > 1.0f)
        {
            return null;
        }

        var distance = Vector3.Dot(ac, q) * inverse;
        return distance > 0.0f ? distance : null;
    }

    /// <summary>
    /// The corner of the collision closest to the point within the distance, none when there's none that close
    /// </summary>
    public static Int32? NearestCorner(CollisionGeometry geometry, Vector3 point, Single within)
    {
        Int32? nearest = null;
        var best = within * within;
        foreach (var corner in geometry.Triangles.SelectMany(triangle => triangle.Corners).Distinct())
        {
            var distance = Vector3.DistanceSquared(geometry.Position(corner), point);
            if (distance <= best)
            {
                best = distance;
                nearest = corner;
            }
        }

        return nearest;
    }
}
