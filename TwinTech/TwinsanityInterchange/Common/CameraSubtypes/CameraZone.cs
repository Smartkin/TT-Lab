using System;
using System.IO;
using System.Runtime.Serialization;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// Two boxes: where the target is within <see cref="TargetBox"/>, along its X and Z axes, is where the camera goes within
    /// <see cref="CameraBox"/> (FUN_0027dd48: FUN_00189f90 finds the target's fractions, FUN_0018a658 places the camera by them).
    /// A box is 5 vectors: its X, Y and Z axes (unit, the first's W a leftover), the corner it starts at and its size: X along the X
    /// axis, Y along the Z axis, Z along the Y axis. The Y axis and size are never read.
    /// </summary>
    public class CameraZone : CameraSubBase
    {
        public const int AxisX = 0;
        public const int AxisY = 1;
        public const int AxisZ = 2;
        public const int Origin = 3;
        public const int Size = 4;

        public Vector4[] CameraBox { get; set; }
        public Vector4[] TargetBox { get; set; }

        public CameraZone()
        {
            Follow = 0;
            CameraBox = DefaultBox();
            TargetBox = DefaultBox();
        }

        private static Vector4[] DefaultBox()
        {
            return new[] { new Vector4(1, 0, 0, 1), new Vector4(0, 1, 0, 1), new Vector4(0, 0, 1, 1), new Vector4(0, 0, 0, 1), new Vector4(1, 1, 1, 1) };
        }

        /// <summary>
        /// The camera box as a matrix: the axes scaled by the sizes as its columns, the corner as its translation. Setting it puts
        /// the axes, corner and sizes back, the leftovers in the Ws stay.
        /// </summary>
        [IgnoreDataMember]
        public Matrix4 CameraBoxTransform
        {
            get { return ToTransform(CameraBox); }
            set { FromTransform(CameraBox, value); }
        }

        [IgnoreDataMember]
        public Matrix4 TargetBoxTransform
        {
            get { return ToTransform(TargetBox); }
            set { FromTransform(TargetBox, value); }
        }

        public static Matrix4 ToTransform(Vector4[] box)
        {
            var size = box[Size];
            return new Matrix4
            {
                Column1 = Scaled(box[AxisX], size.X),
                Column2 = Scaled(box[AxisY], size.Z),
                Column3 = Scaled(box[AxisZ], size.Y),
                Column4 = new Vector4(box[Origin].X, box[Origin].Y, box[Origin].Z, 1)
            };
        }

        public static void FromTransform(Vector4[] box, Matrix4 transform)
        {
            var (x, sizeX) = Unit(transform.Column1, box[AxisX]);
            var (y, sizeZ) = Unit(transform.Column2, box[AxisY]);
            var (z, sizeY) = Unit(transform.Column3, box[AxisZ]);
            box[AxisX] = x;
            box[AxisY] = y;
            box[AxisZ] = z;
            box[Origin] = new Vector4(transform.Column4.X, transform.Column4.Y, transform.Column4.Z, box[Origin].W);
            box[Size] = new Vector4(sizeX, sizeY, sizeZ, box[Size].W);
        }

        private static Vector4 Scaled(Vector4 axis, Single size)
        {
            return new Vector4(axis.X * size, axis.Y * size, axis.Z * size, 0);
        }

        private static (Vector4 Axis, Single Length) Unit(Vector4 column, Vector4 previous)
        {
            var length = (Single)Math.Sqrt(column.X * column.X + column.Y * column.Y + column.Z * column.Z);
            if (length < 1e-8f)
            {
                return (new Vector4(previous.X, previous.Y, previous.Z, previous.W), 0);
            }

            return (new Vector4(column.X / length, column.Y / length, column.Z / length, previous.W), length);
        }

        public override int GetLength()
        {
            return 160;
        }

        public override void Read(BinaryReader reader, int length)
        {
            for (var i = 0; i < 5; ++i)
            {
                CameraBox[i] = new Vector4();
                CameraBox[i].Read(reader, Constants.SIZE_VECTOR4);
            }

            for (var i = 0; i < 5; ++i)
            {
                TargetBox[i] = new Vector4();
                TargetBox[i].Read(reader, Constants.SIZE_VECTOR4);
            }
        }

        public override void Write(BinaryWriter writer)
        {
            for (var i = 0; i < 5; ++i)
            {
                CameraBox[i].Write(writer);
            }

            for (var i = 0; i < 5; ++i)
            {
                TargetBox[i].Write(writer);
            }
        }

        public override ITwinCamera.CameraType GetCameraType()
        {
            return ITwinCamera.CameraType.CameraZone;
        }
    }
}
