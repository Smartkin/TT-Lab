using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// A point the camera stays between the target and: <see cref="Mode"/> 0 puts it <see cref="Distance"/> of the way from the
    /// target to the point (0 to 1), 1 that many units from the target towards the point, 2 the same but no further than the point
    /// (FUN_0027a570, CameraPoint2At; the retail cameras use 0 and 1).
    /// </summary>
    public class CameraPoint2 : CameraSubBase
    {
        /// <summary>
        /// Where between the target and the point the camera stands, compared whole: other values leave the camera where it was
        /// </summary>
        public enum PointMode : Byte
        {
            /// <summary>
            /// <see cref="Distance"/> of the way from the target to the point (0 to 1)
            /// </summary>
            ShareOfTheWay = 0,
            /// <summary>
            /// <see cref="Distance"/> units from the target towards the point
            /// </summary>
            FromTheTarget = 1,
            /// <summary>
            /// The same, but no further than the point
            /// </summary>
            NoFurtherThanThePoint = 2,
        }

        public Vector4 Point { get; set; }
        public Single Distance { get; set; }
        public PointMode Mode { get; set; }

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
            Mode = (PointMode)reader.ReadByte();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            Point.Write(writer);
            writer.Write(Distance);
            writer.Write((Byte)Mode);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraPoint2;
        }
    }
}
