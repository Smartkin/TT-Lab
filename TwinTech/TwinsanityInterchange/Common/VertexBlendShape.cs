using System;

namespace Twinsanity.TwinsanityInterchange.Common
{
    public class VertexBlendShape
    {
        /// <summary>
        /// The offset packed into the signed bytes PS2 blend skins store, one step per blend shape unit
        /// </summary>
        public Int32[] GetPackedOffset()
        {
            return new[] { Pack(Offset.X, BlendShape.X), Pack(Offset.Y, BlendShape.Y), Pack(Offset.Z, BlendShape.Z) };
        }

        public Vector4 GetVector4()
        {
            var packed = GetPackedOffset();
            var resultVec = new Vector4();
            resultVec.SetBinaryX((UInt32)packed[0]);
            resultVec.SetBinaryY((UInt32)packed[1]);
            resultVec.SetBinaryZ((UInt32)packed[2]);
            resultVec.W = 1.0f;

            return resultVec;
        }

        private static Int32 Pack(Single offset, Single scale)
        {
            if (scale == 0)
            {
                return 0;
            }

            return (Int32)Math.Clamp(Math.Round(offset / scale), SByte.MinValue, SByte.MaxValue);
        }

        public Vector3 BlendShape { get; set; }
        public Vector4 Offset { get; set; }
    }
}
