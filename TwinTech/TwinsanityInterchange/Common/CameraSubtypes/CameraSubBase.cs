using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// What a camera trigger's camera follows. Every kind gives a position for the target's (the player's) position, the ones made of
    /// a line, a path or a spline slide along it to the point nearest the target plus <see cref="Offset"/>. Verified in the PAL
    /// executable's subtype methods.
    /// </summary>
    public abstract class CameraSubBase : ITwinSerializable
    {
        /// <summary>
        /// How the camera rig's point followers move to the points the subtype gives, one choice in the low two bits of its word
        /// (CameraSubtype::flags, the others are never read). 1 on every retail camera but the zones' 0, the game's constructors set 1
        /// as well
        /// </summary>
        public enum FollowMode : UInt32
        {
            /// <summary>
            /// Straight there, as 3 does
            /// </summary>
            Straight = 0,
            /// <summary>
            /// The followers' own way, at their default rate
            /// </summary>
            OwnWay = 1,
            /// <summary>
            /// At <see cref="FollowRate"/>
            /// </summary>
            AtRate = 2,
        }

        public FollowMode Follow { get; set; }
        /// <summary>
        /// With <see cref="FollowMode.AtRate"/>, the share of the way a second the followers move to the subtype's points
        /// </summary>
        public Single FollowRate { get; set; }
        /// <summary>
        /// How far along the line, path or spline the camera leads (positive) or trails the target's nearest point, in units. A
        /// spline only takes it when its flags say so, points, zones and boss cameras never read it.
        /// </summary>
        public Single Offset { get; set; }

        public CameraSubBase()
        {
            Follow = FollowMode.OwnWay;
        }

        public virtual int GetLength()
        {
            return 12;
        }

        public void Compile()
        {
            return;
        }

        public virtual void Read(BinaryReader reader, int length)
        {
            Follow = (FollowMode)reader.ReadUInt32();
            FollowRate = reader.ReadSingle();
            Offset = reader.ReadSingle();
        }

        public virtual void Write(BinaryWriter writer)
        {
            writer.Write((UInt32)Follow);
            writer.Write(FollowRate);
            writer.Write(Offset);
        }

        public virtual ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.Null;
        }
    }
}
