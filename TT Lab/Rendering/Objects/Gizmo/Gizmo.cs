using System;
using System.Collections.Generic;
using GlmSharp;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects.Gizmo;

public abstract class Gizmo : IGizmo
{
    protected readonly EditingContext EditingContext;
    
    protected Gizmo(RenderContext renderContext, EditingContext editingContext)
    {
        EditingContext = editingContext;
        
        // ReSharper disable once VirtualMemberCallInConstructor
        RenderNode = new Node(renderContext, editingContext.GetEditorNode(), GetGizmoName());
        RenderNode.SetInheritScale(false);
        RenderNode.SetInheritDiffuse(false);
        RenderNode.IsVisible = false;
    }

    public Node RenderNode { get; }
    
    public void Show()
    {
        RenderNode.IsVisible = true;
    }

    public void Hide()
    {
        RenderNode.IsVisible = false;
    }

    public void HighlightAxis(TransformAxis axis)
    {
        ResetGizmoTransforms();

        if (axis == TransformAxis.NONE)
        {
            return;
        }
        
        var axisNodes = GetGizmoNodes();

        const float upScale = 1.5f;
        
        if (!SupportsMultipleAxis)
        {
            var node = axisNodes[0].Node;
            node.Scale(new vec3(upScale, upScale, upScale));
            return;
        }
        
        switch (axis)
        {
            case TransformAxis.X:
            {
                var node = axisNodes[0].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
            }
                break;
            case TransformAxis.Y:
            {
                var node = axisNodes[1].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
            }
                break;
            case TransformAxis.Z:
            {
                var node = axisNodes[2].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
            }
                break;
            case TransformAxis.XZ:
            {
                var node = axisNodes[0].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
                node = axisNodes[2].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
            }
                break;
            case TransformAxis.XY:
            {
                var node = axisNodes[0].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
                node = axisNodes[1].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
            }
                break;
            case TransformAxis.ZY:
            {
                var node = axisNodes[1].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
                node = axisNodes[2].Node;
                node.Scale(new vec3(upScale, upScale, upScale));
            }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
    }

    public abstract GizmoType GetGizmoType();
    public abstract string GetGizmoName();

    protected virtual bool SupportsMultipleAxis => true;
    protected abstract void ResetGizmoTransforms();
    protected abstract IGizmo.GizmoNode[] GetGizmoNodes();
}