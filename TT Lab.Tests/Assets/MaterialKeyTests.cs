using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using Twinsanity.TwinsanityInterchange.Common;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.Tests.Assets;

// The game loads a material's VU1 programs by its applied shaders, a bit per type of shader it has, so building writes them from the
// shaders the material has now
public class MaterialKeyTests
{
    private static MaterialData Material(AppliedShaders stored, params TwinShader.Type[] types)
    {
        return new MaterialData(null!)
        {
            ActivatedShaders = stored,
            Shaders = types.Select(type => new LabShader { ShaderType = type }).ToList(),
        };
    }

    [Fact]
    public void AppliedShadersAreTheShadersTypes()
    {
        // Pairs the retail materials have, every one of the game's has exactly the bits of its shaders
        Assert.Equal(AppliedShaders.StandardLit | AppliedShaders.LitEnvironmentMap,
            Material(AppliedShaders.StandardLit, TwinShader.Type.StandardLit, TwinShader.Type.LitEnvironmentMap).DeriveActivatedShaders());
        Assert.Equal((AppliedShaders)0x8008, Material(0, TwinShader.Type.StandardUnlit, TwinShader.Type.UnlitEnvironmentMap).DeriveActivatedShaders());
        Assert.Equal((AppliedShaders)0x10000000, Material(AppliedShaders.StandardLit, TwinShader.Type.UiShader).DeriveActivatedShaders());
        // A billboard loads the unlit programs
        Assert.Equal(AppliedShaders.StandardUnlit, Material(0, TwinShader.Type.UnlitBillboard).DeriveActivatedShaders());
        // No retail material has these types, their bits aren't known
        Assert.Equal((AppliedShaders)0x1234, Material((AppliedShaders)0x1234, TwinShader.Type.SHADER_17).DeriveActivatedShaders());
    }
}
