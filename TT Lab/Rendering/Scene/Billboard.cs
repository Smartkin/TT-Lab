namespace TT_Lab.Rendering.Scene;

public class Billboard : Renderable
{
    public Billboard(RenderContext context, string name = "") : base(context, name)
    {
    }

    public bool IsHighlighted { get; set; }

    internal BillboardSet? Set { get; set; }

    /// <summary>
    /// Billboards get drawn by their set, which keeps drawing them until they are released
    /// </summary>
    public void Release()
    {
        Set?.RemoveBillboard(this);
        Set = null;
    }
}
