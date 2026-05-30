using System;
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

    public override String ToString()
    {
        var r = (Byte)Math.Round(Color.X * 255.0f);
        var g = (Byte)Math.Round(Color.Y * 255.0f);
        var b = (Byte)Math.Round(Color.Z * 255.0f);
        var a = (Byte)Math.Round(Color.W * 255.0f);
        return $"{Position.X} {Position.Y} {Position.Z} {UV.X} {UV.Y} {UV.Z} {r} {g} {b} {a} {EmitColor.X} {EmitColor.Y} {EmitColor.Z} {EmitColor.W}";
    }
}