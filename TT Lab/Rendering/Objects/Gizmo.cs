using System;
using System.Collections.Generic;
using System.Drawing;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Materials;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.Rendering.Objects;

public class Gizmo
{
    public enum GizmoType
    {
        Selection,
        Translate,
        Rotate,
        Scale,
        
        TotalGizmos
    }

    private readonly EditingContext _editingContext;
    private Node _gizmoNode;
    private GizmoNode[] _translateGizmos = new GizmoNode[3];
    private GizmoNode[] _scaleGizmos = new GizmoNode[3];
    private GizmoNode[] _rotateGizmos = new GizmoNode[3];
    private GizmoType _currentGizmo = GizmoType.TotalGizmos;
    private readonly List<Node> _gizmoRenders = new();
    private readonly List<GizmoNode> _gizmoChildrenNodes = new();
    
    public Gizmo(RenderContext renderContext, EditingContext editingContext)
    {
        _editingContext = editingContext;
        _gizmoNode = new Node(renderContext, editingContext.GetEditorNode(), "GIZMOS");
        _gizmoNode.SetInheritScale(false);
        _gizmoNode.SetInheritRotation(false);
        _gizmoNode.SetInheritDiffuse(false);
        
        for (var i = 0; i < (int)GizmoType.TotalGizmos; i++)
        {
            Node? gizmoRootNode = null;
            switch ((GizmoType)i)
            {
                case GizmoType.Selection:
                {
                    var buffer = BufferGeneration.GetCubeBuffer();
                    if (buffer.Model != null)
                    {
                        buffer.Model.Diffuse = new vec4(1.0f, 0.0f, 0.0f, 1.0f);
                        gizmoRootNode = new Node(renderContext, _gizmoNode);
                        gizmoRootNode.AddChild(buffer.Model);
                        gizmoRootNode.SetInitialScale(new vec3(0.25f, 0.25f, 0.25f));
                        gizmoRootNode.ResetLocalTransform();
                    }
                    break;
                }
                case GizmoType.Translate:
                    gizmoRootNode = new Node(renderContext, _gizmoNode);
                    for (var j = 0; j < 3; ++j)
                    {
                        var gizmoNode = new GizmoNode();
                        var axisNode = new Node(renderContext, _gizmoNode);
                        switch (j)
                        {
                            case 0:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(1.0f, 0.0f, 0.0f, 1.0f);
                                cubeMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(cubeMesh.Model);
                                gizmoNode.DefaultScale = new vec3(0.75f, 0.25f, 0.25f);
                                axisNode.SetInitialPosition(new vec3(0.6f, 0, 0));
                                axisNode.SetInitialScale(gizmoNode.DefaultScale);
                                break;
                            }
                            case 1:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 1.0f, 0.0f, 1.0f);
                                cubeMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(cubeMesh.Model);
                                gizmoNode.DefaultScale = new vec3(0.25f, 0.75f, 0.25f);
                                axisNode.SetInitialPosition(new vec3(0, 0.6f, 0));
                                axisNode.SetInitialScale(gizmoNode.DefaultScale);
                                break;
                            }
                            case 2:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 0.0f, 1.0f, 1.0f);
                                cubeMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(cubeMesh.Model);
                                gizmoNode.DefaultScale = new vec3(0.25f, 0.25f, 0.75f);
                                axisNode.SetInitialPosition(new vec3(0, 0, 0.6f));
                                axisNode.SetInitialScale(gizmoNode.DefaultScale);
                                break;
                            }
                        }
                        
                        gizmoNode.Node = axisNode;
                        _translateGizmos[j] = gizmoNode;
                        axisNode.SetInheritVisibility(false);
                        axisNode.ResetLocalTransform();
                        axisNode.IsVisible = false;
                        gizmoRootNode.AddChild(axisNode);
                        _gizmoChildrenNodes.Add(gizmoNode);
                    }
                    break;
                case GizmoType.Rotate:
                    gizmoRootNode = new Node(renderContext, _gizmoNode);
                    for (var j = 0; j < 3; ++j)
                    {
                        var gizmoNode = new GizmoNode();
                        var axisNode = new Node(renderContext, _gizmoNode);
                        switch (j)
                        {
                            case 0:
                            {
                                var circleMesh = BufferGeneration.GetCircleBuffer();
                                circleMesh.Model.Diffuse = new vec4(1.0f, 0.0f, 0.0f, 1.0f);
                                circleMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(circleMesh.Model);
                                axisNode.SetInitialRotation(new quat(vec3.UnitZ * (MathF.PI / 2)));
                                break;
                            }
                            case 1:
                            {
                                var circleMesh = BufferGeneration.GetCircleBuffer();
                                circleMesh.Model.Diffuse = new vec4(0.0f, 1.0f, 0.0f, 1.0f);
                                circleMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(circleMesh.Model);
                                break;
                            }
                            case 2:
                            {
                                var circleMesh = BufferGeneration.GetCircleBuffer();
                                axisNode.AddChild(circleMesh.Model);
                                circleMesh.Model.Diffuse = new vec4(0.0f, 0.0f, 1.0f, 1.0f);
                                circleMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.SetInitialRotation(new quat(vec3.UnitX * (MathF.PI / 2)));
                                break;
                            }
                        }
                        
                        gizmoNode.Node = axisNode;
                        _rotateGizmos[j] = gizmoNode;
                        axisNode.SetInheritVisibility(false);
                        axisNode.ResetLocalTransform();
                        axisNode.IsVisible = false;
                        gizmoRootNode.AddChild(axisNode);
                        _gizmoChildrenNodes.Add(gizmoNode);
                    }
                    break;
                case GizmoType.Scale:
                    gizmoRootNode = new Node(renderContext, _gizmoNode);
                    for (var j = 0; j < 3; ++j)
                    {
                        var gizmoNode = new GizmoNode();
                        var axisNode = new Node(renderContext, _gizmoNode);
                        switch (j)
                        {
                            case 0:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(1.0f, 0.0f, 0.0f, 1.0f);
                                cubeMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(cubeMesh.Model);
                                break;
                            }
                            case 1:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 1.0f, 0.0f, 1.0f);
                                cubeMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(cubeMesh.Model);
                                break;
                            }
                            case 2:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 0.0f, 1.0f, 1.0f);
                                cubeMesh.Model.AddMaterialOverride(new TwinMaterialDepthTestOverride { DepthTestOverride = TwinShader.DepthTestMethod.ALWAYS});
                                axisNode.AddChild(cubeMesh.Model);
                                break;
                            }
                        }
                        
                        gizmoNode.Node = axisNode;
                        _scaleGizmos[j] = gizmoNode;
                        axisNode.SetInheritVisibility(false);
                        axisNode.IsVisible = false;
                        
                        gizmoRootNode.AddChild(axisNode);
                        _gizmoChildrenNodes.Add(gizmoNode);
                    }
                    break;
            }
            
            _gizmoRenders.Add(gizmoRootNode);
        }

        _gizmoNode.IsVisible = false;
    }
    
    public void HighlightAxis(TransformAxis axis)
    {
        ResetGizmoTransforms();
        var axisNodes = GetCurrentGizmos();
        if (axisNodes == null)
        {
            return;
        }

        const float upScale = 1.5f;
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
        }
    }
    
    public void HideGizmo()
    {
        ResetGizmoTransforms();
        foreach (var child in _gizmoChildrenNodes)
        {
            child.Node.IsVisible = false;
        }
        _gizmoNode.IsVisible = false;
        _currentGizmo = GizmoType.TotalGizmos;
    }
    
    public void ShowGizmo()
    {
        _gizmoNode.IsVisible = true;
    }
    
    public void AttachToObject(Renderable node)
    {
        node.AddChild(_gizmoNode);
    }
    
    public void DetachFromCurrentObject()
    {
        _gizmoNode.Parent?.RemoveChild(_gizmoNode);
    }
    
    public void SwitchGizmo(GizmoType gizmoType)
    {
        foreach (var rotateGizmo in _rotateGizmos)
        {
            rotateGizmo.Node.IsVisible = false;
        }
        
        foreach (var scaleGizmo in _scaleGizmos)
        {
            scaleGizmo.Node.IsVisible = false;
        }
    
        foreach (var translateGizmo in _translateGizmos)
        {
            translateGizmo.Node.IsVisible = false;
        }
    
        var scale = 1.0f;
        switch (gizmoType)
        {
            case GizmoType.Selection:
                scale = 0.25f;
                break;
            case GizmoType.Translate:
                foreach (var translateGizmo in _translateGizmos)
                {
                    translateGizmo.Node.IsVisible = true;
                }
                break;
            case GizmoType.Scale:
                foreach (var scaleGizmo in _scaleGizmos)
                {
                    scaleGizmo.Node.IsVisible = true;
                }
                break;
            case GizmoType.Rotate:
                foreach (var rotateGizmo in _rotateGizmos)
                {
                    rotateGizmo.Node.IsVisible = true;
                }
                break;
        }
    
        _gizmoNode.ResetLocalTransform();
        _gizmoNode.Scale(new vec3(scale, scale, scale));
        
        _currentGizmo = gizmoType;
    }
    
    private GizmoNode[]? GetCurrentGizmos()
    {
        switch (_currentGizmo)
        {
            case GizmoType.Translate:
                return _translateGizmos;
            case GizmoType.Rotate:
                return _rotateGizmos;
            case GizmoType.Scale:
                return _scaleGizmos;
        }
        
        return null;
    }
    
    private void ResetGizmoTransforms()
    {
        foreach (var child in _gizmoChildrenNodes)
        {
            child.Node.ResetLocalTransform();
        }
    }
    
    private class GizmoNode
    {
        public Node Node;
        public vec3 DefaultScale = new(1, 1, 1);
    }
    
}