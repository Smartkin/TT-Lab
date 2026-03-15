using GlmSharp;
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
            var quat = new quat(twinVec.X, twinVec.Y, twinVec.Z, twinVec.W);
            var eulerAngles = quat.EulerAngles;
            return new Vector3((float)eulerAngles.x, (float)eulerAngles.y, (float)eulerAngles.z);
        }
    }
}
