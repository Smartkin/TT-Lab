using System;
using System.Collections.Generic;
using System.IO;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Graphics.SubModels;

public class Vertex : IEquatable<Vertex>
{
    public Vertex()
    {
        Position = new Vector4();
        Color = new Vector4();
        UV = new Vector4();
        _normal = new Vector4();
        _emitColor = new Vector4();
        JointInfo = new VertexJointInfo();
    }
    public Vertex(Vector4 pos) : this()
    {
        Position.X = pos.X;
        Position.Y = pos.Y;
        Position.Z = pos.Z;
        Position.W = pos.W;
    }
    public Vertex(Vector4 pos, Vector4 color) : this(pos)
    {
        Color.X = color.X;
        Color.Y = color.Y;
        Color.Z = color.Z;
        Color.W = color.W;
        AlphaBlendingBit = color.StoresColorWithAlphaBlend;
    }
    public Vertex(Vector4 pos, Vector4 color, Vector4 uv) : this(pos, color)
    {
        UV.X = uv.X;
        UV.Y = uv.Y;
        UV.Z = uv.Z;
        UV.W = uv.W;
    }
    public Vertex(Vector4 pos, Vector4 color, Vector4 uv, Vector4 emitColor) : this(pos, color, uv)
    {
        EmitColor.X = emitColor.X;
        EmitColor.Y = emitColor.Y;
        EmitColor.Z = emitColor.Z;
        EmitColor.W = emitColor.W;
    }
    public Vector4 Position { get; set; }
    public Vector4 Color { get; set; }
    public Vector4 Normal
    {
        get => _normal;
        set
        {
            _normal = value;
            HasNormals = true;
        }
    }
    public Vector4 EmitColor
    {
        get => _emitColor;
        set
        {
            _emitColor = value;
            HasEmitColor = true;
        }
    }
    public Vector4 UV { get; set; }
    public VertexJointInfo JointInfo { get; set; }

    public void WriteBinary(BinaryWriter bw)
    {
        bw.Write(Position.X);
        bw.Write(Position.Y);
        bw.Write(Position.Z);
        bw.Write(Position.W);
        bw.Write(UV.X);
        bw.Write(UV.Y);
        bw.Write(UV.Z);
        bw.Write(UV.W);
        bw.Write(Color.X);
        bw.Write(Color.Y);
        bw.Write(Color.Z);
        bw.Write(Color.W);
        bw.Write(AlphaBlendingBit);
        if (HasNormals)
        {
            bw.Write(Normal.X);
            bw.Write(Normal.Y);
            bw.Write(Normal.Z);
            bw.Write(Normal.W);
        }

        if (HasEmitColor)
        {
            bw.Write(EmitColor.X);
            bw.Write(EmitColor.Y);
            bw.Write(EmitColor.Z);
            bw.Write(EmitColor.W);
        }
        
        JointInfo.Write(bw);
    }

    public bool HasNormals { get; private set; }
    public static IEqualityComparer<Vertex> ExactComparer { get; } = new BitwiseComparer();
    public bool HasEmitColor { get; private set; }
    public bool AlphaBlendingBit { get; set; }

    private Vector4 _normal;
    private Vector4 _emitColor;

    public Boolean Equals(Vertex? other)
    {
        if (other is null)
        {
            return false;
        }

        return IsFloatEqual(Position.X, other.Position.X) &&
               IsFloatEqual(Position.Y, other.Position.Y) &&
               IsFloatEqual(Position.Z, other.Position.Z) &&
               IsFloatEqual(UV.X, other.UV.X) &&
               IsFloatEqual(UV.Y, other.UV.Y) &&
               IsFloatEqual(Normal.X, other.Normal.X) &&
               IsFloatEqual(Normal.Y, other.Normal.Y) &&
               IsFloatEqual(Normal.Z, other.Normal.Z) &&
               IsFloatEqual(JointInfo.Weight1, other.JointInfo.Weight1) &&
               IsFloatEqual(JointInfo.Weight2, other.JointInfo.Weight2) &&
               IsFloatEqual(JointInfo.Weight3, other.JointInfo.Weight3) &&
               JointInfo.JointIndex1 == other.JointInfo.JointIndex1 &&
               JointInfo.JointIndex2 == other.JointInfo.JointIndex2 &&
               JointInfo.JointIndex3 == other.JointInfo.JointIndex3;

        static bool IsFloatEqual(float f1, float f2) => Math.Abs(f1 - f2) < 10e-9f;
    }

    // Everything the game stores for the vertex compared bit for bit, the connection flag belongs to where the vertex is in a strip so it's left out
    private sealed class BitwiseComparer : IEqualityComparer<Vertex>
    {
        public Boolean Equals(Vertex? x, Vertex? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return SameBits(x.Position, y.Position) && SameBits(x.UV, y.UV) && SameBits(x.Color, y.Color) &&
                   x.Color.StoresColorWithAlphaBlend == y.Color.StoresColorWithAlphaBlend && x.AlphaBlendingBit == y.AlphaBlendingBit &&
                   x.HasNormals == y.HasNormals && (!x.HasNormals || SameBits(x.Normal, y.Normal)) &&
                   x.HasEmitColor == y.HasEmitColor && (!x.HasEmitColor || SameBits(x.EmitColor, y.EmitColor) && x.EmitColor.StoresColorWithAlphaBlend == y.EmitColor.StoresColorWithAlphaBlend) &&
                   SameJoints(x.JointInfo, y.JointInfo);
        }

        public Int32 GetHashCode(Vertex vertex)
        {
            return HashCode.Combine(vertex.Position.GetBinaryX(), vertex.Position.GetBinaryY(), vertex.Position.GetBinaryZ(), vertex.UV.GetBinaryX(),
                vertex.UV.GetBinaryY(), vertex.Color.GetBinaryX(), vertex.JointInfo.JointIndex1);
        }

        private static Boolean SameBits(Vector4 a, Vector4 b)
        {
            return a.GetBinaryX() == b.GetBinaryX() && a.GetBinaryY() == b.GetBinaryY() && a.GetBinaryZ() == b.GetBinaryZ() && a.GetBinaryW() == b.GetBinaryW();
        }

        private static Boolean SameJoints(VertexJointInfo a, VertexJointInfo b)
        {
            return BitConverter.SingleToUInt32Bits(a.Weight1) == BitConverter.SingleToUInt32Bits(b.Weight1) &&
                   BitConverter.SingleToUInt32Bits(a.Weight2) == BitConverter.SingleToUInt32Bits(b.Weight2) &&
                   BitConverter.SingleToUInt32Bits(a.Weight3) == BitConverter.SingleToUInt32Bits(b.Weight3) &&
                   a.JointIndex1 == b.JointIndex1 && a.JointIndex2 == b.JointIndex2 && a.JointIndex3 == b.JointIndex3 &&
                   a.WeightsAmount == b.WeightsAmount;
        }
    }

    public Vertex Clone()
    {
        var clone = new Vertex(Position, Color, UV)
        {
            AlphaBlendingBit = AlphaBlendingBit,
            JointInfo = new VertexJointInfo
            {
                Weight1 = JointInfo.Weight1,
                Weight2 = JointInfo.Weight2,
                Weight3 = JointInfo.Weight3,
                JointIndex1 = JointInfo.JointIndex1,
                JointIndex2 = JointInfo.JointIndex2,
                JointIndex3 = JointInfo.JointIndex3,
                WeightsAmount = JointInfo.WeightsAmount,
                Connection = JointInfo.Connection
            }
        };
        clone.Color.StoresColorWithAlphaBlend = Color.StoresColorWithAlphaBlend;
        if (HasNormals)
        {
            clone.Normal = new Vector4(Normal);
        }

        if (HasEmitColor)
        {
            clone.EmitColor = new Vector4(EmitColor) { StoresColorWithAlphaBlend = EmitColor.StoresColorWithAlphaBlend };
        }

        return clone;
    }

    /// <summary>
    /// The normal to light the vertex with, the game's normals aren't always normalized and some are zero
    /// </summary>
    public Vector4 GetUnitNormal()
    {
        var length = Normal.Length();
        if (!HasNormals || length < 1e-6f || Single.IsNaN(length))
        {
            return new Vector4(0, 1, 0, 0);
        }

        return new Vector4(Normal.X / length, Normal.Y / length, Normal.Z / length, 0);
    }

    public override String ToString()
    {
        var r = (Byte)Math.Round(Color.X * 255.0f);
        var g = (Byte)Math.Round(Color.Y * 255.0f);
        var b = (Byte)Math.Round(Color.Z * 255.0f);
        var a = (Byte)Math.Round(Color.W * 255.0f);
        return $"{Position.X} {Position.Y} {Position.Z} {UV.X} {UV.Y} {UV.Z} {r} {g} {b} {a} {EmitColor.X} {EmitColor.Y} {EmitColor.Z} {EmitColor.W}";
    }
}