using System;
using System.Collections.Generic;
using System.Drawing;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;

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
    
    public Gizmo(Scene.Scene scene, RenderContext renderContext, EditingContext editingContext)
    {
        _editingContext = editingContext;
        _gizmoNode = new Node(renderContext, scene, "GIZMOS");
        _gizmoNode.SetInheritScale(false);
        
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
                                axisNode.AddChild(cubeMesh.Model);
                                gizmoNode.DefaultScale = new vec3(3f, 1f, 1f);
                                axisNode.Scale(gizmoNode.DefaultScale);
                                axisNode.Translate(new vec3(0.6f, 0, 0));
                                break;
                            }
                            case 1:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 1.0f, 0.0f, 1.0f);
                                axisNode.AddChild(cubeMesh.Model);
                                gizmoNode.DefaultScale = new vec3(1f, 3f, 1f);
                                axisNode.Scale(gizmoNode.DefaultScale);
                                axisNode.Translate(new vec3(0, 0.6f, 0));
                                break;
                            }
                            case 2:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 0.0f, 1.0f, 1.0f);
                                axisNode.AddChild(cubeMesh.Model);
                                gizmoNode.DefaultScale = new vec3(1f, 1f, 5f);
                                axisNode.Scale(gizmoNode.DefaultScale);
                                axisNode.Translate(new vec3(0, 0, 0.6f));
                                break;
                            }
                        }
                        
                        gizmoNode.Node = axisNode;
                        _translateGizmos[j] = gizmoNode;
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
                                axisNode.AddChild(circleMesh.Model);
                                axisNode.Rotate(vec3.UnitZ * (MathF.PI / 2));
                                break;
                            }
                            case 1:
                            {
                                var circleMesh = BufferGeneration.GetCircleBuffer();
                                circleMesh.Model.Diffuse = new vec4(0.0f, 1.0f, 0.0f, 1.0f);
                                axisNode.AddChild(circleMesh.Model);
                                break;
                            }
                            case 2:
                            {
                                var circleMesh = BufferGeneration.GetCircleBuffer();
                                axisNode.AddChild(circleMesh.Model);
                                axisNode.Rotate(vec3.UnitX * (MathF.PI / 2));
                                break;
                            }
                        }
                        
                        gizmoNode.Node = axisNode;
                        _rotateGizmos[j] = gizmoNode;
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
                                axisNode.AddChild(cubeMesh.Model);
                                break;
                            }
                            case 1:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 1.0f, 0.0f, 1.0f);
                                axisNode.AddChild(cubeMesh.Model);
                                break;
                            }
                            case 2:
                            {
                                var cubeMesh = BufferGeneration.GetCubeBuffer();
                                cubeMesh.Model.Diffuse = new vec4(0.0f, 0.0f, 1.0f, 1.0f);
                                axisNode.AddChild(cubeMesh.Model);
                                break;
                            }
                        }
                        
                        gizmoNode.Node = axisNode;
                        _scaleGizmos[j] = gizmoNode;
                        axisNode.IsVisible = false;
                        
                        gizmoRootNode.AddChild(axisNode);
                        _gizmoChildrenNodes.Add(gizmoNode);
                    }
                    break;
            }
            
            _gizmoRenders.Add(gizmoRootNode);
        }
    }
    
    public void HighlightAxis(TransformAxis axis)
    {
        ResetNodesScale();
        var axisNodes = GetCurrentGizmos();
        if (axisNodes == null)
        {
            return;
        }
        
        switch (axis)
        {
            case TransformAxis.X:
                {
                    var node = axisNodes[0].Node;
                    node.LocalTransform = mat4.Identity;
                    node.Scale(new vec3(2.0f, 2.0f, 2.0f));
                }
                break;
            case TransformAxis.Y:
                {
                    var node = axisNodes[1].Node;
                    node.LocalTransform = mat4.Identity;
                    node.Scale(new vec3(2.0f, 2.0f, 2.0f));
                }
                break;
            case TransformAxis.Z:
                {
                    var node = axisNodes[2].Node;
                    node.LocalTransform = mat4.Identity;
                    node.Scale(new vec3(2.0f, 2.0f, 2.0f));
                }
                break;
        }
    }
    
    public void HideGizmo()
    {
        DetachCurrentGizmo();
        _gizmoNode.IsVisible = false;
        _currentGizmo = GizmoType.TotalGizmos;
    }
    
    public void ShowGizmo()
    {
        AttachCurrentGizmo();
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
        DetachCurrentGizmo();
        
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
        // _gizmoNode.setInheritOrientation(true);
        switch (gizmoType)
        {
            case GizmoType.Selection:
                scale = 0.25f;
                break;
            case GizmoType.Translate:
                // _gizmoNode.setInheritOrientation(false);
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
    
        _gizmoNode.LocalTransform = mat4.Identity;
        _gizmoNode.Scale(new vec3(scale, scale, scale));
        
        _currentGizmo = gizmoType;
        AttachCurrentGizmo();
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
    
    private void DetachCurrentGizmo()
    {
        if (_currentGizmo == GizmoType.TotalGizmos)
        {
            return;
        }
    
        // _gizmoNode.RemoveChild(_gizmoRenders[(int)_currentGizmo]);
    }
    
    private void AttachCurrentGizmo()
    {
        if (_currentGizmo == GizmoType.TotalGizmos)
        {
            return;
        }
    
        // _gizmoNode.AddChild(_gizmoRenders[(int)_currentGizmo]);
    }
    
    private void ResetNodesScale()
    {
        foreach (var child in _gizmoChildrenNodes)
        {
            child.Node.Scale(child.DefaultScale);
        }
    }
    
    private class GizmoNode
    {
        public Node Node;
        public vec3 DefaultScale = new(1, 1, 1);
    }
    
}