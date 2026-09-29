using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// A line the camera slides along to the point nearest the target, <see cref="CameraSubBase.Offset"/> units further
    /// (FUN_0027d398, FUN_0027d3f8).
    /// </summary>
    public class CameraLine : CameraSubBase
    {
        public Vector4 LineStart { get; set; }
        public Vector4 LineEnd { get; set; }

        public CameraLine()
        {
            LineStart = new Vector4();
            LineEnd = new Vector4();
        }

        public override int GetLength()
        {
            return base.GetLength() + LineStart.GetLength() + LineEnd.GetLength();
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            LineStart.Read(reader, Constants.SIZE_VECTOR4);
            LineEnd.Read(reader, Constants.SIZE_VECTOR4);
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            LineStart.Write(writer);
            LineEnd.Write(writer);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraLine;
        }
    }
}
