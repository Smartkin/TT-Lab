using GlmSharp;

namespace TT_Lab.Rendering.Scene;

public class BillboardSet : Renderable
{
    public BillboardSet(RenderContext context, string name = "") : base(context, name)
    {
    }

    public Billboard CreateBillboard(float x, float y, float z)
    {
        var billboard = new Billboard(Context);
        billboard.SetPosition(new vec3(x, y, z));
        return billboard;
    }
}