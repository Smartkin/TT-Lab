using System.Drawing;

namespace TT_Lab.Util
{
    public static class VectorExtension
    {
        public static float[] ToArray(this Color color)
        {
            return new float[] { color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f };
        }

        public static Twinsanity.TwinsanityInterchange.Common.Vector4 ToTwin(this System.Numerics.Quaternion quat)
        {
            return new Twinsanity.TwinsanityInterchange.Common.Vector4(quat.X, quat.Y, quat.Z, quat.W);
        }

        public static Twinsanity.TwinsanityInterchange.Common.Vector4 ToTwin(this System.Numerics.Vector4 v)
        {
            return new Twinsanity.TwinsanityInterchange.Common.Vector4(v.X, v.Y, v.Z, v.W);
        }

        public static Twinsanity.TwinsanityInterchange.Common.Vector4 ToTwin(this System.Numerics.Vector3 v)
        {
            return new Twinsanity.TwinsanityInterchange.Common.Vector4(v.X, v.Y, v.Z, 1.0f);
        }

        public static Twinsanity.TwinsanityInterchange.Common.Vector4 ToTwin(this System.Numerics.Vector2 v)
        {
            return new Twinsanity.TwinsanityInterchange.Common.Vector4(v.X, v.Y, 1.0f, 0.0f);
        }

    }
}
