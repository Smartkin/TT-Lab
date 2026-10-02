using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// A curve sampled every <see cref="StepLength"/> units, the camera slides along it (a cubic Hermite spline between the samples
    /// with their tangents, FUN_0018a7a8) to the point nearest the target plus an offset, <see cref="CameraSubBase.Offset"/> when
    /// bit 0 of <see cref="SplineFlags"/> is set (FUN_0027cfe8, FUN_00279f80).
    /// </summary>
    public class CameraSpline : CameraSubBase
    {
        public Single StepLength { get; set; }
        /// <summary>
        /// The samples, W 0
        /// </summary>
        public List<Vector4> PathPoints { get; set; }
        /// <summary>
        /// A unit tangent for every sample, W 1
        /// </summary>
        public List<Vector4> Tangents { get; set; }
        /// <summary>
        /// Every segment's (between two samples) arc length from the start, like a path's (<see cref="TwinPathParameters"/>)
        /// </summary>
        public List<Single> ArcLengths { get; set; }
        /// <summary>
        /// 1 over the steps each segment takes
        /// </summary>
        public List<Single> InverseSteps { get; set; }
        /// <summary>
        /// Bit 0 takes <see cref="CameraSubBase.Offset"/> as the offset along the curve, the rest are leftovers (15 or 0xCDCD in the retail data)
        /// </summary>
        public UInt16 SplineFlags { get; set; }

        public CameraSpline()
        {
            PathPoints = new List<Vector4>();
            Tangents = new List<Vector4>();
            ArcLengths = new List<Single>();
            InverseSteps = new List<Single>();
        }

        public override int GetLength()
        {
            return base.GetLength() + 4 + 4 + PathPoints.Count * Constants.SIZE_VECTOR4 + Tangents.Count * Constants.SIZE_VECTOR4 + 4 * (ArcLengths.Count + InverseSteps.Count) + 2;
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            int segments = reader.ReadInt32();
            StepLength = reader.ReadSingle();
            PathPoints.Clear();
            Tangents.Clear();
            for (int i = 0; i < segments + 1; ++i)
            {
                Vector4 point = new Vector4();
                point.Read(reader, Constants.SIZE_VECTOR4);
                PathPoints.Add(point);
                Vector4 tangent = new Vector4();
                tangent.Read(reader, Constants.SIZE_VECTOR4);
                Tangents.Add(tangent);
            }

            TwinPathParameters.Read(reader, segments, ArcLengths, InverseSteps);

            SplineFlags = reader.ReadUInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(ArcLengths.Count);
            writer.Write(StepLength);
            for (var i = 0; i < PathPoints.Count; ++i)
            {
                PathPoints[i].Write(writer);
                Tangents[i].Write(writer);
            }

            TwinPathParameters.Write(writer, ArcLengths, InverseSteps);

            writer.Write(SplineFlags);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraSpline;
        }
    }
}
