using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

public class PrimitivePass(RenderContext context, string name, ShaderProgram program, TwinShader.Type type) : RenderPass(context, name, program, type)
{
    public override void EndPass()
    {
    }
}