using System;
using GlmSharp;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects.Gizmo;

public class RotationGizmo : Gizmo
{
    private readonly IGizmo.GizmoNode[] _rotateGizmos = new IGizmo.GizmoNode[3];

    public RotationGizmo(RenderContext renderContext, EditingContext editingContext) : base(renderContext, editingContext)
    {
        RenderNode.SetInheritRotation(false);
        var gizmoRootNode = new Node(renderContext, RenderNode);
        for (var j = 0; j < 3; ++j)
        {
            var gizmoNode = new IGizmo.GizmoNode();
            var axisNode = new Node(renderContext, RenderNode);
            var axisColor = new vec4(j == 0 ? 1.0f : 0.0f, j == 1 ? 1.0f : 0.0f, j == 2 ? 1.0f : 0.0f, 1.0f);
            var axisRotation = j switch
            {
                0 => new quat(vec3.UnitZ * (MathF.PI / 2)),
                2 => new quat(vec3.UnitX * (MathF.PI / 2)),
                _ => quat.Identity
            };
            var circleMesh = BufferGeneration.GetCircleBuffer(renderContext).Model!;
            circleMesh.Diffuse = axisColor;
            circleMesh.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
            axisNode.AddChild(circleMesh);
            axisNode.SetInitialRotation(axisRotation);
            
            gizmoNode.Node = axisNode;
            _rotateGizmos[j] = gizmoNode;
            axisNode.ResetLocalTransform();
            gizmoRootNode.AddChild(axisNode);
        }
    }

    public override GizmoType GetGizmoType() => GizmoType.Rotate;
    public override String GetGizmoName()
    {
        return "RotationGizmo";
    }

    protected override void ResetGizmoTransforms()
    {
        foreach (var gizmo in _rotateGizmos)
        {
            gizmo.Node.ResetLocalTransform();
        }
    }

    protected override IGizmo.GizmoNode[] GetGizmoNodes()
    {
        return _rotateGizmos;
    }
}