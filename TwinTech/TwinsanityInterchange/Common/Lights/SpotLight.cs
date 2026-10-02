using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    /// <summary>
    /// A spot light (the game's SpotLight, the tools' "negative light"): a point light shining along <see cref="Direction"/> within a cone.
    /// At a vertex the game takes the cosine between the direction to the light and the cone's axis, gives nothing below
    /// <see cref="OuterConeCosine"/>, fades up to full between it and <see cref="InnerConeCosine"/> and raises the cosine to
    /// <see cref="SpotExponent"/> (FUN_001c8f18). The angles are what the tools made the cosines from, the game only builds the
    /// light's bounds with them.
    /// </summary>
    public class SpotLight : Light
    {
        /// <summary>
        /// Unit vector the light shines along, W 0.
        /// </summary>
        public Vector4 Direction;
        /// <summary>
        /// Cosine of the half angle within which the light is at full intensity, cos(<see cref="ConeAngle"/> / 2).
        /// </summary>
        public Single InnerConeCosine;
        /// <summary>
        /// Cosine of the half angle beyond which the light gives nothing, cos(<see cref="ConeAngle"/> / 2 + <see cref="FalloffAngle"/>).
        /// </summary>
        public Single OuterConeCosine;
        /// <summary>
        /// The whole cone's angle in 65536ths of a turn (18956 is 104 degrees).
        /// </summary>
        public UInt32 ConeAngle;
        /// <summary>
        /// The angle the light fades out over past the cone, in 65536ths of a turn.
        /// </summary>
        public UInt32 FalloffAngle;
        /// <summary>
        /// How many times the distance attenuation multiplies the intensity, like a point light's.
        /// </summary>
        public UInt16 AttenuationPower;
        /// <summary>
        /// Power the cosine is raised to within the cone, 0 to 255 (0 in the retail data).
        /// </summary>
        public UInt16 SpotExponent;

        public const Single TurnsPerUnit = 1.0f / 65536.0f;

        public SpotLight() : base()
        {
            Direction = new Vector4();
        }

        public override LightType Type => LightType.Spot;

        protected override Single BoundsExtent => Intensity * 100.0f;

        public override Int32 GetLength()
        {
            return base.GetLength() + Constants.SIZE_VECTOR4 + 2 * 4 + 2 * 4 + 2 * 2;
        }

        /// <summary>
        /// Sets the cone's angles and the cosines the game lights with from them.
        /// </summary>
        public void SetCone(Single coneAngleDegrees, Single falloffAngleDegrees)
        {
            ConeAngle = (UInt32)Math.Round(coneAngleDegrees / 360.0 * 65536.0);
            FalloffAngle = (UInt32)Math.Round(falloffAngleDegrees / 360.0 * 65536.0);
            (InnerConeCosine, OuterConeCosine) = ConeCosines(ConeAngle, FalloffAngle);
        }

        /// <summary>
        /// The cosines the tools made from a cone's angles (65536ths of a turn): of half the cone, and of that plus the falloff.
        /// </summary>
        public static (Single Inner, Single Outer) ConeCosines(UInt32 coneAngle, UInt32 falloffAngle)
        {
            var halfAngle = coneAngle * TurnsPerUnit * Math.PI;
            var falloff = falloffAngle * TurnsPerUnit * 2.0 * Math.PI;
            return ((Single)Math.Cos(halfAngle), (Single)Math.Cos(halfAngle + falloff));
        }

        public Single ConeAngleDegrees => ConeAngle * TurnsPerUnit * 360.0f;

        public Single FalloffAngleDegrees => FalloffAngle * TurnsPerUnit * 360.0f;

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            Direction.Read(reader, Constants.SIZE_VECTOR4);
            InnerConeCosine = reader.ReadSingle();
            OuterConeCosine = reader.ReadSingle();
            ConeAngle = reader.ReadUInt32();
            FalloffAngle = reader.ReadUInt32();
            AttenuationPower = reader.ReadUInt16();
            SpotExponent = reader.ReadUInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            Direction.Write(writer);
            writer.Write(InnerConeCosine);
            writer.Write(OuterConeCosine);
            writer.Write(ConeAngle);
            writer.Write(FalloffAngle);
            writer.Write(AttenuationPower);
            writer.Write(SpotExponent);
        }
    }
}
