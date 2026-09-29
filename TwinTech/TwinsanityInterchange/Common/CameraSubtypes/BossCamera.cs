using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// An arena camera: the target goes into the arena's space (<see cref="WorldToArena"/>), and while its height is between 0 and
    /// <see cref="Orbit"/>.X the camera stands Orbit.Y from the arena's axis in the target's direction, at its height
    /// plus Orbit.Z, turned back to the world (<see cref="ArenaToWorld"/>). With <see cref="UsesDistanceCurves"/>
    /// the radius and height offset follow the target's distance from the axis instead (FUN_00279810, FUN_0027cef8, FUN_0027cf58),
    /// and <see cref="MaxTurnRate"/> caps how fast the camera turns around the arena (FUN_00279af8).
    /// </summary>
    public class BossCamera : CameraSubBase
    {
        public Matrix4 WorldToArena { get; set; }
        public Matrix4 ArenaToWorld { get; set; }
        /// <summary>
        /// X the height the target is followed up to, Y the radius the camera stands at, Z the height above the target, W a leftover
        /// </summary>
        public Vector4 Orbit { get; set; }
        public Boolean UsesDistanceCurves { get; set; }
        /// <summary>
        /// With the curves: the radius becomes distance + (radius - distance) times this
        /// </summary>
        public Single RadiusBlend { get; set; }
        /// <summary>
        /// With the curves: the height offset at the arena's axis, fading to <see cref="FarHeightOffset"/> at the radius
        /// </summary>
        public Single NearHeightOffset { get; set; }
        public Single FarHeightOffset { get; set; }
        /// <summary>
        /// The most the camera's direction around the axis turns per second, 0 for no limit
        /// </summary>
        public Single MaxTurnRate { get; set; }
        /// <summary>
        /// With the curves: whether the target's distance from the axis counts its height too
        /// </summary>
        public Boolean DistanceIncludesHeight { get; set; }

        public BossCamera()
        {
            WorldToArena = new Matrix4();
            ArenaToWorld = new Matrix4();
            Orbit = new Vector4();
        }

        public override int GetLength()
        {
            return base.GetLength() + 18 + Constants.SIZE_MATRIX4 * 2 + Constants.SIZE_VECTOR4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            WorldToArena.Read(reader, Constants.SIZE_MATRIX4);
            ArenaToWorld.Read(reader, Constants.SIZE_MATRIX4);
            Orbit.Read(reader, Constants.SIZE_VECTOR4);
            UsesDistanceCurves = reader.ReadByte() != 0;
            RadiusBlend = reader.ReadSingle();
            NearHeightOffset = reader.ReadSingle();
            FarHeightOffset = reader.ReadSingle();
            MaxTurnRate = reader.ReadSingle();
            DistanceIncludesHeight = reader.ReadByte() != 0;
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            WorldToArena.Write(writer);
            ArenaToWorld.Write(writer);
            Orbit.Write(writer);
            writer.Write((Byte)(UsesDistanceCurves ? 1 : 0));
            writer.Write(RadiusBlend);
            writer.Write(NearHeightOffset);
            writer.Write(FarHeightOffset);
            writer.Write(MaxTurnRate);
            writer.Write((Byte)(DistanceIncludesHeight ? 1 : 0));
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.BossCamera;
        }
    }
}
