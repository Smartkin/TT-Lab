using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// A chunk link's loading hull, a child of the object placed where the hull is: its edges and, for four-sided faces, faint fills
/// </summary>
public sealed class LinkHullVisual : Renderable, IPrimitiveRenderable
{
    private static readonly vec4 Color = new(1.0f, 0.6f, 0.2f, 1.0f);

    private readonly EditableObject _owner;
    // Replaced as a whole since the hull gets drawn from them on the render thread
    private volatile Shape _shape;

    private sealed record Shape(vec3[] Vertexes, (int From, int To)[] Edges, int[][] Quads);

    public LinkHullVisual(RenderContext context, EditableObject owner, ChunkLinkHull hull) : base(context, "LINK_HULL")
    {
        _owner = owner;
        _shape = ToShape(hull);
        owner.AddChild(this);
    }

    public void SetShape(ChunkLinkHull hull)
    {
        _shape = ToShape(hull);
    }

    private static Shape ToShape(ChunkLinkHull hull)
    {
        var vertexes = hull.Vertexes.Select(vertex => new vec3(vertex.X, vertex.Y, vertex.Z)).ToArray();
        var edges = new HashSet<(int, int)>();
        foreach (var face in hull.Faces)
        {
            for (var i = 0; i < face.Count; i++)
            {
                int from = face[i], to = face[(i + 1) % face.Count];
                if (from < vertexes.Length && to < vertexes.Length && from != to)
                {
                    edges.Add(from < to ? (from, to) : (to, from));
                }
            }
        }

        var quads = hull.Faces.Where(face => face.Count == 4 && face.All(index => index < vertexes.Length)).Select(face => face.Select(index => (int)index).ToArray()).ToArray();
        return new Shape(vertexes, edges.ToArray(), quads);
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        var shape = _shape;
        var world = WorldTransform;
        var corners = new vec3[shape.Vertexes.Length];
        for (var i = 0; i < corners.Length; i++)
        {
            corners[i] = (world * new vec4(shape.Vertexes[i], 1.0f)).xyz;
        }

        var isSelected = _owner.IsSelected;
        foreach (var quad in shape.Quads)
        {
            var (a, b, c, d) = (corners[quad[0]], corners[quad[1]], corners[quad[2]], corners[quad[3]]);
            renderer.DrawQuad((a + b + c + d) * 0.25f, (b - a + c - d) * 0.25f, (d - a + c - b) * 0.25f, Color with { w = isSelected ? 0.2f : 0.08f }, PrimitiveLayer.WorldXRay);
        }

        foreach (var (from, to) in shape.Edges)
        {
            renderer.DrawLine(corners[from], corners[to], Color with { w = 0.95f }, isSelected ? 3.0f : 1.5f, PrimitiveLayer.WorldXRay);
        }
    }
}
