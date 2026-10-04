using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TT_Lab.AssetData.Instance.Scenery;

public enum PlaceholderShape
{
    Cube,
    Pyramid,
    Cylinder,
    Cone,
    Sphere,
    Plane,
    Ramp
}

/// <summary>
/// A placeholder shape's mesh: its faces counter-clockwise seen from outside (the right-handed normal outwards, like the game's meshes and
/// Blender's), standing on the ground at the origin, its UVs in units so a texture repeats once a unit
/// </summary>
public sealed class PlaceholderMesh
{
    public List<Vector3> Positions { get; } = [];
    public List<Vector3> Normals { get; } = [];
    public List<Vector2> Uvs { get; } = [];
    public List<(Int32 A, Int32 B, Int32 C)> Faces { get; } = [];

    public IEnumerable<(Vector3 A, Vector3 B, Vector3 C)> Triangles(Vector3 offset = default)
    {
        return Faces.Select(face => (Positions[face.A] + offset, Positions[face.B] + offset, Positions[face.C] + offset));
    }

    internal Int32 AddVertex(Vector3 position, Vector3 normal, Vector2 uv)
    {
        Positions.Add(position);
        Normals.Add(normal);
        Uvs.Add(uv);
        return Positions.Count - 1;
    }

    // A flat polygon of corners counter-clockwise seen from outside, each with its UV
    internal void AddPolygon(params (Vector3 Position, Vector2 Uv)[] corners)
    {
        var normal = Vector3.Normalize(Vector3.Cross(corners[1].Position - corners[0].Position, corners[2].Position - corners[0].Position));
        var first = Positions.Count;
        foreach (var (position, uv) in corners)
        {
            AddVertex(position, normal, uv);
        }

        for (var corner = 2; corner < corners.Length; corner++)
        {
            Faces.Add((first, first + corner - 1, first + corner));
        }
    }
}

public static class PlaceholderShapes
{
    /// <summary>
    /// How big a new shape is, units across and up
    /// </summary>
    public const Single DefaultSize = 2.0f;

    private const Int32 Segments = 16;
    private const Int32 Rings = 8;

    public static PlaceholderMesh Make(PlaceholderShape shape, Single size = DefaultSize)
    {
        var mesh = new PlaceholderMesh();
        var half = size / 2.0f;
        switch (shape)
        {
            case PlaceholderShape.Cube:
                AddBox(mesh, half, size);
                break;
            case PlaceholderShape.Pyramid:
                AddPyramid(mesh, half, size);
                break;
            case PlaceholderShape.Cylinder:
                AddCylinder(mesh, half, size);
                break;
            case PlaceholderShape.Cone:
                AddCone(mesh, half, size);
                break;
            case PlaceholderShape.Sphere:
                AddSphere(mesh, half);
                break;
            case PlaceholderShape.Plane:
                AddFlat(mesh, half, 0.0f, true);
                break;
            case PlaceholderShape.Ramp:
                AddRamp(mesh, half, size);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
        }

        return mesh;
    }

    // A square across X and Z at the height, facing up or down
    private static void AddFlat(PlaceholderMesh mesh, Single half, Single height, Boolean up)
    {
        Vector3[] corners = up
            ? [new(-half, height, half), new(half, height, half), new(half, height, -half), new(-half, height, -half)]
            : [new(-half, height, -half), new(half, height, -half), new(half, height, half), new(-half, height, half)];
        mesh.AddPolygon(corners.Select(corner => (corner, new Vector2(corner.X + half, up ? half - corner.Z : corner.Z + half))).ToArray());
    }

    private static void AddBox(PlaceholderMesh mesh, Single half, Single height)
    {
        AddFlat(mesh, half, height, true);
        AddFlat(mesh, half, 0.0f, false);
        // The sides, each seen from outside with its bottom left corner first
        (Vector3 Start, Vector3 Along)[] sides =
        [
            (new Vector3(-half, 0, half), Vector3.UnitX),
            (new Vector3(half, 0, half), -Vector3.UnitZ),
            (new Vector3(half, 0, -half), -Vector3.UnitX),
            (new Vector3(-half, 0, -half), Vector3.UnitZ),
        ];
        var width = half * 2.0f;
        foreach (var (start, along) in sides)
        {
            var end = start + along * width;
            mesh.AddPolygon((start, Vector2.Zero), (end, new Vector2(width, 0)), (end + Vector3.UnitY * height, new Vector2(width, height)),
                (start + Vector3.UnitY * height, new Vector2(0, height)));
        }
    }

    private static void AddPyramid(PlaceholderMesh mesh, Single half, Single height)
    {
        AddFlat(mesh, half, 0.0f, false);
        var apex = new Vector3(0, height, 0);
        var width = half * 2.0f;
        var slant = MathF.Sqrt(half * half + height * height);
        (Vector3 Start, Vector3 Along)[] sides =
        [
            (new Vector3(-half, 0, half), Vector3.UnitX),
            (new Vector3(half, 0, half), -Vector3.UnitZ),
            (new Vector3(half, 0, -half), -Vector3.UnitX),
            (new Vector3(-half, 0, -half), Vector3.UnitZ),
        ];
        foreach (var (start, along) in sides)
        {
            mesh.AddPolygon((start, Vector2.Zero), (start + along * width, new Vector2(width, 0)), (apex, new Vector2(half, slant)));
        }
    }

    // Around the Y axis, going towards -Z from +X so the sides come out counter-clockwise seen from outside
    private static Vector3 Around(Single angle) => new(MathF.Cos(angle), 0, -MathF.Sin(angle));

    private static void AddCaps(PlaceholderMesh mesh, Single radius, Single bottom, Single? top)
    {
        foreach (var (height, up) in top == null ? [(bottom, false)] : new[] { (bottom, false), (top.Value, true) })
        {
            var normal = up ? Vector3.UnitY : -Vector3.UnitY;
            var center = mesh.AddVertex(new Vector3(0, height, 0), normal, new Vector2(radius, radius));
            var first = mesh.Positions.Count;
            for (var segment = 0; segment < Segments; segment++)
            {
                var position = Around(segment * MathF.Tau / Segments) * radius + new Vector3(0, height, 0);
                mesh.AddVertex(position, normal, new Vector2(position.X + radius, (up ? -position.Z : position.Z) + radius));
            }

            for (var segment = 0; segment < Segments; segment++)
            {
                var current = first + segment;
                var next = first + (segment + 1) % Segments;
                mesh.Faces.Add(up ? (center, current, next) : (center, next, current));
            }
        }
    }

    private static void AddCylinder(PlaceholderMesh mesh, Single radius, Single height)
    {
        var first = mesh.Positions.Count;
        for (var segment = 0; segment <= Segments; segment++)
        {
            var angle = segment * MathF.Tau / Segments;
            var around = Around(angle);
            mesh.AddVertex(around * radius, around, new Vector2(angle * radius, 0));
            mesh.AddVertex(around * radius + new Vector3(0, height, 0), around, new Vector2(angle * radius, height));
        }

        for (var segment = 0; segment < Segments; segment++)
        {
            var bottom = first + segment * 2;
            var nextBottom = bottom + 2;
            mesh.Faces.Add((bottom, nextBottom, nextBottom + 1));
            mesh.Faces.Add((bottom, nextBottom + 1, bottom + 1));
        }

        AddCaps(mesh, radius, 0.0f, height);
    }

    private static void AddCone(PlaceholderMesh mesh, Single radius, Single height)
    {
        var slant = MathF.Sqrt(radius * radius + height * height);
        Vector3 NormalAt(Single angle)
        {
            var around = Around(angle);
            return Vector3.Normalize(new Vector3(around.X * height, radius, around.Z * height));
        }

        for (var segment = 0; segment < Segments; segment++)
        {
            var angle = segment * MathF.Tau / Segments;
            var nextAngle = (segment + 1) * MathF.Tau / Segments;
            var a = mesh.AddVertex(Around(angle) * radius, NormalAt(angle), new Vector2(angle * radius, 0));
            var b = mesh.AddVertex(Around(nextAngle) * radius, NormalAt(nextAngle), new Vector2(nextAngle * radius, 0));
            var middle = (angle + nextAngle) / 2.0f;
            var apex = mesh.AddVertex(new Vector3(0, height, 0), NormalAt(middle), new Vector2(middle * radius, slant));
            mesh.Faces.Add((a, b, apex));
        }

        AddCaps(mesh, radius, 0.0f, null);
    }

    private static void AddSphere(PlaceholderMesh mesh, Single radius)
    {
        var center = new Vector3(0, radius, 0);
        var first = mesh.Positions.Count;
        for (var ring = 0; ring <= Rings; ring++)
        {
            var latitude = ring * MathF.PI / Rings;
            for (var segment = 0; segment <= Segments; segment++)
            {
                var longitude = segment * MathF.Tau / Segments;
                var normal = Around(longitude) * MathF.Sin(latitude) + Vector3.UnitY * MathF.Cos(latitude);
                mesh.AddVertex(center + normal * radius, normal, new Vector2(longitude * radius, latitude * radius));
            }
        }

        Int32 Index(Int32 ring, Int32 segment) => first + ring * (Segments + 1) + segment;
        for (var ring = 0; ring < Rings; ring++)
        {
            for (var segment = 0; segment < Segments; segment++)
            {
                // The poles' rows are one point, the triangles along them that would be lines are left out
                if (ring != Rings - 1)
                {
                    mesh.Faces.Add((Index(ring, segment), Index(ring + 1, segment), Index(ring + 1, segment + 1)));
                }

                if (ring != 0)
                {
                    mesh.Faces.Add((Index(ring, segment), Index(ring + 1, segment + 1), Index(ring, segment + 1)));
                }
            }
        }
    }

    // Its back side as tall as it's deep, sloping down to its front edge on the ground (+Z)
    private static void AddRamp(PlaceholderMesh mesh, Single half, Single height)
    {
        var width = half * 2.0f;
        AddFlat(mesh, half, 0.0f, false);
        mesh.AddPolygon((new Vector3(half, 0, -half), Vector2.Zero), (new Vector3(-half, 0, -half), new Vector2(width, 0)),
            (new Vector3(-half, height, -half), new Vector2(width, height)), (new Vector3(half, height, -half), new Vector2(0, height)));
        var slope = MathF.Sqrt(width * width + height * height);
        mesh.AddPolygon((new Vector3(-half, 0, half), Vector2.Zero), (new Vector3(half, 0, half), new Vector2(width, 0)),
            (new Vector3(half, height, -half), new Vector2(width, slope)), (new Vector3(-half, height, -half), new Vector2(0, slope)));
        mesh.AddPolygon((new Vector3(half, 0, half), Vector2.Zero), (new Vector3(half, 0, -half), new Vector2(width, 0)), (new Vector3(half, height, -half), new Vector2(width, height)));
        mesh.AddPolygon((new Vector3(-half, 0, -half), Vector2.Zero), (new Vector3(-half, 0, half), new Vector2(width, 0)), (new Vector3(-half, height, -half), new Vector2(0, height)));
    }
}
