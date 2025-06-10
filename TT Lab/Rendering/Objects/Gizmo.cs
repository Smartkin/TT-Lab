using System;
using System.Collections.Generic;
using System.Drawing;
using GlmSharp;
using org.ogre;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Math = org.ogre.Math;

namespace TT_Lab.Rendering.Objects
{
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
        private SceneNode _gizmoNode;
        private GizmoNode[] _translateGizmos = new GizmoNode[3];
        private GizmoNode[] _scaleGizmos = new GizmoNode[3];
        private GizmoNode[] _rotateGizmos = new GizmoNode[3];
        private GizmoType _currentGizmo = GizmoType.TotalGizmos;
        private readonly List<Entity> _gizmoRenders = new();
        private readonly List<GizmoNode> _gizmoChildrenNodes = new();
        
        public Gizmo(SceneManager sceneManager, EditingContext editingContext)
        {
            _editingContext = editingContext;
            _gizmoNode = _editingContext.GetEditorNode().createChildSceneNode("GizmoNode");
            _gizmoNode.setInheritScale(false);
            
            for (var i = 0; i < (int)GizmoType.TotalGizmos; i++)
            {
                MeshPtr? mesh = null;
                switch ((GizmoType)i)
                {
                    case GizmoType.Selection:
                        mesh = BufferGeneration.GetCubeBuffer("SelectionCube", Color.Red);
                        break;
                    case GizmoType.Translate:
                        mesh = BufferGeneration.GetSimpleAxisBuffer();
                        for (var j = 0; j < 3; ++j)
                        {
                            var gizmoNode = new GizmoNode();
                            var axisNode = _gizmoNode.createChildSceneNode();
                            switch (j)
                            {
                                case 0:
                                {
                                    var cubeMesh = BufferGeneration.GetCubeBuffer("TranslateXAxis", Color.Red);
                                    var entity = sceneManager.createEntity(cubeMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    gizmoNode.DefaultScale = new Vector3(0.5f, 0.1f, 0.1f);
                                    axisNode.setScale(gizmoNode.DefaultScale);
                                    axisNode.translate(0.6f, 0, 0);
                                    break;
                                }
                                case 1:
                                {
                                    var cubeMesh = BufferGeneration.GetCubeBuffer("TranslateYAxis", Color.Green);
                                    var entity = sceneManager.createEntity(cubeMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    gizmoNode.DefaultScale = new Vector3(0.1f, 0.5f, 0.1f);
                                    axisNode.setScale(gizmoNode.DefaultScale);
                                    axisNode.translate(0, 0.6f, 0);
                                    break;
                                }
                                case 2:
                                {
                                    var cubeMesh = BufferGeneration.GetCubeBuffer("TranslateZAxis", Color.Blue);
                                    var entity = sceneManager.createEntity(cubeMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    gizmoNode.DefaultScale = new Vector3(0.1f, 0.1f, 0.5f);
                                    axisNode.setScale(gizmoNode.DefaultScale);
                                    axisNode.translate(0, 0, 0.6f);
                                    break;
                                }
                            }
                            
                            gizmoNode.Node = axisNode;
                            _translateGizmos[j] = gizmoNode;
                            axisNode.setVisible(false);
                            
                            _gizmoChildrenNodes.Add(gizmoNode);
                        }
                        break;
                    case GizmoType.Rotate:
                        mesh = BufferGeneration.GetSimpleAxisBuffer();
                        for (var j = 0; j < 3; ++j)
                        {
                            var gizmoNode = new GizmoNode();
                            var axisNode = _gizmoNode.createChildSceneNode();
                            switch (j)
                            {
                                case 0:
                                {
                                    var circleMesh = BufferGeneration.GetCircleBuffer("CircleXAxis", Color.Red);
                                    var entity = sceneManager.createEntity(circleMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    axisNode.rotate(new Vector3(0, 0, 1), new Radian(Math.PI / 2));
                                    break;
                                }
                                case 1:
                                {
                                    var circleMesh = BufferGeneration.GetCircleBuffer("CircleYAxis", Color.Green);
                                    var entity = sceneManager.createEntity(circleMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    break;
                                }
                                case 2:
                                {
                                    var circleMesh = BufferGeneration.GetCircleBuffer("CircleZAxis", Color.Blue);
                                    var entity = sceneManager.createEntity(circleMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    axisNode.rotate(new Vector3(1, 0, 0), new Radian(Math.PI / 2));
                                    break;
                                }
                            }
                            
                            gizmoNode.Node = axisNode;
                            _rotateGizmos[j] = gizmoNode;
                            axisNode.setVisible(false);
                            
                            _gizmoChildrenNodes.Add(gizmoNode);
                        }
                        break;
                    case GizmoType.Scale:
                        mesh = BufferGeneration.GetSimpleAxisBuffer();
                        for (var j = 0; j < 3; ++j)
                        {
                            var gizmoNode = new GizmoNode();
                            var axisNode = _gizmoNode.createChildSceneNode();
                            switch (j)
                            {
                                case 0:
                                {
                                    var cubeMesh = BufferGeneration.GetCubeBuffer("CubeXAxis", Color.Red);
                                    var entity = sceneManager.createEntity(cubeMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    break;
                                }
                                case 1:
                                {
                                    var cubeMesh = BufferGeneration.GetCubeBuffer("CubeYAxis", Color.Green);
                                    var entity = sceneManager.createEntity(cubeMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    break;
                                }
                                case 2:
                                {
                                    var cubeMesh = BufferGeneration.GetCubeBuffer("CubeZAxis", Color.Blue);
                                    var entity = sceneManager.createEntity(cubeMesh);
                                    entity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                                    axisNode.attachObject(entity);
                                    break;
                                }
                            }
                            
                            gizmoNode.Node = axisNode;
                            _scaleGizmos[j] = gizmoNode;
                            axisNode.setVisible(false);
                            
                            _gizmoChildrenNodes.Add(gizmoNode);
                        }
                        break;
                }
                
                var gizmoEntity = sceneManager.createEntity(mesh);
                gizmoEntity.setMaterial(MaterialManager.GetMaterial("Gizmo"));
                _gizmoRenders.Add(gizmoEntity);
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
                        node.scale(2.0f, 2.0f, 2.0f);
                    }
                    break;
                case TransformAxis.Y:
                    {
                        var node = axisNodes[1].Node;
                        node.scale(2.0f, 2.0f, 2.0f);
                    }
                    break;
                case TransformAxis.Z:
                    {
                        var node = axisNodes[2].Node;
                        node.scale(2.0f, 2.0f, 2.0f);
                    }
                    break;
            }
        }

        public void HideGizmo()
        {
            DetachCurrentGizmo();
            _gizmoNode.setVisible(false);
            _currentGizmo = GizmoType.TotalGizmos;
        }

        public void ShowGizmo()
        {
            AttachCurrentGizmo();
            _gizmoNode.setVisible(true);
        }

        public void AttachToObject(SceneNode node)
        {
            node.addChild(_gizmoNode);
        }

        public void DetachFromCurrentObject()
        {
            if (_gizmoNode.getParent() != null)
            {
                _gizmoNode.getParent().removeChild(_gizmoNode);
            }
        }

        public void SwitchGizmo(GizmoType gizmoType)
        {
            DetachCurrentGizmo();
            
            foreach (var rotateGizmo in _rotateGizmos)
            {
                rotateGizmo.Node.setVisible(false);
            }
            
            foreach (var scaleGizmo in _scaleGizmos)
            {
                scaleGizmo.Node.setVisible(false);
            }

            foreach (var translateGizmo in _translateGizmos)
            {
                translateGizmo.Node.setVisible(false);
            }

            var scale = 1.0f;
            _gizmoNode.setInheritOrientation(true);
            switch (gizmoType)
            {
                case GizmoType.Selection:
                    scale = 0.25f;
                    break;
                case GizmoType.Translate:
                    _gizmoNode.setInheritOrientation(false);
                    foreach (var translateGizmo in _translateGizmos)
                    {
                        translateGizmo.Node.setVisible(true);
                    }
                    break;
                case GizmoType.Scale:
                    foreach (var scaleGizmo in _scaleGizmos)
                    {
                        scaleGizmo.Node.setVisible(true);
                    }
                    break;
                case GizmoType.Rotate:
                    foreach (var rotateGizmo in _rotateGizmos)
                    {
                        rotateGizmo.Node.setVisible(true);
                    }
                    break;
            }

            _gizmoNode.setScale(scale, scale, scale);
            
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

            _gizmoNode.detachObject(_gizmoRenders[(int)_currentGizmo]);
        }

        private void AttachCurrentGizmo()
        {
            if (_currentGizmo == GizmoType.TotalGizmos)
            {
                return;
            }

            _gizmoNode.attachObject(_gizmoRenders[(int)_currentGizmo]);
        }

        private void ResetNodesScale()
        {
            foreach (var child in _gizmoChildrenNodes)
            {
                child.Node.setScale(child.DefaultScale);
            }
        }

        private class GizmoNode
        {
            public SceneNode Node;
            public Vector3 DefaultScale = new(1, 1, 1);
        }
        
    }
}
