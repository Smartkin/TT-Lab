using GlmSharp;
using org.ogre;
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
            var quat = new Quaternion(twinVec.W, twinVec.X, twinVec.Y, twinVec.Z);
            var rotationMatrix = new Matrix3();
            quat.ToRotationMatrix(rotationMatrix);
            var rotX = new Radian();
            var rotY = new Radian();
            var rotZ = new Radian();
            if (!rotationMatrix.ToEulerAnglesXYZ(rotX, rotY, rotZ))
            {
                Log.WriteLine($"WARNING: Received solution for {twinVec} Euler angles was not unique");
            }
            return new Vector3(rotX.valueDegrees(), rotY.valueDegrees(), rotZ.valueDegrees());
        }
    }
}
