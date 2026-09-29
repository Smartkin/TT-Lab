using System;
using System.IO;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    /// <summary>
    /// Lights what's around it: at distance d the attenuation is 25 / (d² + 25) and the light gives its color times
    /// intensity · attenuation^<see cref="AttenuationPower"/>, from the direction of the light (FUN_001c87b0).
    /// </summary>
    public class PointLight : Light
    {
        /// <summary>
        /// How many times the attenuation multiplies the intensity: 0 lights everything at full intensity, the retail data has 0 to 2.
        /// </summary>
        public Int16 AttenuationPower;

        public override LightType Type => LightType.Point;

        protected override Single BoundsExtent => Intensity * 100.0f;

        public override Int32 GetLength()
        {
            return base.GetLength() + 2;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            AttenuationPower = reader.ReadInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(AttenuationPower);
        }

        /// <summary>
        /// The attenuation the game applies at a distance, 25 / (d² + 25), used by <see cref="IntensityAt"/>.
        /// </summary>
        public static Single AttenuationAt(Single distance)
        {
            return 25.0f / (distance * distance + 25.0f);
        }

        public Single IntensityAt(Single distance)
        {
            var intensity = Intensity;
            var attenuation = AttenuationAt(distance);
            for (var i = 0; i < AttenuationPower; i++)
            {
                intensity *= attenuation;
            }

            return intensity;
        }
    }
}
