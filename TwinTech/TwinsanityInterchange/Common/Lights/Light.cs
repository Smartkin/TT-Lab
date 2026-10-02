using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common.Lights
{
    /// <summary>
    /// Kind of a scenery light, the low byte of its header. The game reads it back from the file and lights by it.
    /// </summary>
    public enum LightType : Byte
    {
        Ambient = 0,
        Directional = 1,
        Point = 2,
        /// <summary>A spot light (the tools called it a negative light)</summary>
        Spot = 3,
    }

    /// <summary>
    /// A light of a level's scenery. Every light adds its color times its intensity: ambient lights straight away, the others
    /// through the 3 strongest ones at every vertex. Verified in the PAL executable (FUN_001c7f50).
    /// </summary>
    public abstract class Light : ITwinSerializable
    {
        /// <summary>
        /// Bit 8 of the header, set on every retail light and never read by the game. The rest of the header is the light's type.
        /// </summary>
        public Boolean Enabled = true;
        /// <summary>
        /// Multiplies the color. Point and spot lights fall off with distance on top of it (see their attenuation power).
        /// </summary>
        public Single Intensity;
        public Vector4 Color;
        public Vector4 Position;
        /// <summary>
        /// Bounds the tools kept, which the game works out again at load (<see cref="ComputeBounds"/>) and never reads.
        /// </summary>
        public Vector4 BoundsMin;
        public Vector4 BoundsMax;

        public Light()
        {
            Color = new Vector4();
            Position = new Vector4();
            BoundsMin = new Vector4();
            BoundsMax = new Vector4();
        }

        public abstract LightType Type { get; }

        /// <summary>
        /// Half the size of the box the game puts around the light at load, a multiple of its intensity.
        /// </summary>
        protected abstract Single BoundsExtent { get; }

        /// <summary>
        /// Sets the bounds the way the game does at load: a box of <see cref="BoundsExtent"/> around the position.
        /// </summary>
        public virtual void ComputeBounds()
        {
            var extent = BoundsExtent;
            BoundsMin = new Vector4(Position.X - extent, Position.Y - extent, Position.Z - extent, 1);
            BoundsMax = new Vector4(Position.X + extent, Position.Y + extent, Position.Z + extent, 1);
        }

        public virtual Int32 GetLength()
        {
            return 8 + 4 * Constants.SIZE_VECTOR4;
        }

        public void Compile()
        {
            return;
        }

        public virtual void Read(BinaryReader reader, Int32 length)
        {
            var header = reader.ReadUInt32();
            Enabled = (header & 0x100) != 0;
            Intensity = reader.ReadSingle();
            Color.Read(reader, Constants.SIZE_VECTOR4);
            Position.Read(reader, Constants.SIZE_VECTOR4);
            BoundsMin.Read(reader, Constants.SIZE_VECTOR4);
            BoundsMax.Read(reader, Constants.SIZE_VECTOR4);
        }

        public virtual void Write(BinaryWriter writer)
        {
            writer.Write((UInt32)Type | (Enabled ? 0x100u : 0u));
            writer.Write(Intensity);
            Color.Write(writer);
            Position.Write(writer);
            BoundsMin.Write(writer);
            BoundsMax.Write(writer);
        }
    }
}
