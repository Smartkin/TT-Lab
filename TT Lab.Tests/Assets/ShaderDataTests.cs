using Newtonsoft.Json;
using TT_Lab.AssetData.Graphics.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Assets;

public class ShaderDataTests
{
    private static Vector4 FromBits(uint x, uint y, uint z, uint w)
    {
        var vector = new Vector4();
        vector.SetBinaryX(x);
        vector.SetBinaryY(y);
        vector.SetBinaryZ(z);
        vector.SetBinaryW(w);
        return vector;
    }

    private static uint[] Bits(Vector4 vector) => [vector.GetBinaryX(), vector.GetBinaryY(), vector.GetBinaryZ(), vector.GetBinaryW()];

    // The HUD's clock material keeps bytes in its vectors, NaNs JSON turned into the same one and the clock came out upside down
    [Fact]
    public void ShaderVectorsKeepTheirBits()
    {
        var shader = new LabShader
        {
            UnkVector1 = FromBits(0xFFFF00FF, 0xFFFF50FF, 0xFFFFFFFF, 0xD1D2FFFF),
            UnkVector2 = FromBits(0x3F800000, 0x7F800001, 0x80000000, 0x42800000),
            UvScrollSpeed = FromBits(0x00000001, 0x00000101, 0x00000001, 0x00050040)
        };

        var copy = JsonConvert.DeserializeObject<LabShader>(JsonConvert.SerializeObject(shader))!;

        Assert.Equal(Bits(shader.UnkVector1), Bits(copy.UnkVector1));
        Assert.Equal(Bits(shader.UnkVector2), Bits(copy.UnkVector2));
        Assert.Equal(Bits(shader.UvScrollSpeed), Bits(copy.UvScrollSpeed));
    }

    [Fact]
    public void ShaderVectorsSavedAsFloatsStillLoad()
    {
        var copy = JsonConvert.DeserializeObject<LabShader>("""{"UnkVector2": {"X": 1.0, "Y": 2.5, "Z": 0.0, "W": 64.0}}""")!;

        Assert.Equal((1.0f, 2.5f, 0.0f, 64.0f), (copy.UnkVector2.X, copy.UnkVector2.Y, copy.UnkVector2.Z, copy.UnkVector2.W));
    }
}
