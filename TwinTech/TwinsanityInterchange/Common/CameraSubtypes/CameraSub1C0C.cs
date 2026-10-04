using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// A camera around a point at the target's height (Camera1C0C, no data has one): its read only takes the four bytes of its follow
    /// flags, nothing gives it its other values
    /// </summary>
    public class CameraSub1C0C : CameraSubBase
    {
        public override int GetLength()
        {
            return 4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            Follow = (FollowMode)reader.ReadUInt32();
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write((UInt32)Follow);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraSub1C0C;
        }
    }
}
