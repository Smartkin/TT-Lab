using System;
using System.Numerics;
using GlmSharp;

namespace TT_Lab.Extensions;

public static class GlmExtensions
{
    // Below this cosine of the middle angle only the first and last angles' sum or difference is known (gimbal lock)
    private const double GimbalLockCosine = 1e-7;

    // Euler angles in radians the way quat(vec3) takes them: about X, then Y, then Z (the game's GetRotationXYZ). Worked out in doubles
    // from the rotation's matrix: the arc sine of the middle angle lost what's near a quarter turn (a retail 90 degree turn came out
    // 89.98), and at gimbal lock the first and last angles worked out on their own gave turns of anything (GlmSharp's EulerAngles gives
    // NaN): the turn goes into the first one there
    public static vec3 ToEulerAngles(this quat rotation)
    {
        var angles = EulerAnglesOf(rotation);
        return new vec3((float)angles.x, (float)angles.y, (float)angles.z);
    }

    // The same in degrees, made floats once they're degrees
    public static vec3 ToEulerDegrees(this quat rotation)
    {
        var angles = EulerAnglesOf(rotation) * (180.0 / Math.PI);
        return new vec3((float)angles.x, (float)angles.y, (float)angles.z);
    }

    private static dvec3 EulerAnglesOf(quat rotation)
    {
        double x = rotation.x, y = rotation.y, z = rotation.z, w = rotation.w;
        var length = Math.Sqrt(x * x + y * y + z * z + w * w);
        if (!(length > 0.0))
        {
            return dvec3.Zero;
        }

        x /= length;
        y /= length;
        z /= length;
        w /= length;
        // Where X turns to gives the middle and last angles, the heights Y and Z turn to the first
        var turnedX = new dvec3(1.0 - 2.0 * (y * y + z * z), 2.0 * (x * y + w * z), 2.0 * (x * z - w * y));
        var cosine = Math.Sqrt(turnedX.x * turnedX.x + turnedX.y * turnedX.y);
        var middle = Math.Atan2(-turnedX.z, cosine);
        if (cosine > GimbalLockCosine)
        {
            return new dvec3(Math.Atan2(2.0 * (y * z + w * x), 1.0 - 2.0 * (x * x + y * y)), middle, Math.Atan2(turnedX.y, turnedX.x));
        }

        // Where Y turns to then has the turns about X and Z together
        var turnedYx = 2.0 * (x * y - w * z);
        var turnedYy = 1.0 - 2.0 * (x * x + z * z);
        return new dvec3(Math.Atan2(-turnedX.z * turnedYx, turnedYy), middle, 0.0);
    }

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

    public static mat4 CreateRotationMatrix(vec3 rotation)
    {
        return mat4.RotateZ(rotation.z) * mat4.RotateY(rotation.y) * mat4.RotateX(rotation.x);
    }

    public static quat Multiply(this quat left, quat right)
    {
        var leftXyz = new vec3(left.x, left.y, left.z);
        var rightXyz = new vec3(right.x, right.y, right.z);
        return new quat(right.w * leftXyz + left.w * rightXyz + vec3.Cross(leftXyz, rightXyz), left.w * right.w - vec3.Dot(leftXyz, rightXyz));
    }
}