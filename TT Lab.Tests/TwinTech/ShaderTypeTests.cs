using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.TwinTech;

// Some shader types have values of their own before the settings (the decomp's ShaderType*Read): the screen copies a corner value,
// the cloth deformations a mode, speed and amplitudes, the waves a speed and an amplitude. A type read without them misread the rest
public sealed class ShaderTypeTests
{
    [Theory]
    [InlineData(TwinShader.Type.ScreenCopy, 1)]
    [InlineData(TwinShader.Type.WaveDeformation, 2)]
    [InlineData(TwinShader.Type.SHADER_17, 1)]
    public void TheirOwnValuesComeBack(TwinShader.Type type, int values)
    {
        var shader = new TwinShader { ShaderType = type, FloatParam = [1.5f, -2.25f, 0, 0], LodParamK = 0xFFBB, TextureId = 0x1234 };
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        shader.Write(writer);
        writer.Flush();

        Assert.Equal(shader.GetLength(), stream.Length);
        stream.Position = 0;
        var read = new TwinShader();
        read.Read(new BinaryReader(stream), (int)stream.Length);

        Assert.Equal(type, read.ShaderType);
        Assert.Equal(shader.FloatParam.Take(values), read.FloatParam.Take(values));
        // What follows them is where it was
        Assert.Equal((0xFFBB, 0x1234U), (read.LodParamK, read.TextureId));
    }
}
