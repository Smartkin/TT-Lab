using System;
using GlmSharp;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects.Gizmo;

public class TranslationGizmo : Gizmo
{
    private readonly IGizmo.GizmoNode[] _translateGizmos = new IGizmo.GizmoNode[3];

    public TranslationGizmo(RenderContext renderContext, EditingContext editingContext) : base(renderContext, editingContext)
    {
        var gizmoRootNode = new Node(renderContext, RenderNode);
        for (var j = 0; j < 3; ++j)
        {
            var gizmoNode = new IGizmo.GizmoNode();
            var axisNode = new Node(renderContext, RenderNode);
            const float scale = 0.75f;
            const float lowerScale = 0.25f;
            const float offset = 0.6f;
            var axisColor = new vec4(j == 0 ? 1.0f : 0.0f, j == 1 ? 1.0f : 0.0f, j == 2 ? 1.0f : 0.0f, 1.0f);
            var axisScale = new vec3(j == 0 ? scale : lowerScale, j == 1 ? scale : lowerScale, j == 2 ? scale : lowerScale);
            var axisPosition = new vec3(j == 0 ? offset : 0.0f, j == 1 ? offset : 0.0f, j == 2 ? offset : 0.0f);
            var cubeMesh = BufferGeneration.GetCubeBuffer(renderContext).Model!;
            cubeMesh.Diffuse = axisColor;
            cubeMesh.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
            axisNode.AddChild(cubeMesh);
            gizmoNode.DefaultScale = axisScale;
            axisNode.SetInitialPosition(axisPosition);
            axisNode.SetInitialScale(gizmoNode.DefaultScale);
            
            gizmoNode.Node = axisNode;
            _translateGizmos[j] = gizmoNode;
            axisNode.ResetLocalTransform();
            gizmoRootNode.AddChild(axisNode);
        }
    }

    public override GizmoType GetGizmoType() => GizmoType.Translate;
    public override String GetGizmoName()
    {
        return "TranslateGizmo";
    }

    protected override void ResetGizmoTransforms()
    {
        foreach (var gizmo in _translateGizmos)
        {
            gizmo.Node.ResetLocalTransform();
        }
    }

    protected override IGizmo.GizmoNode[] GetGizmoNodes()
    {
        return _translateGizmos;
    }
}