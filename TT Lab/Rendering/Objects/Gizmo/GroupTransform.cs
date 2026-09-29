using System;
using GlmSharp;


namespace TT_Lab.Rendering.Objects.Gizmo;

/// <summary>
/// What the gizmo's drag of the selection's first object does to the others selected with it: they move by the same offset, turn and
/// scale about the first object's place where the drag started, keeping their own transforms otherwise
/// </summary>
public static class GroupTransform
{
    public readonly record struct Placement(vec3 Position, quat Rotation, vec3 Scale);

    public static Placement Apply(TransformMode mode, GizmoTransform start, GizmoTransform result, Placement other)
    {
        switch (mode)
        {
            case TransformMode.TRANSLATE:
                return other with { Position = other.Position + (result.Position - start.Position) };
            case TransformMode.ROTATE:
                var rotation = result.Rotation * start.Rotation.Inverse;
                return new Placement(start.Position + rotation * (other.Position - start.Position), (rotation * other.Rotation).Normalized, other.Scale);
            case TransformMode.SCALE:
                var factor = result.Scale / NonZero(start.Scale);
                return new Placement(start.Position + (other.Position - start.Position) * factor, other.Rotation, other.Scale * factor);
            default:
                return other;
        }
    }

    /// <summary>
    /// The same change as a matrix applied to an object's local transform, for the objects edited as a whole matrix
    /// </summary>
    public static mat4 ApplyToMatrix(TransformMode mode, GizmoTransform start, GizmoTransform result, mat4 matrix)
    {
        switch (mode)
        {
            case TransformMode.TRANSLATE:
                matrix.Column3 = new vec4(matrix.Column3.xyz + (result.Position - start.Position), 1.0f);
                return matrix;
            case TransformMode.ROTATE:
                var rotation = result.Rotation * start.Rotation.Inverse;
                return mat4.Translate(start.Position) * rotation.ToMat4 * mat4.Translate(-start.Position) * matrix;
            case TransformMode.SCALE:
                var factor = result.Scale / NonZero(start.Scale);
                return mat4.Translate(start.Position) * mat4.Scale(factor) * mat4.Translate(-start.Position) * matrix;
            default:
                return matrix;
        }
    }

    public static vec3 NonZero(vec3 value)
    {
        return new vec3(MathF.Abs(value.x) < 1e-6f ? 1.0f : value.x, MathF.Abs(value.y) < 1e-6f ? 1.0f : value.y, MathF.Abs(value.z) < 1e-6f ? 1.0f : value.z);
    }
}
