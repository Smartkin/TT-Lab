using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    /// <summary>
    /// Lights from one direction with its color times its intensity, wherever it is.
    /// </summary>
    public class DirectionalLight : Light
    {
        /// <summary>
        /// Unit vector pointing at where the light comes from, W 0.
        /// </summary>
        public Vector4 Direction { get; set; }
        /// <summary>
        /// Copied from the tools' template and never read by the game (0, 9, -1 or 6922 in the retail data).
        /// </summary>
        public Int16 Leftover { get; set; }

        public DirectionalLight() : base()
        {
            Direction = new Vector4();
        }

        public override LightType Type => LightType.Directional;

        protected override Single BoundsExtent => Intensity * 99999.99f;

        public override Int32 GetLength()
        {
            return base.GetLength() + 2 + Constants.SIZE_VECTOR4;
        }

        public override void Read(BinaryReader reader, Int32 length)
        {
            base.Read(reader, length);
            Direction.Read(reader, Constants.SIZE_VECTOR4);
            Leftover = reader.ReadInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            Direction.Write(writer);
            writer.Write(Leftover);
        }
    }
}
