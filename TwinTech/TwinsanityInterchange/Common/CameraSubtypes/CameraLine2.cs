using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// A line the camera slides along by the target's distance from its start (measured flat, without the height): at the start up to
    /// <see cref="NearDistance"/>, at the end from <see cref="FarDistance"/> on, between them at the share of the way between the
    /// distances plus <see cref="NearDistance"/> (FUN_0027da60, CameraLine2At): every retail camera has a near distance of 0, any
    /// other puts the camera past the line.
    /// </summary>
    public class CameraLine2 : CameraSubBase
    {
        public Vector4 LineStart { get; set; }
        public Vector4 LineEnd { get; set; }
        public Single NearDistance { get; set; }
        public Single FarDistance { get; set; }

        public CameraLine2()
        {
            LineStart = new Vector4();
            LineEnd = new Vector4();
        }

        public override int GetLength()
        {
            return base.GetLength() + 8 + LineStart.GetLength() + LineEnd.GetLength();
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            LineStart.Read(reader, Constants.SIZE_VECTOR4);
            LineEnd.Read(reader, Constants.SIZE_VECTOR4);
            NearDistance = reader.ReadSingle();
            FarDistance = reader.ReadSingle();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            LineStart.Write(writer);
            LineEnd.Write(writer);
            writer.Write(NearDistance);
            writer.Write(FarDistance);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraLine2;
        }
    }
}
