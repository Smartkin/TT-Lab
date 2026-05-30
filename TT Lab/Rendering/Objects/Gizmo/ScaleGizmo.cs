using System;
using GlmSharp;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects.Gizmo;

public class ScaleGizmo : Gizmo
{
    private readonly IGizmo.GizmoNode[] _scaleGizmos = new IGizmo.GizmoNode[3];

    public ScaleGizmo(RenderContext renderContext, EditingContext editingContext) : base(renderContext, editingContext)
    {
        var gizmoRootNode = new Node(renderContext, RenderNode);
        for (var j = 0; j < 3; ++j)
        {
            var gizmoNode = new IGizmo.GizmoNode();
            var axisNode = new Node(renderContext, RenderNode);
            var axisColor = new vec4(j == 0 ? 1.0f : 0.0f, j == 1 ? 1.0f : 0.0f, j == 2 ? 1.0f : 0.0f, 1.0f);
            var cubeMesh = BufferGeneration.GetCubeBuffer(renderContext).Model!;
            cubeMesh.Diffuse = axisColor;
            cubeMesh.Scale(vec3.Ones * 0.5f);
            cubeMesh.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
            axisNode.AddChild(cubeMesh);
            
            gizmoNode.Node = axisNode;
            _scaleGizmos[j] = gizmoNode;
            gizmoRootNode.AddChild(axisNode);
        }
    }

    public override GizmoType GetGizmoType() => GizmoType.Scale;
    
    public override String GetGizmoName()
    {
        return "ScaleGizmo";
    }

    protected override void ResetGizmoTransforms()
    {
        foreach (var gizmoNode in _scaleGizmos)
        {
            gizmoNode.Node.ResetLocalTransform();
        }
    }

    protected override IGizmo.GizmoNode[] GetGizmoNodes()
    {
        return _scaleGizmos;
    }
}