using System;
using GlmSharp;

namespace TT_Lab.Rendering;

// The camera of a viewer of a single model turns around the model's middle. It never goes over the top or under the bottom, so up
// stays up. Cameras look down their -Z and have no scale
public sealed class OrbitCamera
{
    private const float StartPitch = 20.0f;
    private const float MaxSteepness = 0.98f;

    public vec3 Pivot { get; private set; } = vec3.Zero;
    public float Distance { get; private set; } = 5.0f;
    public float Radius { get; private set; } = 2.5f;

    // Looks at the middle of the box from a little above, far enough back to see all of it
    public mat4 Frame(vec3 min, vec3 max, float fovY)
    {
        if (min.x <= max.x)
        {
            Pivot = (min + max) * 0.5f;
            Radius = Math.Max((max - min).Length * 0.5f, 0.5f);
            Distance = Radius / MathF.Tan(fovY * 0.5f) * 1.2f;
        }

        return Place(quat.FromAxisAngle(glm.Radians(-StartPitch), vec3.UnitX).ToMat4);
    }

    // Left and right turn around the world's up, up and down around the camera's side
    public mat4 Orbit(mat4 camera, float yaw, float pitch)
    {
        var orientation = quat.FromAxisAngle(yaw, vec3.UnitY).ToMat4 * Orientation(camera);
        var pitched = quat.FromAxisAngle(pitch, orientation.Column0.xyz.Normalized).ToMat4 * orientation;
        return Place(MathF.Abs(pitched.Column2.xyz.Normalized.y) <= MaxSteepness ? pitched : orientation);
    }

    public mat4 Zoom(mat4 camera, float distance)
    {
        Distance = Math.Clamp(distance, Radius * 0.05f, Radius * 50.0f);
        return Place(Orientation(camera));
    }

    public mat4 Pan(mat4 camera, vec3 offset)
    {
        Pivot += offset;
        return Place(Orientation(camera));
    }

    private static mat4 Orientation(mat4 camera)
    {
        return camera with { Column3 = new vec4(0, 0, 0, 1) };
    }

    private mat4 Place(mat4 orientation)
    {
        return orientation with { Column3 = new vec4(Pivot + orientation.Column2.xyz.Normalized * Distance, 1) };
    }
}
