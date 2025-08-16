using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Materials;

public class TwinMaterialDepthTestOverride : MaterialPropertyOverrider
{
    public TwinShader.DepthTestMethod DepthTestOverride { get; set; } = TwinShader.DepthTestMethod.GEQUAL;
    
    public override void Override(Material material)
    {
        var twinMaterial = (TwinMaterial)material;
        twinMaterial.ApplyDepthWrite(true);
        twinMaterial.ApplyDepthTest(DepthTestOverride);
    }

    public override void UnOverride(Material material)
    {
        var twinMaterial = (TwinMaterial)material;
        twinMaterial.ApplyDepthWrite(twinMaterial.GetCurrentMaterialDesc().DepthWrite);
        twinMaterial.ApplyDepthTest(twinMaterial.GetCurrentMaterialDesc().DepthTest);
    }
}
