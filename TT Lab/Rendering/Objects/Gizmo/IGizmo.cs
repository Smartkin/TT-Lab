using GlmSharp;
using TT_Lab.Rendering.Scene;

namespace TT_Lab.Rendering.Objects.Gizmo;

public enum GizmoType
{
    Selection,
    Translate,
    Rotate,
    Scale,
    Custom,
        
    TotalGizmos
}

public interface IGizmo
{
    Node RenderNode { get; }
    
    void Show();
    void Hide();
    void HighlightAxis(TransformAxis axis);
    void ChangeLocalityRender(TransformLocality locality);
    GizmoType GetGizmoType();
    string GetGizmoName();

    void AttachGizmo(Renderable renderable)
    {
        renderable.AddChild(RenderNode);
    }

    void DetachGizmo()
    {
        RenderNode.Parent?.RemoveChild(RenderNode);
    }
    
    protected class GizmoNode
    {
        public Node Node;
        public vec3 DefaultScale = new(1, 1, 1);
    }
}