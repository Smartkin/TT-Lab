using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GlmSharp;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.Gizmo;
using TT_Lab.Rendering.Scene;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Color = System.Drawing.Color;

namespace TT_Lab.Rendering;

public class EditingContext
{
    public ViewportObject? SelectedInstance;
    public EditableObject? SelectedRenderable;
    public TransformMode TransformMode = TransformMode.SELECTION;
    public TransformAxis TransformAxis = TransformAxis.NONE;
    public TransformLocality TransformLocality = TransformLocality.LOCAL;

    private readonly Scene.Scene _scene;
    private readonly EditorCursor _cursor;
    private readonly ViewportObject?[] _palette = new ViewportObject[9];
    private readonly BillboardSet _positionsBillboards;
    private readonly BillboardSet _pathsBillboards;
    private readonly BillboardSet _particlesBillboards;
    private readonly BillboardSet _triggersBillboards;
    private readonly BillboardSet _camerasBillboards;
    private readonly BillboardSet _instancesBillboards;
    private readonly BillboardSet _aiPositionsBillboards;
    private readonly BillboardSet _chunkLinksBillboards;
    private int _currentPaletteIndex = 0;
    private readonly Node _editCtxNode;
    private IGizmo? _currentGizmo;
    private readonly List<IGizmo> _gizmos = [];
    private vec3 _gridStep;
    private mat4 _gridRotation;
    private readonly RenderContext _renderContext;

    public EditingContext(RenderContext context, Scene.Scene scene)
    {
        _scene = scene;
        _renderContext = context;
        _editCtxNode = new Node(context, scene);
        _cursor = new EditorCursor(this);
        var color = Color.FromKnownColor(KnownColor.Green);
        _positionsBillboards = CreateBillboardSet(context, "PositionsBillboards", "Position");
        _positionsBillboards.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  1.0f);
        _editCtxNode.AddChild(_positionsBillboards);
            
        _triggersBillboards = CreateBillboardSet(context, "TriggersBillboards", "Trigger");
        color = Color.FromKnownColor(KnownColor.DarkOrange);
        _triggersBillboards.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  1.0f);
        _editCtxNode.AddChild(_triggersBillboards);
            
        _camerasBillboards = CreateBillboardSet(context, "CamerasBillboards", "Camera");
        color = Color.FromKnownColor(KnownColor.Blue);
        _camerasBillboards.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
        _editCtxNode.AddChild(_camerasBillboards);
            
        _instancesBillboards = CreateBillboardSet(context, "InstancesBillboards", "Instance");
        _editCtxNode.AddChild(_instancesBillboards);
        
        _aiPositionsBillboards = CreateBillboardSet(context, "AiPositionsBillboards", "AI_Position");
        color = Color.FromKnownColor(KnownColor.Yellow);
        _aiPositionsBillboards.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
        _editCtxNode.AddChild(_aiPositionsBillboards);

        _pathsBillboards = CreateBillboardSet(context, "PathsBillboards", "Path");
        color = Color.FromKnownColor(KnownColor.LightBlue);
        _pathsBillboards.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
        _editCtxNode.AddChild(_pathsBillboards);

        _particlesBillboards = CreateBillboardSet(context, "ParticlesBillboards", "Particle_Emitter", false);
        _editCtxNode.AddChild(_particlesBillboards);

        _chunkLinksBillboards = CreateBillboardSet(context, "ChunkLinksBillboard", "Chunk_Link");
        color = Color.FromKnownColor(KnownColor.Red);
        _chunkLinksBillboards.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, 1.0f);
        _editCtxNode.AddChild(_chunkLinksBillboards);
        
        RegisterGizmo(new SelectionGizmo(context, this));
        RegisterGizmo(new TranslationGizmo(context, this));
        RegisterGizmo(new RotationGizmo(context, this));
        RegisterGizmo(new ScaleGizmo(context, this));
    }

    public Node GetEditorNode()
    {
        return _editCtxNode;
    }

    public void RegisterGizmo(IGizmo gizmo)
    {
        _gizmos.Add(gizmo);
        gizmo.RenderNode.IsVisible = false;
    }

    // public Entity CreateEntity(MeshPtr mesh)
    // {
    //     return _sceneManager.createEntity(mesh);
    // }

    public Renderable GetPositionBillboards()
    {
        return _positionsBillboards;
    }

    public Renderable GetPathBillboards()
    {
        return _pathsBillboards;
    }

    public Renderable GetParticleBillboards()
    {
        return _particlesBillboards;
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

    public Billboard CreateChunkLinkBillboard()
    {
        return _chunkLinksBillboards.CreateBillboard(0, 0, 0);
    }
        
    public Billboard CreateTriggerBillboard()
    {
        return _triggersBillboards.CreateBillboard(0, 0, 0);
    }
        
    public Billboard CreateInstanceBillboard()
    {
        return _instancesBillboards.CreateBillboard(0, 0, 0);
    }

    public Billboard CreateParticleBillboard()
    {
        return _particlesBillboards.CreateBillboard(0, 0, 0);
    }
    
    public Billboard CreateCameraBillboard()
    {
        return _camerasBillboards.CreateBillboard(0, 0, 0);
    }
        
    public Billboard CreateAiPositionBillboard()
    {
        return _aiPositionsBillboards.CreateBillboard(0, 0, 0);
    }

    public Billboard CreatePathBillboard()
    {
        return _pathsBillboards.CreateBillboard(0, 0, 0);
    }

    public RenderContext GetRenderContext()
    {
        return _renderContext;
    }

    public void Deselect()
    {
        _renderContext.QueueRenderAction(() =>
        {
            TransformMode = TransformMode.SELECTION;
            TransformAxis = TransformAxis.NONE;
            TransformLocality = TransformLocality.LOCAL;
            SelectedInstance?.Render.Deselect();
            SelectedInstance = null;
            SelectedRenderable = null;
            SwitchGizmo(GizmoType.Selection);
            _currentGizmo?.Hide();
        });
    }

    public void Select(ViewportObject instance)
    {
        Deselect();
        _renderContext.QueueRenderAction(() =>
        {
            SelectedInstance = instance;
            SelectedInstance?.Render.Select();
            SelectedRenderable = SelectedInstance?.Render;
        
            if (SelectedInstance != null)
            {
                SwitchGizmo((GizmoType)(int)TransformMode);
            }
        });
        
    }

    public void SwitchGizmo(GizmoType type, string? customGizmoName = null)
    {
        _renderContext.QueueRenderAction(() =>
        {
            _currentGizmo?.ChangeLocalityRender(TransformLocality);
            _currentGizmo?.HighlightAxis(TransformAxis.NONE);
            _currentGizmo?.DetachGizmo();
            _currentGizmo?.Hide();
            var availableGizmos = _gizmos.Where(g => g.GetGizmoType() == type);
            if (type == GizmoType.Custom && !string.IsNullOrEmpty(customGizmoName))
            {
                availableGizmos = availableGizmos.Where(g => g.GetGizmoName() == customGizmoName);
            }

            _currentGizmo = availableGizmos.FirstOrDefault();
            if (SelectedInstance != null)
            {
                _currentGizmo?.AttachGizmo(SelectedInstance.Render);
            }
            _currentGizmo?.Show();
            _currentGizmo?.HighlightAxis(TransformAxis);
            _currentGizmo?.ChangeLocalityRender(TransformLocality);
        });
    }

    public void SetGrid()
    {
        if (SelectedInstance == null)
        {
            return;
        }
            
        _gridStep.x = SelectedInstance.Render.GetSize().x;
        _gridStep.y = SelectedInstance.Render.GetSize().y;
        _gridStep.z = SelectedInstance.Render.GetSize().z;
        _gridRotation = (new quat(SelectedInstance.Render.GetRotation())).ToMat4;
        SetCursorCoordinates(SelectedInstance.Render.GetPosition());
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

    public vec3 GetCursorCoordinates()
    {
        return _cursor.GetPosition();
    }

    public void SetPalette(ViewportObject instance)
    {
        _palette[_currentPaletteIndex] = instance;
    }

    public ViewportObject? SpawnAtCursor()
    {
        if (_palette[_currentPaletteIndex] == null)
        {
            return null;
        }

        
        TransformMode = TransformMode.SELECTION;
        TransformAxis = TransformAxis.NONE;
        return _palette[_currentPaletteIndex];
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
            var k = glm.Radians(0.2f);
            var axis = TransformAxis switch
            {
                TransformAxis.X => vec3.UnitX,
                TransformAxis.Y => vec3.UnitY,
                TransformAxis.Z => vec3.UnitZ,
                _ => vec3.UnitX
            };
            Rotate(axis * k * delta);
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
        if (mode != TransformMode.TRANSLATE)
        {
            TransformLocality = TransformLocality.LOCAL;
        }
        SwitchGizmo((GizmoType)(int)TransformMode);
    }

    public void ToggleScale()
    {
        if (SelectedInstance == null || !SelectedInstance.IsTransformSupported(TransformMode.SCALE))
        {
            return;
        }
        
        SwitchEditMode(TransformMode.SCALE);
    }

    public void ToggleLocality()
    {
        if (SelectedInstance == null || TransformMode != TransformMode.TRANSLATE)
        {
            return;
        }

        TransformLocality = TransformLocality == TransformLocality.LOCAL ? TransformLocality.WORLD : TransformLocality.LOCAL;
        _renderContext.QueueRenderAction(() =>
        {
            _currentGizmo?.ChangeLocalityRender(TransformLocality);
        });
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
        _currentGizmo?.HighlightAxis(TransformAxis);
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
        SelectedInstance?.Render.Scale(offset + vec3.Ones, true);
        if (SelectedInstance is { Scale: not null })
        {
            var editorScale = SelectedInstance.Render.GetEditorScale();
            SelectedInstance.Scale.SetValue(new Vector3(editorScale.x, editorScale.y, editorScale.z));
        }

        if (SelectedInstance is { Transform: not null })
        {
            var renderTransform = SelectedInstance.Render.LocalTransform;
            SelectedInstance.Transform.SetValue(renderTransform.ToTwin());
        }
    }

    private void Translate(vec3 offset)
    {
        SelectedInstance?.Render.Translate(offset, TransformLocality == TransformLocality.LOCAL);
        if (SelectedInstance is { Position: not null })
        {
            var editorPos = SelectedInstance.Render.GetEditorPosition();
            SelectedInstance.Position.SetValue(new Vector3(editorPos.x, editorPos.y, editorPos.z));
        }
        
        if (SelectedInstance is { Transform: not null })
        {
            var renderTransform = SelectedInstance.Render.LocalTransform;
            SelectedInstance.Transform.SetValue(renderTransform.ToTwin());
        }
    }

    private void Rotate(vec3 offset)
    {
        SelectedInstance?.Render.Rotate(offset, true);
        if (SelectedInstance is { Rotation: not null })
        {
            var editorRot = vec3.Degrees(SelectedInstance.Render.GetEditorRotation());
            SelectedInstance.Rotation.SetValue(new Vector3(editorRot.x, editorRot.y, editorRot.z));
        }
        
        if (SelectedInstance is { Transform: not null })
        {
            var renderTransform = SelectedInstance.Render.LocalTransform;
            SelectedInstance.Transform.SetValue(renderTransform.ToTwin());
        }
    }

    private BillboardSet CreateBillboardSet(RenderContext renderContext, string billboardName, string billboardIconName, bool useDiffuseOnly = true)
    {
        var billboardSet = new BillboardSet(renderContext, renderContext.MeshFactory, billboardIconName, billboardName, useDiffuseOnly);
        return billboardSet;
    }
}

public enum TransformLocality
{
    LOCAL,
    WORLD
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