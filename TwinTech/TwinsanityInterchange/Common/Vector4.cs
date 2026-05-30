using System;
using System.Diagnostics;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public class Vector4 : ITwinSerializable
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }
        public Vector4()
        {
            X = 0.0f;
            Y = 0.0f;
            Z = 0.0f;
            W = 0.0f;
        }
        public Vector4(float X, float Y, float Z, float W)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
            this.W = W;
        }

        public Vector4(Vector3 pos, float W)
        {
            this.X = pos.X;
            this.Y = pos.Y;
            this.Z = pos.Z;
            this.W = W;
        }

        public Vector4(uint packedX, uint packedY, uint packedZ)
        {
            var baseX = packedX & 0xFFFF;
            var baseY = packedY & 0xFFFF;
            var baseZ = packedZ & 0xFFFF;
            var resultQuat = GetPackedRotationXYZ(baseX, baseY, baseZ);
            X = resultQuat.X;
            Y = resultQuat.Y;
            Z = resultQuat.Z;
            W = resultQuat.W;
        }

        public Vector4(Vector4 other)
        {
            X = other.X;
            Y = other.Y;
            Z = other.Z;
            W = other.W;
        }
        public int GetLength()
        {
            return Constants.SIZE_VECTOR4;
        }

        public void Compile()
        {
            return;
        }

        public void Normalize()
        {
            var length = Length();
            Debug.Assert(length > 0);
            X /= length;
            Y /= length;
            Z /= length;
        }

        public Single Length()
        {
            return (Single)Math.Sqrt(X * X + Y * Y + Z * Z);
        }

        public void Read(BinaryReader reader, int length)
        {
            X = reader.ReadSingle();
            Y = reader.ReadSingle();
            Z = reader.ReadSingle();
            W = reader.ReadSingle();
        }

        public void Add(Vector4 vec)
        {
            X += vec.X;
            Y += vec.Y;
            Z += vec.Z;
        }

        public Vector4 Multiply(float value)
        {
            var resVec = new Vector4
            {
                X = X * value,
                Y = Y * value,
                Z = Z * value,
                W = W * value
            };
            return resVec;
        }

        public Vector4 Divide(float value)
        {
            Debug.Assert(Math.Abs(value) > 0.000001f, "Division value can't be 0");
            var resVec = new Vector4()
            {
                X = X / value,
                Y = Y / value,
                Z = Z / value,
                W = W / value
            };
            return resVec;
        }

        public static Vector4 operator +(Vector4 v1, Vector4 v2)
        {
            return new Vector4(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z, v1.W + v2.W);
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(X);
            writer.Write(Y);
            writer.Write(Z);
            writer.Write(W);
        }
        public void SetBinaryX(UInt32 src)
        {
            X = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public void SetBinaryY(UInt32 src)
        {
            Y = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public void SetBinaryZ(UInt32 src)
        {
            Z = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public void SetBinaryW(UInt32 src)
        {
            W = BitConverter.ToSingle(BitConverter.GetBytes(src), 0);
        }
        public UInt32 GetBinaryX()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(X), 0);
        }
        public UInt32 GetBinaryY()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(Y), 0);
        }
        public UInt32 GetBinaryZ()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(Z), 0);
        }
        public UInt32 GetBinaryW()
        {
            return BitConverter.ToUInt32(BitConverter.GetBytes(W), 0);
        }
        public override String ToString()
        {
            return $"({X:0.00000}; {Y:0.00000}; {Z:0.00000}; {W:0.00000})";
        }
        public Color GetColor()
        {
            var c = new Color
            {
                R = (Byte)(X * 255),
                G = (Byte)(Y * 255),
                B = (Byte)(Z * 255),
                A = (Byte)(W * 255),
                AlphaBlendFlag = StoresColorWithAlphaBlend
            };
            return c;
        }
        public Boolean StoresColorWithAlphaBlend { get; set; }
        public static Vector4 FromColor(Color c)
        {
            var vec = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            vec.StoresColorWithAlphaBlend = c.AlphaBlendFlag;
            return vec;
        }

        public static (float, float) GetCosSin(uint packedValue)
        {
            var halfPacked = (int)(packedValue * 0.5f);
            var biasAngle = halfPacked + 8;
            var coarseIndex = (biasAngle >> 4) & 0x3FF;
            var fineOffset = biasAngle & 0xF;
            var quadrant = (biasAngle >> 4) & 0xC00;

            if (quadrant is 0x400 or 0x800 or 0xC00)
            {
                if (quadrant is 0x400 or 0xC00)
                {
                    coarseIndex = 0x400 - coarseIndex;
                    fineOffset = 7 - fineOffset;
                }
                else
                {
                    fineOffset -= 8;
                }
            }
            else
            {
                fineOffset -= 8;
            }

            var angleBase = coarseIndex * 1.5707964f * (1.0f / 1024.0f);
            var cosBase = (float)Math.Cos(angleBase);
            var sinBase = (float)Math.Sin(angleBase);

            Single resultX;
            Single resultY;
            if (fineOffset == 0)
            {
                resultX = cosBase;
                resultY = sinBase;
            }
            else
            {
                var delta = 2 * (float)Math.PI * (1.0f / 4096.0f) * 0.0625f * fineOffset;

                resultX = cosBase - delta * sinBase;
                resultY = sinBase + delta * cosBase;
            }

            switch (quadrant)
            {
                case 0x400:
                    resultX = -resultX;
                    break;
                case 0x800:
                    resultX = -resultX;
                    resultY = -resultY;
                    break;
                case 0xC00:
                    resultY = -resultY;
                    break;
            }
            
            return (resultX, resultY);
        }
        
        public static uint GetAngle(float a, float b)
        {

            uint result = 0;
            uint biasAngle = 0;
            uint coarseIndex = (biasAngle >> 4) & 0x03FF;
            uint fineOffset = biasAngle & 0xF;
            uint quadrant = (biasAngle >> 4) & 0x0C00;

            var valX = a;
            var valY = b;
            if (valX < 0 && valY < 0)
            {
                quadrant = 0x800;
                valX = -valX;
                valY = -valY;
            } else if (valY < 0)
            {
                quadrant = 0xC00;
                valY = -valY;
            } else
            {
                quadrant = 0x400;
                valX = -valX;
            }


            float delta = 0;
            fineOffset = 0;
            var angleBase = MathF.Acos(valX);
            coarseIndex = (uint)(angleBase * 1024.0f / 1.5707964f);
            if (quadrant is 0x400 or 0xC00)
            {
                coarseIndex = 0x400 - coarseIndex;
            }

            biasAngle = 0xFFFF0000 | ((quadrant & 0x0C00) << 4 ) | ((coarseIndex & 0x03FF) << 4) | fineOffset & 0xF;
            result = biasAngle - 8;
            result *= 2;
            return result;
        }



        private static Vector4 GetPackedRotationYZ(uint baseY, uint baseZ)
        {
            if (baseY == 0)
            {
                if (baseZ == 0)
                {
                    return new Vector4(0, 0, 0, 1);
                }
                
                var components = GetCosSin(baseZ);
                return new Vector4(0, 0, components.Item1, components.Item2);
            }
            
            if (baseZ == 0)
            {
                var components = GetCosSin(baseY);
                return new Vector4(0, components.Item1, 0, components.Item2);
            }
            
            var componentsY = GetCosSin(baseY);
            var componentsZ = GetCosSin(baseZ);

            return new Vector4(-componentsY.Item2 * componentsZ.Item2, componentsY.Item2 * componentsZ.Item1,
                componentsY.Item1 * componentsZ.Item2, componentsY.Item2 * componentsZ.Item1);
        }

        private static Vector4 GetPackedRotationXZ(uint baseX, uint baseZ)
        {
            if (baseX == 0)
            {
                if (baseZ == 0)
                {
                    return new Vector4(0, 0, 0, 1);
                }
                
                var components = GetCosSin(baseZ);
                return new Vector4(0, 0, components.Item1, components.Item2);
            }

            if (baseZ == 0)
            {
                var components = GetCosSin(baseX);
                return new Vector4(components.Item1, 0, 0, components.Item2);
            }
            
            var componentsX = GetCosSin(baseX);
            var componentsZ = GetCosSin(baseZ);

            return new Vector4(componentsX.Item2 * componentsZ.Item1, componentsX.Item2 * componentsZ.Item2,
                componentsX.Item1 * componentsZ.Item2, componentsX.Item1 * componentsZ.Item1);
        }

        private static Vector4 GetPackedRotationXY(uint baseX, uint baseY)
        {
            if (baseX == 0)
            {
                if (baseY == 0)
                {
                    return new Vector4(0, 0, 0, 1);
                }
                
                var components = GetCosSin(baseY);
                return new Vector4(0, components.Item1, 0, components.Item2);
            }

            if (baseY == 0)
            {
                var components = GetCosSin(baseX);
                return new Vector4(components.Item1, 0, 0, components.Item2);
            }
            
            var componentsX = GetCosSin(baseX);
            var componentsY = GetCosSin(baseY);

            return new Vector4(componentsX.Item2 * componentsY.Item1, componentsX.Item1 * componentsY.Item2,
                -componentsX.Item2 * componentsY.Item2, componentsX.Item1 * componentsY.Item1);
        }

        private static Vector4 GetPackedRotationXYZ(uint baseX, uint baseY, uint baseZ)
        {
            if (baseX == 0)
            {
                return GetPackedRotationYZ(baseY, baseZ);
            }

            if (baseY == 0)
            {
                return GetPackedRotationXZ(baseX, baseZ);
            }

            if (baseZ == 0)
            {
                return GetPackedRotationXY(baseX, baseY);
            }
            
            var componentsX = GetCosSin(baseX);
            var componentsY = GetCosSin(baseY);
            var componentsZ = GetCosSin(baseZ);

            return new Vector4(componentsZ.Item1 * componentsX.Item2 * componentsY.Item1 - componentsZ.Item2 * componentsX.Item1 * componentsY.Item1,
                                componentsZ.Item2 * componentsX.Item2 * componentsY.Item1 + componentsZ.Item1 * componentsX.Item1 * componentsY.Item2,
                                componentsZ.Item2 * componentsX.Item1 * componentsY.Item1 - componentsZ.Item1 * componentsX.Item2 * componentsY.Item2,
                                componentsZ.Item1 * componentsX.Item1 * componentsY.Item1 + componentsZ.Item2 * componentsX.Item2 * componentsY.Item2);
        }

        private String DebuggerDisplay
        {
            get => $"x,y,z,w = {X},{Y},{Z},{W}; BinX, BinY, BinZ, BinW = {GetBinaryX():X}, {GetBinaryY():X}, {GetBinaryZ():X}, {GetBinaryW():X}";
        }
    }
}
