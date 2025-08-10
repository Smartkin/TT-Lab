using TT_Lab.Rendering.Shaders;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Passes;

public abstract class RenderPass(RenderContext context, string name, ShaderProgram program, TwinShader.Type type)
{
    protected RenderContext Context { get; } = context;

    public virtual bool StartPass()
    {
        if (Context.CurrentPass?.Program == Program && !Context.Invalidated)
        {
            Context.ChangePass(this);
            return false;
        }
        
        Context.Validate();
        Context.ChangePass(this);
        program.Use();
        return true;
    }

    public abstract void EndPass();

    public TwinShader.Type ShaderType => type;
    public string Name { get; } = name;
    public ShaderProgram Program => program;
}