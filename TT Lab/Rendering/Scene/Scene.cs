using System;
using GlmSharp;
using TT_Lab.Rendering.Objects;

namespace TT_Lab.Rendering.Scene;

public class Scene : Renderable
{
    public Camera Camera { get; }
    
    public Scene(RenderContext context, string name = "") : base(context, name)
    {
        Camera = new Camera(context);
    }

    public override void UpdateRenderTransform()
    {
        Camera.UpdateRenderTransform();
        
        base.UpdateRenderTransform();
    }

    public void UpdateResolution(vec2 resolution)
    {
        Camera.SetResolution(resolution);
    }

    protected override void RenderSelf(float delta)
    {
        Camera.Render(delta);
    }
}