namespace TT_Lab.Rendering.Materials;

public class TwinMaterialAlphaBlendingOverride : MaterialPropertyOverrider
{
    public bool AlphaBlendingEnabled { get; set; } = false;
    
    public override void Override(Material material)
    {
        var twinMaterial = (TwinMaterial)material;
        twinMaterial.ApplyAlphaBlending(AlphaBlendingEnabled);
    }

    public override void UnOverride(Material material)
    {
        var twinMaterial = (TwinMaterial)material;
        twinMaterial.ApplyAlphaBlending(twinMaterial.GetCurrentMaterialDesc().AlphaBlend.Equals(1.0f));
    }
}