using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// A path (the same uniform cubic B-spline as the AI paths, with the same parameters: every segment's arc length from the start
    /// then 1 over its steps) the camera slides along to the point nearest the target, <see cref="CameraSubBase.Offset"/> units
    /// further (FUN_0027d6e8, FUN_0027d770).
    /// </summary>
    public class CameraPath : CameraSubBase
    {
        public List<Vector4> PathPoints { get; set; }
        /// <summary>
        /// The segments' arc lengths from the start, then 1 over the steps each takes, one pair after the other like a path's
        /// </summary>
        public List<Vector2> Parameters { get; set; }

        public CameraPath()
        {
            PathPoints = new List<Vector4>();
            Parameters = new List<Vector2>();
        }

        public override int GetLength()
        {
            return base.GetLength() + 4 + PathPoints.Count * Constants.SIZE_VECTOR4 + 4 + Parameters.Count * Constants.SIZE_VECTOR2;
        }

        public override void Read(BinaryReader reader, int length)
        {
            base.Read(reader, base.GetLength());
            int cnt1 = reader.ReadInt32();
            PathPoints.Clear();
            for (int i = 0; i < cnt1; ++i)
            {
                Vector4 vec = new Vector4();
                vec.Read(reader, Constants.SIZE_VECTOR4);
                PathPoints.Add(vec);
            }

            int cnt2 = reader.ReadInt32();
            Parameters.Clear();
            for (var i = 0; i < cnt2; ++i)
            {
                Vector2 parameter = new Vector2();
                parameter.Read(reader, Constants.SIZE_VECTOR2);
                Parameters.Add(parameter);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            base.Write(writer);
            writer.Write(PathPoints.Count);
            foreach (var point in PathPoints)
            {
                point.Write(writer);
            }

            writer.Write(Parameters.Count);
            foreach (var parameter in Parameters)
            {
                parameter.Write(writer);
            }
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraPath;
        }
    }
}
