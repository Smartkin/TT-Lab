using System;
using System.Collections.Generic;
using GlmSharp;

namespace TT_Lab.Rendering;

/// <summary>
/// Unit shapes used by the primitive renderer, all wound counter-clockwise when looked at from outside
/// </summary>
internal static class PrimitiveMeshes
{
    // Position and normal
    public const int VertexFloats = 6;

    private const int RoundSegments = 24;
    private const int SphereStacks = 12;

    public static (int First, int Count) AddBox(List<float> vertices)
    {
        var first = vertices.Count / VertexFloats;
        // Every face's U x V gives its normal, which keeps them all facing outwards
        AddBoxFace(vertices, vec3.UnitX, vec3.UnitY, vec3.UnitZ);
        AddBoxFace(vertices, -vec3.UnitX, vec3.UnitZ, vec3.UnitY);
        AddBoxFace(vertices, vec3.UnitY, vec3.UnitZ, vec3.UnitX);
        AddBoxFace(vertices, -vec3.UnitY, vec3.UnitX, vec3.UnitZ);
        AddBoxFace(vertices, vec3.UnitZ, vec3.UnitX, vec3.UnitY);
        AddBoxFace(vertices, -vec3.UnitZ, vec3.UnitY, vec3.UnitX);
        return (first, vertices.Count / VertexFloats - first);
    }

    public static (int First, int Count) AddSphere(List<float> vertices)
    {
        var first = vertices.Count / VertexFloats;
        for (var stack = 0; stack < SphereStacks; stack++)
        {
            for (var slice = 0; slice < RoundSegments; slice++)
            {
                var a = GetSpherePoint(stack, slice);
                var b = GetSpherePoint(stack + 1, slice);
                var c = GetSpherePoint(stack + 1, slice + 1);
                var d = GetSpherePoint(stack, slice + 1);
                AddTriangle(vertices, a, c, b, a, c, b);
                AddTriangle(vertices, a, d, c, a, d, c);
            }
        }

        return (first, vertices.Count / VertexFloats - first);
    }

    public static (int First, int Count) AddCone(List<float> vertices)
    {
        var first = vertices.Count / VertexFloats;
        var tip = vec3.UnitY;
        for (var segment = 0; segment < RoundSegments; segment++)
        {
            var start = GetRoundPoint(segment);
            var end = GetRoundPoint(segment + 1);
            var middle = GetRoundPoint(segment + 0.5f);
            // Height and radius of the cone are the same, which tilts the sides by 45 degrees
            var startNormal = (start + vec3.UnitY).Normalized;
            var endNormal = (end + vec3.UnitY).Normalized;
            var tipNormal = (middle + vec3.UnitY).Normalized;
            AddTriangle(vertices, start, tip, end, startNormal, tipNormal, endNormal);
            AddTriangle(vertices, vec3.Zero, start, end, -vec3.UnitY, -vec3.UnitY, -vec3.UnitY);
        }

        return (first, vertices.Count / VertexFloats - first);
    }

    public static (int First, int Count) AddCylinder(List<float> vertices)
    {
        var first = vertices.Count / VertexFloats;
        for (var segment = 0; segment < RoundSegments; segment++)
        {
            var bottomStart = GetRoundPoint(segment);
            var bottomEnd = GetRoundPoint(segment + 1);
            var topStart = bottomStart + vec3.UnitY;
            var topEnd = bottomEnd + vec3.UnitY;
            AddTriangle(vertices, bottomStart, topStart, bottomEnd, bottomStart, bottomStart, bottomEnd);
            AddTriangle(vertices, topStart, topEnd, bottomEnd, bottomStart, bottomEnd, bottomEnd);
            AddTriangle(vertices, vec3.UnitY, topEnd, topStart, vec3.UnitY, vec3.UnitY, vec3.UnitY);
            AddTriangle(vertices, vec3.Zero, bottomStart, bottomEnd, -vec3.UnitY, -vec3.UnitY, -vec3.UnitY);
        }

        return (first, vertices.Count / VertexFloats - first);
    }

    public static (int First, int Count) AddQuad(List<float> vertices)
    {
        var first = vertices.Count / VertexFloats;
        var a = new vec3(-1.0f, -1.0f, 0.0f);
        var b = new vec3(1.0f, -1.0f, 0.0f);
        var c = new vec3(1.0f, 1.0f, 0.0f);
        var d = new vec3(-1.0f, 1.0f, 0.0f);
        var front = vec3.UnitZ;
        var back = -vec3.UnitZ;
        AddTriangle(vertices, a, b, c, front, front, front);
        AddTriangle(vertices, a, c, d, front, front, front);
        AddTriangle(vertices, a, c, b, back, back, back);
        AddTriangle(vertices, a, d, c, back, back, back);
        return (first, vertices.Count / VertexFloats - first);
    }

    public static (int First, int Count) AddDisc(List<float> vertices)
    {
        var first = vertices.Count / VertexFloats;
        var front = vec3.UnitZ;
        var back = -vec3.UnitZ;
        for (var segment = 0; segment < RoundSegments; segment++)
        {
            var start = GetRoundPoint(segment);
            var end = GetRoundPoint(segment + 1);
            start = new vec3(start.x, start.z, 0.0f);
            end = new vec3(end.x, end.z, 0.0f);
            AddTriangle(vertices, vec3.Zero, start, end, front, front, front);
            AddTriangle(vertices, vec3.Zero, end, start, back, back, back);
        }

        return (first, vertices.Count / VertexFloats - first);
    }

    private static void AddBoxFace(List<float> vertices, vec3 normal, vec3 u, vec3 v)
    {
        var a = normal - u - v;
        var b = normal + u - v;
        var c = normal + u + v;
        var d = normal - u + v;
        AddTriangle(vertices, a, b, c, normal, normal, normal);
        AddTriangle(vertices, a, c, d, normal, normal, normal);
    }

    // Points around the Y axis
    private static vec3 GetRoundPoint(float segment)
    {
        var angle = MathF.Tau * segment / RoundSegments;
        return new vec3(MathF.Cos(angle), 0.0f, MathF.Sin(angle));
    }

    private static vec3 GetSpherePoint(int stack, int slice)
    {
        var theta = MathF.PI * stack / SphereStacks;
        var phi = MathF.Tau * slice / RoundSegments;
        return new vec3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));
    }

    private static void AddTriangle(List<float> vertices, vec3 a, vec3 b, vec3 c, vec3 normalA, vec3 normalB, vec3 normalC)
    {
        AddVertex(vertices, a, normalA);
        AddVertex(vertices, b, normalB);
        AddVertex(vertices, c, normalC);
    }

    private static void AddVertex(List<float> vertices, vec3 position, vec3 normal)
    {
        vertices.Add(position.x);
        vertices.Add(position.y);
        vertices.Add(position.z);
        vertices.Add(normal.x);
        vertices.Add(normal.y);
        vertices.Add(normal.z);
    }
}
