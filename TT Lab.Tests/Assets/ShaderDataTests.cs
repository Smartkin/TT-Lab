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
            LeftoverVector = FromBits(0xFFFF00FF, 0xFFFF50FF, 0xFFFFFFFF, 0xD1D2FFFF),
            ShaderColor = FromBits(0x3F800000, 0x7F800001, 0x80000000, 0x42800000),
            UvScrollSpeed = FromBits(0x00000001, 0x00000101, 0x00000001, 0x00050040)
        };

        var copy = JsonConvert.DeserializeObject<LabShader>(JsonConvert.SerializeObject(shader))!;

        Assert.Equal(Bits(shader.LeftoverVector), Bits(copy.LeftoverVector));
        Assert.Equal(Bits(shader.ShaderColor), Bits(copy.ShaderColor));
        Assert.Equal(Bits(shader.UvScrollSpeed), Bits(copy.UvScrollSpeed));
    }
}
