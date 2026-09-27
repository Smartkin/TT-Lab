using System;
using GlmSharp;

namespace TT_Lab.Rendering;

public readonly record struct Ray(vec3 Origin, vec3 Direction)
{
    public vec3 GetPoint(float distance) => Origin + Direction * distance;
}

/// <summary>
/// State of a camera for a single frame. Screen positions are in the viewport control's coordinates, the rendered image gets
/// mirrored horizontally before it's shown and its origin is at the top left corner unlike GL's
/// </summary>
public readonly struct FrameCamera
{
    public FrameCamera(mat4 cameraTransform, mat4 projection, vec2 viewportSize, float fovY)
    {
        World = cameraTransform;
        View = cameraTransform.Inverse;
        Projection = projection;
        ViewProjection = projection * View;
        InverseViewProjection = ViewProjection.Inverse;
        ViewportSize = viewportSize;
        FovY = fovY;
        Position = cameraTransform.Column3.xyz;
        Forward = (-cameraTransform.Column2.xyz).Normalized;
        Up = cameraTransform.Column1.xyz.Normalized;
    }

    public mat4 World { get; }
    public mat4 View { get; }
    public mat4 Projection { get; }
    public mat4 ViewProjection { get; }
    public mat4 InverseViewProjection { get; }
    public vec2 ViewportSize { get; }
    public float FovY { get; }
    public vec3 Position { get; }
    public vec3 Forward { get; }
    public vec3 Up { get; }

    public bool IsValid => ViewportSize.x >= 1 && ViewportSize.y >= 1;

    public bool WorldToScreen(vec3 world, out vec2 screen)
    {
        var clip = ViewProjection * new vec4(world, 1.0f);
        if (clip.w <= 1e-5f)
        {
            screen = vec2.Zero;
            return false;
        }

        var glX = (clip.x / clip.w * 0.5f + 0.5f) * ViewportSize.x;
        var glY = (clip.y / clip.w * 0.5f + 0.5f) * ViewportSize.y;
        screen = new vec2(ViewportSize.x - glX, ViewportSize.y - glY);
        return true;
    }

    public Ray ScreenRay(vec2 screen)
    {
        var ndcX = (ViewportSize.x - screen.x) / ViewportSize.x * 2.0f - 1.0f;
        var ndcY = (ViewportSize.y - screen.y) / ViewportSize.y * 2.0f - 1.0f;
        var near = InverseViewProjection * new vec4(ndcX, ndcY, -1.0f, 1.0f);
        var far = InverseViewProjection * new vec4(ndcX, ndcY, 1.0f, 1.0f);
        var direction = far.xyz / far.w - near.xyz / near.w;
        return new Ray(Position, direction.Normalized);
    }

    /// <summary>
    /// Size of a pixel in world units at the given point, used to keep things the same size on screen
    /// </summary>
    public float WorldUnitsPerPixel(vec3 point)
    {
        var depth = Math.Max(vec3.Dot(point - Position, Forward), 0.01f);
        return 2.0f * depth * MathF.Tan(FovY * 0.5f) / Math.Max(ViewportSize.y, 1.0f);
    }
}
