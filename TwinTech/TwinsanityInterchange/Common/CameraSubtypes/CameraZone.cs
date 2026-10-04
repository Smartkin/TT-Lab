using System;
using System.IO;
using System.Runtime.Serialization;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace Twinsanity.TwinsanityInterchange.Common.CameraSubtypes
{
    /// <summary>
    /// Two flat boxes: where the target is within <see cref="TargetBox"/>, across and along it, is where the camera goes within
    /// <see cref="CameraBox"/> (CameraZone::At: BoxFractionsOf finds the target's fractions, BoxPointAtFractions places the camera
    /// by them). A box is 5 vectors: the plane it lies in (its normal, across × along, with a leftover W; never read), its across and
    /// along axes (unit), the corner it starts at and its size, X across and Y along (Z and W never read).
    /// </summary>
    public class CameraZone : CameraSubBase
    {
        public const int Normal = 0;
        public const int Across = 1;
        public const int Along = 2;
        public const int Origin = 3;
        public const int Size = 4;
        /// <summary>
        /// How thick the flat box is as a matrix, which then keeps an inverse
        /// </summary>
        public const Single Thickness = 0.05f;

        public Vector4[] CameraBox { get; set; }
        public Vector4[] TargetBox { get; set; }

        public CameraZone()
        {
            Follow = 0;
            CameraBox = DefaultBox();
            TargetBox = DefaultBox();
        }

        // Lying flat like the game's zones: across Z, along X
        private static Vector4[] DefaultBox()
        {
            return new[] { new Vector4(0, 1, 0, 0), new Vector4(0, 0, 1, 1), new Vector4(1, 0, 0, 1), new Vector4(0, 0, 0, 1), new Vector4(1, 1, 0, 0) };
        }

        /// <summary>
        /// The camera box as a matrix: the across axis times its size, the thickness below the plane and the along axis times its size
        /// as its columns, the corner as its translation. Setting it puts the axes, corner and sizes back with the plane's normal
        /// following them, the leftovers stay.
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
            // Along × across keeps the matrix right-handed, it points away from the plane's normal
            var thickness = Normalized(Cross(box[Along], box[Across]));
            return new Matrix4
            {
                Column1 = Scaled(box[Across], size.X),
                Column2 = Scaled(thickness, Thickness),
                Column3 = Scaled(box[Along], size.Y),
                Column4 = new Vector4(box[Origin].X, box[Origin].Y, box[Origin].Z, 1)
            };
        }

        public static void FromTransform(Vector4[] box, Matrix4 transform)
        {
            var (across, sizeX) = Unit(transform.Column1, box[Across]);
            var (along, sizeY) = Unit(transform.Column3, box[Along]);
            box[Across] = across;
            box[Along] = along;
            var normal = Normalized(Cross(across, along));
            if (normal.X != 0 || normal.Y != 0 || normal.Z != 0)
            {
                box[Normal] = new Vector4(normal.X, normal.Y, normal.Z, box[Normal].W);
            }

            box[Origin] = new Vector4(transform.Column4.X, transform.Column4.Y, transform.Column4.Z, box[Origin].W);
            box[Size] = new Vector4(sizeX, sizeY, box[Size].Z, box[Size].W);
        }

        private static Vector4 Cross(Vector4 a, Vector4 b)
        {
            return new Vector4(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X, 0);
        }

        private static Vector4 Normalized(Vector4 vector)
        {
            var length = (Single)Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
            return length < 1e-8f ? new Vector4(0, 0, 0, 0) : new Vector4(vector.X / length, vector.Y / length, vector.Z / length, 0);
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
