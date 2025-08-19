using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

public class BillboardPass(RenderContext context, string name, ShaderProgram program) : RenderPass(context, name, program, TwinShader.Type.UnlitBillboard)
{
    public override void EndPass()
    {
        
    }
}