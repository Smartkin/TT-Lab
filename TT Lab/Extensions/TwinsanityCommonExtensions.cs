using System;
using System.Numerics;
using GlmSharp;
using Silk.NET.Maths;
using Vortice.Mathematics;
using Matrix4 = Twinsanity.TwinsanityInterchange.Common.Matrix4;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.Extensions
{
    public static class TwinsanityCommonExtensions
    {
        public static System.Numerics.Vector4 ToSystem(this Twinsanity.TwinsanityInterchange.Common.Vector4 twinVec)
        {
            return new System.Numerics.Vector4(twinVec.X, twinVec.Y, twinVec.Z, twinVec.W);
        }

        public static System.Numerics.Matrix4x4 ToSystem(this Twinsanity.TwinsanityInterchange.Common.Matrix4 twinMat)
        {
            var glmMat = twinMat.ToGlm().Transposed;
            return new System.Numerics.Matrix4x4(glmMat.Row0.x, glmMat.Row0.y, glmMat.Row0.z, glmMat.Row0.w,
                glmMat.Row1.x, glmMat.Row1.y, glmMat.Row1.z, glmMat.Row1.w,
                glmMat.Row2.x, glmMat.Row2.y, glmMat.Row2.z, glmMat.Row2.w,
                glmMat.Row3.x, glmMat.Row3.y, glmMat.Row3.z, glmMat.Row3.w);
        }
        
        public static Twinsanity.TwinsanityInterchange.Common.Matrix4 ToTwin(this System.Numerics.Matrix4x4 sysMat)
        {
            var transposedMatrix = sysMat;
            return new Matrix4
            {
                Column1 = new Vector4(transposedMatrix.M11, transposedMatrix.M12, transposedMatrix.M13, transposedMatrix.M14),
                Column2 = new Vector4(transposedMatrix.M21, transposedMatrix.M22, transposedMatrix.M23, transposedMatrix.M24),
                Column3 = new Vector4(transposedMatrix.M31, transposedMatrix.M32, transposedMatrix.M33, transposedMatrix.M34),
                Column4 = new Vector4(transposedMatrix.M41, transposedMatrix.M42, transposedMatrix.M43, transposedMatrix.M44)
            };
        }

        public static GlmSharp.quat ToQuat(this Twinsanity.TwinsanityInterchange.Common.Vector3 vec)
        {
            var mat = mat4.RotateZ(vec.Z) * mat4.RotateX(vec.X) * mat4.RotateY(vec.Y);
            var quat = GlmSharp.quat.FromMat4(mat);
            quat.x = -quat.x;
            quat.y = -quat.y;
            quat.z = -quat.z;
            return quat;
        }

        public static GlmSharp.vec4 ToGlm(this Twinsanity.TwinsanityInterchange.Common.Vector4 twinVec)
        {
            return new GlmSharp.vec4(twinVec.X, twinVec.Y, twinVec.Z, twinVec.W);
        }

        public static System.Numerics.Vector3 ToSystem(this Twinsanity.TwinsanityInterchange.Common.Vector3 twinVec)
        {
            return new System.Numerics.Vector3(twinVec.X, twinVec.Y, twinVec.Z);
        }

        public static GlmSharp.vec3 ToGlm(this Twinsanity.TwinsanityInterchange.Common.Vector3 twinVec)
        {
            return new GlmSharp.vec3(twinVec.X, twinVec.Y, twinVec.Z);
        }

        public static GlmSharp.vec3 ToRadiansGlm(this Twinsanity.TwinsanityInterchange.Common.Vector3 twinVec)
        {
            return new GlmSharp.vec3(glm.Radians(twinVec.X), glm.Radians(twinVec.Y), glm.Radians(twinVec.Z));
        }

        public static GlmSharp.mat4 ToGlm(this Twinsanity.TwinsanityInterchange.Common.Matrix4 twinMat)
        {
            return new mat4(twinMat.Column1.ToGlm(), twinMat.Column2.ToGlm(), twinMat.Column3.ToGlm(), twinMat.Column4.ToGlm());
        }

        public static Vector4 ToTwin(this vec4 vec)
        {
            return new Vector4(vec.x, vec.y, vec.z, vec.w);
        }

        public static Matrix4 ToTwin(this mat4 mat)
        {
            var twinMat = new Matrix4
            {
                Column1 = mat.Column0.ToTwin(),
                Column2 = mat.Column1.ToTwin(),
                Column3 = mat.Column2.ToTwin(),
                Column4 = mat.Column3.ToTwin()
            };
            return twinMat;
        }

        public static Vector3 ToEulerAngles(this Vector4 twinVec)
        {
            var quat = new Quaternion(twinVec.X, twinVec.Y, twinVec.Z, twinVec.W);
            return quat.ToTwinEulerAngles();
        }

        public static Vector3 ToTwinEulerAngles(this Quaternion q)
        {
            var angles = q.ToEulerAngles();
            return new Vector3(angles.X, angles.Y, angles.Z);
        }
        
        private static System.Numerics.Vector3 ToEulerAngles(this Quaternion q)
        {
            System.Numerics.Vector3 angles = new();
            var quat = new quat(q.X, q.Y, q.Z, q.W);
            var mat = quat.ToMat4;
            angles.X = (float)Math.Asin(-Math.Clamp(mat.m12, -1, 1));
            if (Math.Abs(mat.m12) < 0.9999999)
            {
                angles.Y = (float)Math.Atan2(mat.m02, mat.m22);
                angles.Z = (float)Math.Atan2(mat.m10, mat.m11);
            }
            else
            {
                angles.Y = (float)Math.Atan2(-mat.m20, mat.m00);
                angles.Z = 0.0f;
            }
            
            return angles;
        }
    }
}
