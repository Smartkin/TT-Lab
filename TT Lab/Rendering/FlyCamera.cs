using System;
using GlmSharp;

namespace TT_Lab.Rendering;

// The camera of a chunk's viewport turns left and right around the world's up and up and down around its side, at most until it looks
// straight up or down, so it never turns over and never rolls. Cameras look down their -Z and have no scale
public static class FlyCamera
{
    private const float MaxPitch = MathF.PI * 0.5f;

    public static mat4 Look(mat4 camera, float yaw, float pitch)
    {
        var forward = -camera.Column2.xyz.Normalized;
        // The side stays level, so it still tells the heading while looking straight up or down. A camera that was turned over gets
        // turned back up looking where it looked
        var side = camera.Column1.y < -1e-3f ? -camera.Column0.xyz : camera.Column0.xyz;
        var heading = MathF.Atan2(-side.z, side.x) + yaw;
        var elevation = Math.Clamp(MathF.Asin(Math.Clamp(forward.y, -1.0f, 1.0f)) + pitch, -MaxPitch, MaxPitch);
        var orientation = (quat.FromAxisAngle(heading, vec3.UnitY) * quat.FromAxisAngle(elevation, vec3.UnitX)).ToMat4;
        return orientation with { Column3 = camera.Column3 };
    }
}
