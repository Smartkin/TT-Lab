using System;
using System.IO;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace Twinsanity.TwinsanityInterchange.Common;

public class VectorCharacterData : ITwinSerializable
{
    public Vector2 PageUv { get; set; }
    public Vector2 Size { get; set; }
    public Byte FontPageSpecifier { get; set; } = 0;
    
    public VectorCharacterData()
    {
        PageUv = new Vector2();
        Size = new Vector2();
    }
    
    public VectorCharacterData(Vector4 data)
    {
        GetDataFromVector(data);
    }
    
    public void Read(BinaryReader reader, int length)
    {
        var data = new Vector4();
        data.Read(reader, Constants.SIZE_VECTOR4);
        GetDataFromVector(data);
    }

    public void Write(BinaryWriter writer)
    {
        var vec = GetVectorFromData();
        vec.Write(writer);
    }

    public void Compile()
    {
    }

    public Int32 GetLength()
    {
        return Constants.SIZE_VECTOR4;
    }

    private Vector4 GetVectorFromData()
    {
        var vec = new Vector4(PageUv.X, PageUv.Y, Size.X, Size.Y);
        vec = vec.Divide(SCALE);
        
        var binX = (uint)(vec.GetBinaryX() & ~0x3) | FontPageSpecifier;
        vec.SetBinaryX(binX);
        return vec;
    }

    private void GetDataFromVector(Vector4 data)
    {
        var trueX = (uint)(data.GetBinaryX() & ~0x3);
        var dummyVec = new Vector4();
        dummyVec.SetBinaryX(trueX);
        PageUv = new Vector2
        {
            X = dummyVec.X * SCALE,
            Y = data.Y * SCALE,
        };

        Size = new Vector2
        {
            X = data.Z * SCALE,
            Y = data.W * SCALE,
        };

        FontPageSpecifier = (byte)(data.GetBinaryX() & 0x3);
    }

    private const float SCALE = 256.0f / 4095.9375f;
}