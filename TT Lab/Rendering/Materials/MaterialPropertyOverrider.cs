namespace TT_Lab.Rendering.Materials;

public abstract class MaterialPropertyOverrider
{
    public abstract void Override(Material material);
    public abstract void UnOverride(Material material);
}