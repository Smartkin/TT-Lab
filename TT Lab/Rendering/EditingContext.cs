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

/// <summary>
/// Selection and transform tools of a viewport. Everything except drawing happens on the UI thread
/// </summary>
public class EditingContext
{
    private static readonly vec4 SelectionColor = new(1.0f, 0.62f, 0.12f, 1.0f);
    private const int GridCellsToEdge = 12;
    private const int GridCellsPerMajorLine = 5;
    // Grids cover at least this much of the screen around the selection, so they stay useful when zoomed out
    private const float GridMinimumPixels = 260.0f;

    public ViewportObject? SelectedInstance;
    public EditableObject? SelectedRenderable;
    public TransformMode TransformMode = TransformMode.SELECTION;
    public TransformAxis TransformAxis = TransformAxis.NONE;
    public TransformLocality TransformLocality = TransformLocality.LOCAL;

    private readonly EditorCursor _cursor;
    private const float CursorPixels = 12.0f;
    private const int CursorDashes = 16;
    private static readonly vec4 CursorRed = new(0.9f, 0.15f, 0.15f, 1.0f);
    private static readonly vec4 CursorWhite = new(1.0f, 1.0f, 1.0f, 1.0f);
    private readonly BillboardSet _positionsBillboards;
    private readonly BillboardSet _pathsBillboards;
    private readonly BillboardSet _particlesBillboards;
    private readonly BillboardSet _triggersBillboards;
    private readonly BillboardSet _camerasBillboards;
    private readonly BillboardSet _instancesBillboards;
    private readonly BillboardSet _aiPositionsBillboards;
    private readonly BillboardSet _chunkLinksBillboards;
    private readonly TransformGizmo _gizmo = new();
    private readonly Node _editCtxNode;
    private mat4 _dragStartTransform;
    private readonly RenderContext _renderContext;
    // Set on the UI thread and read when drawing
    private volatile bool _isGridShown = true;
    private volatile float _gridCellSize = 1.0f;

    public EditingContext(RenderContext context, Scene.Scene scene)
    {
        _renderContext = context;
        _editCtxNode = new Node(context, scene);
        _cursor = new EditorCursor();
        _positionsBillboards = CreateBillboardSet(context, "PositionsBillboards", "Position", KnownColor.Green);
        _triggersBillboards = CreateBillboardSet(context, "TriggersBillboards", "Trigger", KnownColor.DarkOrange);
        _camerasBillboards = CreateBillboardSet(context, "CamerasBillboards", "Camera", KnownColor.Blue);
        _instancesBillboards = CreateBillboardSet(context, "InstancesBillboards", "Instance");
        _aiPositionsBillboards = CreateBillboardSet(context, "AiPositionsBillboards", "AI_Position", KnownColor.Yellow);
        _pathsBillboards = CreateBillboardSet(context, "PathsBillboards", "Path", KnownColor.LightBlue);
        _particlesBillboards = CreateBillboardSet(context, "ParticlesBillboards", "Particle_Emitter", useDiffuseOnly: false);
        _chunkLinksBillboards = CreateBillboardSet(context, "ChunkLinksBillboard", "Chunk_Link", KnownColor.Red);
    }

    public TransformGizmo Gizmo => _gizmo;

    public bool IsGridShown
    {
        get => _isGridShown;
        set => _isGridShown = value;
    }

    public Node GetEditorNode()
    {
        return _editCtxNode;
    }

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

    // What's selected along with the first object: the gizmo works on the first, the others follow it. Replaced whole, never changed in
    // place: the render thread draws it while the UI thread selects (selecting everything broke a frame being drawn)
    private ViewportObject[] _extraSelection = [];
    private readonly List<(ViewportObject Object, mat4 Local, GroupTransform.Placement Placement)> _extraStarts = new();

    /// <summary>
    /// The objects selected after the first one, which the gizmo edits
    /// </summary>
    public IReadOnlyList<ViewportObject> ExtraSelection => _extraSelection;

    /// <summary>
    /// Everything selected, the gizmo's object first
    /// </summary>
    public IEnumerable<ViewportObject> Selection => SelectedInstance == null ? Enumerable.Empty<ViewportObject>() : _extraSelection.Prepend(SelectedInstance);

    public int SelectionCount => SelectedInstance == null ? 0 : 1 + _extraSelection.Length;

    public void Deselect()
    {
        _gizmo.EndDrag();
        _gizmo.Hovered = GizmoHandle.None;
        SelectedInstance?.Render.Deselect();
        SelectedInstance = null;
        SelectedRenderable = null;
        foreach (var extra in _extraSelection)
        {
            extra.Render.Deselect();
        }

        _extraSelection = [];
    }

    public void Select(ViewportObject instance)
    {
        if (SelectedInstance == instance && _extraSelection.Length == 0)
        {
            return;
        }

        Deselect();
        SelectedInstance = instance;
        SelectedRenderable = instance.Render;
        instance.Render.Select();
    }

    /// <summary>
    /// Selects the objects together, the first gets the gizmo
    /// </summary>
    public void SelectMany(IReadOnlyList<ViewportObject> objects)
    {
        Deselect();
        if (objects.Count == 0)
        {
            return;
        }

        Select(objects[0]);
        var extras = objects.Skip(1).Where(other => other != objects[0]).Distinct().ToArray();
        foreach (var other in extras)
        {
            other.Render.Select();
        }

        _extraSelection = extras;
    }

    public bool IsSelected(ViewportObject instance) => SelectedInstance == instance || _extraSelection.Contains(instance);

    public void AddToSelection(ViewportObject instance)
    {
        if (SelectedInstance == null)
        {
            Select(instance);
            return;
        }

        if (IsSelected(instance))
        {
            return;
        }

        _extraSelection = [.. _extraSelection, instance];
        instance.Render.Select();
    }

    /// <summary>
    /// Takes the object out of the selection, the next one gets the gizmo when it had it
    /// </summary>
    public void RemoveFromSelection(ViewportObject instance)
    {
        if (Array.IndexOf(_extraSelection, instance) >= 0)
        {
            _extraSelection = _extraSelection.Where(other => other != instance).ToArray();
            instance.Render.Deselect();
            return;
        }

        if (SelectedInstance != instance)
        {
            return;
        }

        var rest = _extraSelection.ToList();
        SelectMany(rest);
    }

    public void SetTransformMode(TransformMode mode)
    {
        if (_gizmo.IsDragging)
        {
            return;
        }

        TransformMode = mode;
        TransformAxis = TransformAxis.NONE;
        _gizmo.Mode = mode;
        _gizmo.Hovered = GizmoHandle.None;
    }

    /// <summary>
    /// The grids around the selection have a line every step the moves snap to, whether they snap or not
    /// </summary>
    public void SetSnapping(bool isSnapping, GizmoSnapping snapping)
    {
        _gizmo.IsSnapping = isSnapping;
        _gizmo.Snapping = snapping;
        _gridCellSize = snapping.Translation;
    }

    public void SetTransformLocality(TransformLocality locality)
    {
        if (_gizmo.IsDragging)
        {
            return;
        }

        TransformLocality = locality;
        _gizmo.Locality = locality;
    }

    /// <summary>
    /// Gizmo only shows up for selections that can be transformed the way the current tool does
    /// </summary>
    public bool IsGizmoShown => SelectedInstance is { } instance && TransformMode != TransformMode.SELECTION && instance.IsTransformSupported(TransformMode);

    public bool IsDraggingGizmo => _gizmo.IsDragging;

    public bool UpdateGizmoHover(in FrameCamera camera, vec2 mouse)
    {
        if (_gizmo.IsDragging)
        {
            return true;
        }

        _gizmo.Hovered = IsGizmoShown ? _gizmo.HitTest(camera, GetTargetTransform(SelectedInstance!), mouse) : GizmoHandle.None;
        return _gizmo.Hovered != GizmoHandle.None;
    }

    public bool BeginGizmoDrag(in FrameCamera camera, vec2 mouse)
    {
        if (!IsGizmoShown || !_gizmo.BeginDrag(camera, GetTargetTransform(SelectedInstance!), mouse))
        {
            return false;
        }

        _dragStartTransform = SelectedRenderable!.LocalTransform;
        _extraStarts.Clear();
        foreach (var extra in _extraSelection)
        {
            var target = GetTargetTransform(extra);
            _extraStarts.Add((extra, extra.Render.LocalTransform, new GroupTransform.Placement(target.Position, target.Rotation, target.Scale)));
        }

        return true;
    }

    public void UpdateGizmoDrag(in FrameCamera camera, vec2 mouse, bool invertSnapping = false)
    {
        if (!_gizmo.IsDragging || SelectedInstance == null)
        {
            return;
        }

        var start = _gizmo.DragStart!.Value;
        var result = _gizmo.Drag(camera, mouse, invertSnapping);
        ApplyTransform(SelectedInstance, start, result);
        ApplyToExtras(start, result);
    }

    public void EndGizmoDrag()
    {
        _gizmo.EndDrag();
    }

    /// <summary>
    /// Puts the selection back to where it was before the drag
    /// </summary>
    public void CancelGizmoDrag()
    {
        if (!_gizmo.IsDragging || SelectedInstance == null)
        {
            _gizmo.EndDrag();
            return;
        }

        var start = _gizmo.DragStart!.Value;
        _gizmo.EndDrag();
        ApplyTransform(SelectedInstance, start, start);
        ApplyToExtras(start, start);
    }

    // The other selected objects follow the gizmo's, only in what their data lets them change
    private void ApplyToExtras(GizmoTransform start, GizmoTransform result)
    {
        foreach (var (extra, local, placement) in _extraStarts)
        {
            var render = extra.Render;
            if (extra.Transform != null)
            {
                if (extra.Transform.IsReadOnly)
                {
                    continue;
                }

                var matrix = GroupTransform.ApplyToMatrix(TransformMode, start, result, local);
                render.SetLocalTransform(matrix);
                extra.Transform.SetValue(extra.GetDataFromTransform(matrix));
                continue;
            }

            var moved = GroupTransform.Apply(TransformMode, start, result, placement);
            if (extra.Position is { IsReadOnly: false } position)
            {
                render.SetPosition(moved.Position);
                position.SetValue(extra.PositionData(moved.Position));
            }

            if (TransformMode == TransformMode.ROTATE && extra.Rotation is { IsReadOnly: false } rotation)
            {
                render.SetRotation(moved.Rotation);
                var degrees = vec3.Degrees(moved.Rotation.ToEulerAngles());
                rotation.SetValue(new Vector3(degrees.x, degrees.y, degrees.z));
            }

            if (TransformMode == TransformMode.SCALE && extra.Scale is { IsReadOnly: false } scale)
            {
                render.SetScale(moved.Scale);
                scale.SetValue(new Vector3(moved.Scale.x, moved.Scale.y, moved.Scale.z));
            }
        }
    }

    public void DrawPrimitives(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (_cursor.IsShown)
        {
            DrawCursor(renderer, camera, _cursor.GetPosition());
        }

        var instance = SelectedInstance;
        var render = instance?.Render;
        if (instance == null || render == null)
        {
            return;
        }

        if (_isGridShown)
        {
            DrawGrids(renderer, camera, instance);
        }

        renderer.DrawWireBox(render.GetBoundsTransform(), SelectionColor, 2.0f, PrimitiveLayer.WorldXRay);
        foreach (var extra in _extraSelection)
        {
            renderer.DrawWireBox(extra.Render.GetBoundsTransform(), SelectionColor, 1.0f, PrimitiveLayer.WorldXRay);
        }

        if (IsGizmoShown)
        {
            _gizmo.Draw(renderer, camera, GetTargetTransform(instance));
        }
    }

    // Like Blender's 3D cursor, only in 3D: a ring of red and white dashes on each of the three planes with the axes sticking out of
    // them, the same size on screen wherever it is, drawn over everything
    private static void DrawCursor(PrimitiveRenderer renderer, in FrameCamera camera, vec3 position)
    {
        var radius = camera.WorldUnitsPerPixel(position) * CursorPixels;
        for (var plane = 0; plane < 3; plane++)
        {
            var u = GetAxis((plane + 1) % 3);
            var v = GetAxis((plane + 2) % 3);
            for (var dash = 0; dash < CursorDashes; dash++)
            {
                var from = dash * 2.0f * MathF.PI / CursorDashes;
                var to = (dash + 1) * 2.0f * MathF.PI / CursorDashes;
                var start = position + (u * MathF.Cos(from) + v * MathF.Sin(from)) * radius;
                var end = position + (u * MathF.Cos(to) + v * MathF.Sin(to)) * radius;
                renderer.DrawLine(start, end, dash % 2 == 0 ? CursorRed : CursorWhite, 2.0f, PrimitiveLayer.Overlay);
            }
        }

        for (var axis = 0; axis < 3; axis++)
        {
            var direction = GetAxis(axis);
            var color = TransformGizmo.GetAxisColor(axis);
            renderer.DrawLine(position + direction * radius * 1.3f, position + direction * radius * 2.0f, color, 2.0f, PrimitiveLayer.Overlay);
            renderer.DrawLine(position - direction * radius * 1.3f, position - direction * radius * 2.0f, color, 2.0f, PrimitiveLayer.Overlay);
        }
    }

    // Grids on the three planes through the selection lined up with the gizmo. They stay where a drag started, so the selection can be
    // seen moving over them
    private void DrawGrids(PrimitiveRenderer renderer, in FrameCamera camera, ViewportObject instance)
    {
        var target = _gizmo.DragStart ?? GetTargetTransform(instance);
        var orientation = _gizmo.GetOrientation(target);
        var cellSize = _gridCellSize;
        var radius = MathF.Max(cellSize * GridCellsToEdge, camera.WorldUnitsPerPixel(target.Position) * GridMinimumPixels);
        for (var normal = 0; normal < 3; normal++)
        {
            var first = (normal + 1) % 3;
            var second = (normal + 2) % 3;
            var emphasis = GetGridEmphasis(normal);
            renderer.DrawGrid(target.Position, orientation * GetAxis(first), orientation * GetAxis(second), radius, cellSize, GridCellsPerMajorLine,
                TransformGizmo.GetAxisColor(normal) with { w = 0.3f * emphasis },
                TransformGizmo.GetAxisColor(first) with { w = MathF.Min(0.7f * emphasis, 1.0f) },
                TransformGizmo.GetAxisColor(second) with { w = MathF.Min(0.7f * emphasis, 1.0f) },
                PrimitiveLayer.WorldXRay);
        }
    }

    // Planes a drag moves the selection on, or turns it in, stand out from the rest
    private float GetGridEmphasis(int normal)
    {
        var active = _gizmo.Active;
        return active switch
        {
            GizmoHandle.AxisX or GizmoHandle.AxisY or GizmoHandle.AxisZ => normal == active - GizmoHandle.AxisX ? 0.4f : 1.5f,
            GizmoHandle.PlaneX or GizmoHandle.PlaneY or GizmoHandle.PlaneZ => normal == active - GizmoHandle.PlaneX ? 1.5f : 0.4f,
            GizmoHandle.RingX or GizmoHandle.RingY or GizmoHandle.RingZ => normal == active - GizmoHandle.RingX ? 1.5f : 0.4f,
            _ => 1.0f,
        };
    }

    private static vec3 GetAxis(int axis)
    {
        return axis switch
        {
            0 => vec3.UnitX,
            1 => vec3.UnitY,
            _ => vec3.UnitZ
        };
    }

    public bool IsInstanceSelected()
    {
        return SelectedInstance != null;
    }

    public bool IsCursorPlaced => _cursor.IsShown;

    public void SetCursorCoordinates(vec3 pos)
    {
        _cursor.SetPosition(pos);
    }

    public vec3 GetCursorCoordinates()
    {
        return _cursor.GetPosition();
    }

    // Transforms used to be done by dragging anywhere with an axis picked on the keyboard, the chunk editor still calls these
    public bool StartTransform(float x, float y)
    {
        return false;
    }

    public void EndTransform(float x, float y)
    {
    }

    public void UpdateTransform(float x, float y)
    {
    }

    public void SetTransformAxis(TransformAxis axis)
    {
        TransformAxis = TransformAxis == axis ? TransformAxis.NONE : axis;
    }

    public void ToggleScale()
    {
        ToggleTransformMode(TransformMode.SCALE);
    }

    public void ToggleLocality()
    {
        SetTransformLocality(TransformLocality == TransformLocality.LOCAL ? TransformLocality.WORLD : TransformLocality.LOCAL);
    }

    public void ToggleTranslate()
    {
        ToggleTransformMode(TransformMode.TRANSLATE);
    }

    public void ToggleRotate()
    {
        ToggleTransformMode(TransformMode.ROTATE);
    }

    private void ToggleTransformMode(TransformMode mode)
    {
        SetTransformMode(TransformMode == mode ? TransformMode.SELECTION : mode);
    }

    private static GizmoTransform GetTargetTransform(ViewportObject instance)
    {
        var render = instance.Render;
        if (instance.Transform != null)
        {
            return new GizmoTransform(render.WorldTransform.Column3.xyz, render.GetRotationQuat(), render.GetScale());
        }

        return new GizmoTransform(render.WorldTransform.Column3.xyz, render.Orientation, render.GetEditorScale());
    }

    private void ApplyTransform(ViewportObject instance, GizmoTransform start, GizmoTransform result)
    {
        var render = instance.Render;
        if (instance.Transform != null)
        {
            // Changing only what the drag changed keeps whatever else is in the matrix
            var matrix = _dragStartTransform;
            switch (TransformMode)
            {
                case TransformMode.TRANSLATE:
                    matrix.Column3 = new vec4(result.Position, 1.0f);
                    break;
                case TransformMode.ROTATE:
                    var rotation = result.Rotation * start.Rotation.Inverse;
                    matrix = mat4.Translate(start.Position) * rotation.ToMat4 * mat4.Translate(-start.Position) * matrix;
                    break;
                case TransformMode.SCALE:
                    matrix *= mat4.Scale(result.Scale / NonZero(start.Scale));
                    break;
            }

            render.SetLocalTransform(matrix);
            instance.Transform.SetValue(instance.GetDataFromTransform(matrix));
            return;
        }

        switch (TransformMode)
        {
            case TransformMode.TRANSLATE:
                render.SetPosition(result.Position);
                instance.Position?.SetValue(instance.PositionData(result.Position));
                break;
            case TransformMode.ROTATE:
                render.SetRotation(result.Rotation);
                var degrees = vec3.Degrees(result.Rotation.ToEulerAngles());
                instance.Rotation?.SetValue(new Vector3(degrees.x, degrees.y, degrees.z));
                break;
            case TransformMode.SCALE:
                render.SetScale(result.Scale);
                instance.Scale?.SetValue(new Vector3(result.Scale.x, result.Scale.y, result.Scale.z));
                break;
        }
    }

    private static vec3 NonZero(vec3 value) => GroupTransform.NonZero(value);

    private BillboardSet CreateBillboardSet(RenderContext renderContext, string billboardName, string billboardIconName, KnownColor? color = null, bool useDiffuseOnly = true)
    {
        var billboardSet = new BillboardSet(renderContext, renderContext.MeshFactory, billboardIconName, billboardName, useDiffuseOnly);
        if (color != null)
        {
            var knownColor = Color.FromKnownColor(color.Value);
            billboardSet.Diffuse = new vec4(knownColor.R / 255.0f, knownColor.G / 255.0f, knownColor.B / 255.0f, 1.0f);
        }

        _editCtxNode.AddChild(billboardSet);
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
