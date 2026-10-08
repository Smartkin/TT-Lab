using System;
using GlmSharp;

namespace TT_Lab.Rendering;

/// <summary>
/// Where the game's environment and metallic shaders read their pictures, the VU1 programs' lookups (0x1D and 0x14 for the environment
/// maps, types 0x16 and 0x0C; 0x17 for the metallic one, type 0x0F; checked by emulating the programs): directions of the object's space
/// taken through its clip matrix, which scales the camera's axes by the game's lens. <c>MainPass.vert</c> does the same with
/// <see cref="ClipScale"/>, the methods here are what it does, for the tests
/// </summary>
public static class EnvironmentMapping
{
    // The follow camera's lens (g_DefaultFov, the field of view blender starts there and most of the game's cameras keep it) on the
    // PAL screen's 4:3 (NarrowAspect, a pixel aspect of 1): RenderView::SetProjection's cot(fov / 2) / aspect, cot(fov / 2) and
    // (far + near) / (far - near), about 1
    public const float GameFovDegrees = 45.0f;
    public const float GameAspect = 4.0f / 3.0f;

    public static readonly vec3 ClipScale = new(1.0f / MathF.Tan(glm.Radians(GameFovDegrees) * 0.5f) / GameAspect,
        1.0f / MathF.Tan(glm.Radians(GameFovDegrees) * 0.5f), 1.0f);

    /// <summary>
    /// The direction on the game's camera's axes scaled by its lens: x the shown frame's right (TT Lab's frame is mirrored before it's
    /// shown, so GL's view x the other way), y up, z ahead
    /// </summary>
    public static vec3 ToGameClip(mat4 view, vec3 worldDirection)
    {
        var viewDirection = new mat3(view) * worldDirection;
        return new vec3(-viewDirection.x, viewDirection.y, -viewDirection.z) * ClipScale;
    }

    /// <summary>
    /// The environment maps: d the way to the eye plus the normal through the clip matrix, normalized, the picture read at
    /// (0.5 + 0.5 d.x, 0.5 - 0.5 d.y) clamped (before the scroll)
    /// </summary>
    public static vec2 EnvironmentUv(mat4 view, vec3 eye, vec3 position, vec3 normal)
    {
        var toEye = (eye - position).Normalized;
        var d = ToGameClip(view, toEye + normal.Normalized).Normalized;
        return vec2.Clamp(new vec2(0.5f + 0.5f * d.x, 0.5f - 0.5f * d.y), vec2.Zero, vec2.Ones);
    }

    /// <summary>
    /// The metallic shader: r the way to the eye reflected off the normal through the clip matrix as it is, the picture read at
    /// (0.5 + 0.5 r.x, 0.5 + 0.5 r.y) clamped (before the scroll)
    /// </summary>
    public static vec2 MetallicUv(mat4 view, vec3 eye, vec3 position, vec3 normal)
    {
        var toEye = (eye - position).Normalized;
        var unit = normal.Normalized;
        var r = ToGameClip(view, 2.0f * vec3.Dot(toEye, unit) * unit - toEye);
        return vec2.Clamp(new vec2(0.5f + 0.5f * r.x, 0.5f + 0.5f * r.y), vec2.Zero, vec2.Ones);
    }
}
