using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Services;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Tests.Rendering;

// The game paints a sky's parts over each other in the order of its meshes and their parts, blended or not (FUN_001ba350), so they all
// go into one pass by that order
public class SkydomePassTests
{
    private static ModelBuffer Part(int drawOrder, params TwinShader.AlphaBlending[] blending)
    {
        var material = new MaterialData(null!)
        {
            Shaders = blending.Select(value => new LabShader { ShaderType = TwinShader.Type.UnlitSkydome, ABlending = value }).ToList(),
        };
        return new ModelBuffer(null!, null!, null!, material) { DrawOrder = drawOrder };
    }

    [Fact]
    public void SkyPartsDrawInOnePassInTheirOrder()
    {
        var parts = new[]
        {
            Part(0, TwinShader.AlphaBlending.OFF),
            Part(1, TwinShader.AlphaBlending.ON, TwinShader.AlphaBlending.OFF, TwinShader.AlphaBlending.ON),
            Part(2, TwinShader.AlphaBlending.OFF),
            Part(3, TwinShader.AlphaBlending.ON),
        };

        var passes = parts.SelectMany(part => part.GetPriorityPass()).ToList();

        Assert.All(passes, pass => Assert.Equal(PassService.SkydomePassName, pass.Item1));
        Assert.Equal(passes.Select(pass => pass.Item2).Order(), passes.Select(pass => pass.Item2));
        Assert.Equal(passes.Count, passes.Select(pass => pass.Item2).Distinct().Count());
    }

    [Fact]
    public void AForcedShaderKeepsItsOwnPass()
    {
        var material = new MaterialData(null!)
        {
            Shaders = [new LabShader { ShaderType = TwinShader.Type.UnlitSkydome, ABlending = TwinShader.AlphaBlending.ON, ForcedShaderName = "StandardUnlit" }],
        };
        var part = new ModelBuffer(null!, null!, null!, material);

        Assert.Equal("StandardUnlit", Assert.Single(part.GetPriorityPass()).Item1);
    }
}
