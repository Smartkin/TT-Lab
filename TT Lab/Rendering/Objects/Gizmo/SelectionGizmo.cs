using System;
using GlmSharp;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;

namespace TT_Lab.Rendering.Objects.Gizmo;

public class SelectionGizmo : Gizmo
{
    private readonly IGizmo.GizmoNode[] _selectionGizmo = new IGizmo.GizmoNode[1];
    
    public SelectionGizmo(RenderContext renderContext, EditingContext editingContext) : base(renderContext, editingContext)
    {
        _selectionGizmo[0] = new IGizmo.GizmoNode
        {
            DefaultScale = new vec3(0.25f, 0.25f, 0.25f)
        };
        
        var buffer = BufferGeneration.GetCubeBuffer(renderContext).Model!;
        buffer.Diffuse = new vec4(1.0f, 0.0f, 0.0f, 1.0f);
        var gizmoRootNode = new Node(renderContext, RenderNode);
        gizmoRootNode.AddChild(buffer);
        gizmoRootNode.SetInitialScale(_selectionGizmo[0].DefaultScale);
        gizmoRootNode.ResetLocalTransform();

        _selectionGizmo[0].Node = gizmoRootNode;
    }

    public override GizmoType GetGizmoType() => GizmoType.Selection;
    public override String GetGizmoName()
    {
        return "SelectionGizmo";
    }
    
    protected override bool SupportsMultipleAxis => false;

    

    protected override void ResetGizmoTransforms()
    {
        foreach (var gizmo in _selectionGizmo)
        {
            gizmo.Node.ResetLocalTransform();
        }
    }

    protected override IGizmo.GizmoNode[] GetGizmoNodes()
    {
        return _selectionGizmo;
    }
}