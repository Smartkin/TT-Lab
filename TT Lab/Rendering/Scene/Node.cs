namespace TT_Lab.Rendering.Scene;

public class Node : Renderable
{
    public Node(RenderContext context, Renderable? parent = null, string name = "") : base(context, name)
    {
        parent?.AddChild(this);
    }
}