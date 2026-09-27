using System;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects;

/// <summary>
/// A chunk link's load wall is kept as the corners of a quad, one in each column of a matrix going around it from the bottom left. Its
/// object gets the rectangle between them as its transform and the corners' place in that is kept, so moving, turning and scaling the wall
/// keeps whatever shape it has
/// </summary>
public sealed class LoadWallCorners : ITransformConverter
{
    // Replaced as a whole since the wall gets drawn from them on the render thread
    private vec3[] _local = [new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0)];

    public ReadOnlySpan<vec3> Local => _local;

    /// <summary>
    /// Whether the corners span an area, walls of new links have them all at the origin
    /// </summary>
    public static bool IsUsable(Matrix4? corners)
    {
        if (corners == null)
        {
            return false;
        }

        var (_, halfWidth, halfHeight) = GetRectangle(corners);
        return vec3.Cross(halfWidth, halfHeight).LengthSqr > 1e-8f;
    }

    public mat4 ToTransform(Matrix4 data)
    {
        var (center, halfWidth, halfHeight) = GetRectangle(data);
        var normal = vec3.Cross(halfWidth, halfHeight).NormalizedSafe;
        var transform = new mat4(new vec4(halfWidth, 0.0f), new vec4(halfHeight, 0.0f), new vec4(normal, 0.0f), new vec4(center, 1.0f));
        if (MathF.Abs(transform.Determinant) < 1e-12f)
        {
            return transform;
        }

        var inverse = transform.Inverse;
        var local = new vec3[4];
        for (var i = 0; i < local.Length; i++)
        {
            local[i] = (inverse * new vec4(GetCorner(data, i), 1.0f)).xyz;
        }

        _local = local;
        return transform;
    }

    public Matrix4 ToData(mat4 transform)
    {
        var local = _local;
        return new mat4(ToCorner(transform, local[0]), ToCorner(transform, local[1]), ToCorner(transform, local[2]), ToCorner(transform, local[3])).ToTwin();
    }

    private static vec4 ToCorner(in mat4 transform, vec3 local)
    {
        return new vec4((transform * new vec4(local, 1.0f)).xyz, 1.0f);
    }

    // Halves of the rectangle's sides are the averages of the quad's opposite sides
    private static (vec3 Center, vec3 HalfWidth, vec3 HalfHeight) GetRectangle(Matrix4 corners)
    {
        var bottomLeft = GetCorner(corners, 0);
        var bottomRight = GetCorner(corners, 1);
        var topRight = GetCorner(corners, 2);
        var topLeft = GetCorner(corners, 3);
        return ((bottomLeft + bottomRight + topRight + topLeft) * 0.25f,
            (bottomRight - bottomLeft + topRight - topLeft) * 0.25f,
            (topLeft - bottomLeft + topRight - bottomRight) * 0.25f);
    }

    private static vec3 GetCorner(Matrix4 corners, int index)
    {
        var corner = corners[index];
        return new vec3(corner.X, corner.Y, corner.Z);
    }
}

/// <summary>
/// Quad of a load wall, a child of the object that's transformed to it
/// </summary>
public sealed class LoadWallVisual : Renderable, IPrimitiveRenderable
{
    private static readonly vec4 ActiveColor = new(0.3f, 0.85f, 1.0f, 1.0f);
    // An inactive wall can't be crossed, only the linked chunk's scenery gets shown
    private static readonly vec4 InactiveColor = new(0.6f, 0.62f, 0.66f, 1.0f);

    private readonly EditableObject _owner;
    private readonly LoadWallCorners _corners;
    private volatile bool _isActive;

    public LoadWallVisual(RenderContext context, EditableObject owner, LoadWallCorners corners, bool isActive) : base(context, "LOAD_WALL")
    {
        _owner = owner;
        _corners = corners;
        _isActive = isActive;
        owner.AddChild(this);
    }

    public bool IsActive
    {
        get => _isActive;
        set => _isActive = value;
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        var world = WorldTransform;
        var local = _corners.Local;
        Span<vec3> corners = stackalloc vec3[4];
        for (var i = 0; i < corners.Length; i++)
        {
            corners[i] = (world * new vec4(local[i], 1.0f)).xyz;
        }

        var color = _isActive ? ActiveColor : InactiveColor;
        var isSelected = _owner.IsSelected;
        // Filled as the rectangle between the corners, the outline goes through the corners themselves
        renderer.DrawQuad(world.Column3.xyz, world.Column0.xyz, world.Column1.xyz, color with { w = isSelected ? 0.3f : 0.14f }, PrimitiveLayer.WorldXRay);
        renderer.DrawPolyline(corners, color with { w = 0.95f }, isSelected ? 3.0f : 2.0f, PrimitiveLayer.WorldXRay, true);
        renderer.DrawLine(corners[0], corners[2], color with { w = 0.35f }, 1.0f, PrimitiveLayer.WorldXRay);
        renderer.DrawLine(corners[1], corners[3], color with { w = 0.35f }, 1.0f, PrimitiveLayer.WorldXRay);
    }
}
