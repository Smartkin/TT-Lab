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
    /// bit 0 of <see cref="Flags"/> is set (FUN_0027cfe8, FUN_00279f80).
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
        /// One pair per segment between samples: the arc lengths from the start, then 1 over the steps each takes, like a path's
        /// </summary>
        public List<Vector2> Parameters { get; set; }
        /// <summary>
        /// Bit 0 takes <see cref="CameraSubBase.Offset"/> as the offset along the curve, the rest are leftovers (15 or 0xCDCD in the retail data)
        /// </summary>
        public UInt16 SplineFlags { get; set; }

        public CameraSpline()
        {
            PathPoints = new List<Vector4>();
            Tangents = new List<Vector4>();
            Parameters = new List<Vector2>();
        }

        public override int GetLength()
        {
            return base.GetLength() + 4 + 4 + PathPoints.Count * Constants.SIZE_VECTOR4 + Tangents.Count * Constants.SIZE_VECTOR4 + Parameters.Count * Constants.SIZE_VECTOR2 + 2;
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

            Parameters.Clear();
            for (var i = 0; i < segments; ++i)
            {
                Vector2 parameter = new Vector2();
                parameter.Read(reader, Constants.SIZE_VECTOR2);
                Parameters.Add(parameter);
            }

            SplineFlags = reader.ReadUInt16();
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(Parameters.Count);
            writer.Write(StepLength);
            for (var i = 0; i < PathPoints.Count; ++i)
            {
                PathPoints[i].Write(writer);
                Tangents[i].Write(writer);
            }

            foreach (var parameter in Parameters)
            {
                parameter.Write(writer);
            }

            writer.Write(SplineFlags);
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraSpline;
        }
    }
}
