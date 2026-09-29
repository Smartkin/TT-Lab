using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// A point the camera stays between the target and: <see cref="Mode"/> 0 puts it <see cref="Distance"/> of the way from the
    /// target to the point (0 to 1), 1 that many units from the target towards the point, 2 that many units from the point towards
    /// the target (FUN_0027a570).
    /// </summary>
    public class CameraPoint2 : CameraSubBase
    {
        public Vector4 Point { get; set; }
        public Single Distance { get; set; }
        public Byte Mode { get; set; }

        public CameraPoint2()
        {
            Point = new Vector4();
        }

        public override int GetLength()
        {
            return base.GetLength() + 5 + Constants.SIZE_VECTOR4;
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            Point.Read(reader, Constants.SIZE_VECTOR4);
            Distance = reader.ReadSingle();
            Mode = reader.ReadByte();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            Point.Write(writer);
            writer.Write(Distance);
            writer.Write(Mode);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraPoint2;
        }
    }
}
