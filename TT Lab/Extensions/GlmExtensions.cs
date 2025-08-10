using System;
using System.Numerics;
using GlmSharp;

namespace TT_Lab.Extensions;

public static class GlmExtensions
{
    // Slerp code credits to https://github.com/opentk/opentk/blob/master/src/OpenTK.Mathematics/Data/Quaternion.cs
    public static quat SLerpSafe(quat q1, quat q2, float blend)
    {
        // if either input is zero, return the other.
        if (q1.LengthSqr == 0.0f)
        {
            if (q2.LengthSqr == 0.0f)
            {
                return quat.Identity;
            }

            return q2;
        }

        if (q2.LengthSqr == 0.0f)
        {
            return q1;
        }

        var cosHalfAngle = (q1.w * q2.w) + vec3.Dot(new vec3(q1.x, q1.y, q1.z), new vec3(q2.x, q2.y, q2.z));

        if (cosHalfAngle >= 1.0f || cosHalfAngle <= -1.0f)
        {
            // angle = 0.0f, so just return one input.
            return q1;
        }

        if (cosHalfAngle < 0.0f)
        {
            q2 = q2.Conjugate;
            q2.w = -q2.w;
            cosHalfAngle = -cosHalfAngle;
        }

        float blendA;
        float blendB;
        if (cosHalfAngle < 0.99f)
        {
            // do proper slerp for big angles
            var halfAngle = MathF.Acos(cosHalfAngle);
            var sinHalfAngle = MathF.Sin(halfAngle);
            var oneOverSinHalfAngle = 1.0f / sinHalfAngle;
            blendA = MathF.Sin(halfAngle * (1.0f - blend)) * oneOverSinHalfAngle;
            blendB = MathF.Sin(halfAngle * blend) * oneOverSinHalfAngle;
        }
        else
        {
            // do lerp if angle is minuscule.
            blendA = 1.0f - blend;
            blendB = blend;
        }

        var result = new quat((blendA * new vec3(q1.x, q1.y, q1.z)) + (blendB * new vec3(q2.x, q2.y, q2.z)), (blendA * q1.w) + (blendB * q2.w));
        return result.LengthSqr > 0.0f ? result.Normalized : quat.Identity;
    }

    public static vec2 FromSystem(this Vector2 v)
    {
        return new vec2(v.X, v.Y);
    }

    public static quat Multiply(this quat left, quat right)
    {
        var leftXyz = new vec3(left.x, left.y, left.z);
        var rightXyz = new vec3(right.x, right.y, right.z);
        return new quat(right.w * leftXyz + left.w * rightXyz + vec3.Cross(leftXyz, rightXyz), left.w * right.w - vec3.Dot(leftXyz, rightXyz));
    }
}