using System;
using System.Windows;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.SceneInstances;
using TT_Lab.Rendering.Scene;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Instance;
using Math = System.Math;

namespace TT_Lab.Rendering
{
    public class EditingContext
    {
        public SceneInstance? SelectedInstance;
        public EditableObject? SelectedRenderable;
        public TransformMode TransformMode = TransformMode.SELECTION;
        public TransformAxis TransformAxis = TransformAxis.NONE;
        
        private readonly EditorCursor _cursor;
        private readonly SceneInstance?[] _palette = new SceneInstance[9];
        private readonly BillboardSet _positionsBillboards;
        private readonly BillboardSet _triggersBillboards;
        private readonly BillboardSet _camerasBillboards;
        private readonly BillboardSet _instancesBillboards;
        private readonly BillboardSet _aiPositionsBillboards;
        private int _currentPaletteIndex = 0;
        private readonly ChunkEditorViewModel _editor;
        private readonly Node _editCtxNode;
        private readonly Gizmo _gizmo;
        private vec3 _gridStep;
        private mat4 _gridRotation;
        private readonly Scene.Scene _scene;
        private readonly RenderContext _renderContext;

        public EditingContext(RenderContext context, Scene.Scene scene, ChunkEditorViewModel editor)
        {
            _editor = editor;
            _renderContext = context;
            _scene = scene;
            _editCtxNode = new Node(context, scene);
            _cursor = new EditorCursor(this);
            _gizmo = new Gizmo(scene, context, this);
            _positionsBillboards = CreateBillboardSet(context, "PositionsBillboards", "BillboardPositions");
            _triggersBillboards = CreateBillboardSet(context, "TriggersBillboards", "BillboardTriggers");
            _camerasBillboards = CreateBillboardSet(context, "CamerasBillboards", "BillboardCameras");
            _instancesBillboards = CreateBillboardSet(context, "InstancesBillboards", "BillboardInstances");
            _aiPositionsBillboards = CreateBillboardSet(context, "AiPositionsBillboards", "BillboardAiPositions");
        }

        public Node GetEditorNode()
        {
            return _editCtxNode;
        }

        // public Entity CreateEntity(MeshPtr mesh)
        // {
        //     return _sceneManager.createEntity(mesh);
        // }

        public Renderable GetPositionBillboards()
        {
            return _positionsBillboards;
        }
        
        public Renderable GetInstancesBillboards()
        {
            return _instancesBillboards;
        }
        
        public Renderable GetTriggersBillboards()
        {
            return _triggersBillboards;
        }
        
        public Renderable GetCamerasBillboards()
        {
            return _camerasBillboards;
        }
        
        public Renderable GetAiPositionsBillboards()
        {
            return _aiPositionsBillboards;
        }
        
        public Billboard CreatePositionBillboard()
        {
            return _positionsBillboards.CreateBillboard(0, 0, 0);
        }
        
        public Billboard CreateTriggerBillboard()
        {
            return _triggersBillboards.CreateBillboard(0, 0, 0);
        }
        
        public Billboard CreateInstanceBillboard()
        {
            return _instancesBillboards.CreateBillboard(0, 0, 0);
        }
        
        public Billboard CreateCameraBillboard()
        {
            return _camerasBillboards.CreateBillboard(0, 0, 0);
        }
        
        public Billboard CreateAiPositionBillboard()
        {
            return _aiPositionsBillboards.CreateBillboard(0, 0, 0);
        }

        public RenderContext GetRenderContext()
        {
            return _renderContext;
        }

        public void Deselect()
        {
            TransformMode = TransformMode.SELECTION;
            TransformAxis = TransformAxis.NONE;
            SelectedInstance?.UnlinkChangesToViewModel((ViewportEditableInstanceViewModel)_editor.CurrentInstanceEditor!);
            _editor.InstanceEditorChanged(new RoutedPropertyChangedEventArgs<Object>(null, null));
            // _renderWindow.SetCameraStyle(CameraStyle.CS_FREELOOK);
            SelectedInstance?.Deselect();
            SelectedInstance = null;
            SelectedRenderable = null;
            _gizmo.HideGizmo();
        }

        public void Select(SceneInstance instance)
        {
            Deselect();
            SelectedInstance = instance;
            SelectedInstance?.Select();
            SelectedRenderable = SelectedInstance?.GetEditableObject();
            
            if (SelectedInstance != null)
            {
                // _renderWindow.SetCameraTarget(SelectedInstance.GetEditableObject().GetSceneNode());
                // _renderWindow.SetCameraStyle(CameraStyle.CS_ORBIT);
                // _renderWindow.SetYawPitchDist((float)Math.PI / 4.0f, (float)Math.PI / 4.0f, 10);
                _editor.InstanceEditorChanged(new RoutedPropertyChangedEventArgs<Object>(null, SelectedInstance.GetViewModel()));
                SelectedInstance.LinkChangesToViewModel((ViewportEditableInstanceViewModel)_editor.CurrentInstanceEditor!);
                _gizmo.DetachFromCurrentObject();
                _gizmo.SwitchGizmo((Gizmo.GizmoType)(int)TransformMode);
                _gizmo.AttachToObject(SelectedInstance.GetEditableObject());
                _gizmo.ShowGizmo();
            }
        }

        public InstanceSectionResourceEditorViewModel? GetCurrentEditor()
        {
            return _editor.CurrentInstanceEditor;
        }

        public void SetGrid()
        {
            if (SelectedInstance == null)
            {
                return;
            }
            
            _gridStep.x = SelectedInstance.GetSize().x;
            _gridStep.y = SelectedInstance.GetSize().y;
            _gridStep.z = SelectedInstance.GetSize().z;
            var gridRot = SelectedInstance.GetRotation();
            _gridRotation = (new quat(vec3.Radians(SelectedInstance.GetRotation()))).ToMat4;
            SetCursorCoordinates(SelectedInstance.GetPosition());
        }

        public void MoveCursorGrid(vec3 offset)
        {
            var cursorPos = _cursor.GetPosition();
            cursorPos += (_gridRotation * new vec4(offset * _gridStep, 1.0f)).xyz;
            SetCursorCoordinates(cursorPos);
        }

        public bool IsInstanceSelected()
        {
            return SelectedInstance != null;
        }

        public void SetCursorCoordinates(vec3 pos)
        {
            _cursor.SetPosition(pos);
        }

        public void SetPalette(SceneInstance instance)
        {
            _palette[_currentPaletteIndex] = instance;
        }

        public void SpawnAtCursor()
        {
            if (_palette[_currentPaletteIndex] == null)
            {
                return;
            }

            var cursorPosition = _cursor.GetPosition();
            var newInstance = _editor.NewSceneInstance(_palette[_currentPaletteIndex]!.GetType(), _palette[_currentPaletteIndex]!.GetViewModel());
            Select(newInstance);
            newInstance.SetPositionRotationScale(cursorPosition, _palette[_currentPaletteIndex]!.GetRotation(), _palette[_currentPaletteIndex]!.GetScale());
            TransformMode = TransformMode.SELECTION;
            TransformAxis = TransformAxis.NONE;
        }

        public bool StartTransform(float x, float y)
        {
            if (SelectedInstance == null || TransformMode == TransformMode.SELECTION)
            {
                transforming = false;
                return false;
            }
            if (transforming)
            {
                return false;
            }
            startPos = new vec2(x, y);
            transforming = true;
            return true;
        }

        public void EndTransform(float x, float y)
        {
            if (SelectedInstance == null || TransformMode == TransformMode.SELECTION)
            {
                transforming = false;
                return;
            }
            if (!transforming)
            {
                return;
            }
            UpdateTransform(x, y);
            transforming = false;
            // var pos = SelectedRenderable!.getParentSceneNode().getPosition();
            // var renderQuat = SelectedRenderable.getParentSceneNode().getOrientation();
            // var rotationMatrix = new Matrix3();
            // renderQuat.ToRotationMatrix(rotationMatrix);
            // var rotX = new Radian();
            // var rotY = new Radian();
            // var rotZ = new Radian();
            // rotationMatrix.ToEulerAnglesXYZ(rotX, rotY, rotZ);
            // var rot = new vec3(rotX.valueDegrees(), rotY.valueDegrees(), rotZ.valueDegrees());
            // var scl = SelectedRenderable!.getParentSceneNode().getScale();
            // SelectedInstance.SetPositionRotationScale(new vec3(pos.x, pos.y, pos.z), rot, new vec3(scl.x, scl.y, scl.z));
        }

        public void UpdateTransform(float x, float y)
        {
            if (SelectedInstance == null || !transforming)
            {
                return;
            }
            
            endPos = new vec2(x, y);
            var delta = (endPos.x - startPos.x) + (startPos.y - endPos.y);
            startPos = endPos;

            if (TransformMode == TransformMode.TRANSLATE)
            {
                var k = 0.05f;
                var axis = new vec3();
                if (TransformAxis == TransformAxis.X)
                {
                    axis.x = 1.0f;
                }
                else if (TransformAxis == TransformAxis.Y)
                {
                    axis.y = 1.0f;
                }
                else if (TransformAxis == TransformAxis.Z)
                {
                    axis.z = 1.0f;
                }
                Translate(axis * k * delta);
            }
            if (TransformMode == TransformMode.SCALE)
            {
                var k = 0.05f;
                var axis = new vec3();
                if (TransformAxis == TransformAxis.X)
                {
                    axis.x = 1.0f;
                }
                else if (TransformAxis == TransformAxis.Y)
                {
                    axis.y = 1.0f;
                }
                else if (TransformAxis == TransformAxis.Z)
                {
                    axis.z = 1.0f;
                }
                Scale(axis * k * delta);
            }
            if (TransformMode == TransformMode.ROTATE)
            {
                var k = 0.2f;
                if (TransformAxis == TransformAxis.X)
                {
                    RotateX(k * delta);
                }
                else if (TransformAxis == TransformAxis.Y)
                {
                    RotateY(k * delta);
                }
                else if (TransformAxis == TransformAxis.Z)
                {
                    RotateZ(k * delta);
                }
            }
        }

        private void SwitchEditMode(TransformMode mode)
        {
            if (transforming)
            {
                return;
            }

            if (TransformMode != mode)
            {
                TransformMode = mode;
            }
            else
            {
                TransformMode = TransformMode.SELECTION;
            }
            TransformAxis = TransformAxis.NONE;
            _gizmo.SwitchGizmo((Gizmo.GizmoType)(int)TransformMode);
        }

        public void ToggleScale()
        {
            if (SelectedInstance == null || !SelectedInstance.IsTransformSupported(TransformMode.SCALE))
            {
                return;
            }
            SwitchEditMode(TransformMode.SCALE);
        }

        public void ToggleTranslate()
        {
            if (SelectedInstance == null || !SelectedInstance.IsTransformSupported(TransformMode.TRANSLATE))
            {
                return;
            }
            SwitchEditMode(TransformMode.TRANSLATE);
        }

        public void ToggleRotate()
        {
            if (SelectedInstance == null || !SelectedInstance.IsTransformSupported(TransformMode.ROTATE))
            {
                return;
            }
            SwitchEditMode(TransformMode.ROTATE);
        }

        public void SetTransformAxis(TransformAxis axis)
        {
            if (TransformAxis == axis)
            {
                TransformAxis = TransformAxis.NONE;
            }
            else
            {
                TransformAxis = axis;
            }
            _gizmo.HighlightAxis(TransformAxis);
            if (transforming)
            {
                UpdateTransform(endPos.x, endPos.y);
            }
        }

        private mat4 startTransform;
        private vec2 startPos;
        private vec2 endPos;
        private bool transforming = false;

        private void Scale(vec3 offset)
        {
            SelectedRenderable.Scale(offset);
        }

        private void Translate(vec3 offset)
        {
            SelectedRenderable.Translate(offset);
        }

        private void RotateX(float value)
        {
            SelectedRenderable.Rotate(vec3.UnitX * value, true);
        }

        private void RotateY(float value)
        {
            SelectedRenderable.Rotate(vec3.UnitY * value, true);
        }

        private void RotateZ(float value)
        {
            SelectedRenderable.Rotate(vec3.UnitZ * value, true);
        }

        private BillboardSet CreateBillboardSet(RenderContext renderContext, string billboardName, string billboardMaterial)
        {
            var billboardSet = new BillboardSet(renderContext, billboardName);
            // billboardSet.setMaterial(MaterialManager.GetMaterial(billboardMaterial));
            // billboardSet.setDefaultDimensions(2, 2);
            // billboardSet.setCullIndividually(true);
            // // Set the bounding box to be huge, because otherwise the node itself gets culled out and all billboards stop rendering
            // billboardSet.setBounds(new AxisAlignedBox(-1000, -1000, -1000, 1000, 1000, 1000), 1000);
            // billboardSet.setRenderQueueGroup((byte)RenderQueueGroupID.RENDER_QUEUE_OVERLAY);
        
            return billboardSet;
        }
    }

    public enum TransformMode
    {
        SELECTION,
        TRANSLATE,
        ROTATE,
        SCALE
    }

    public enum TransformAxis
    {
        NONE,
        X,
        Y,
        Z,
        XZ,
        XY,
        ZY
    }
}
