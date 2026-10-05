using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Threading;
using GlmSharp;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using Silk.NET.Input;
using Silk.NET.Maths;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Controls;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Input;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.Gizmo;
using TT_Lab.Rendering.Scene;
using TT_Lab.ServiceProviders;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Action = System.Action;
using Vector2 = System.Numerics.Vector2;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.ViewModels;

/// <summary>
/// Kind of viewport objects that can be shown or hidden from the toolbar
/// </summary>
public partial class ViewportLayerToggle : ReactiveObject
{
    [Reactive]
    private bool _isShown;

    public ViewportLayerToggle(string name, ViewportObjectCategory category, bool isShown)
    {
        Name = name;
        Category = category;
        _isShown = isShown;
    }

    public string Name { get; }
    public ViewportObjectCategory Category { get; }
}

/// <summary>
/// Step one kind of transform snaps to, typed in or picked from the presets
/// </summary>
public class SnapStep : ReactiveObject
{
    private float _value;
    private string _text;

    public SnapStep(float value, params float[] presets)
    {
        _value = value;
        _text = Format(value);
        Presets = presets.Select(Format).ToList();
    }

    public IReadOnlyList<string> Presets { get; }

    public float Value
    {
        get => _value;
        set
        {
            if (!(value > 0.0f) || !float.IsFinite(value) || _value == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _value, value);
            if (!TryParse(_text, out var typed) || typed != value)
            {
                _text = Format(value);
                this.RaisePropertyChanged(nameof(Text));
            }
        }
    }

    // Text that isn't a positive number stays as it's typed without changing the step, turning it back while typing would fight the user
    public string Text
    {
        get => _text;
        set
        {
            this.RaiseAndSetIfChanged(ref _text, value);
            if (TryParse(value, out var step))
            {
                Value = step;
            }
        }
    }

    public static string Format(float value)
    {
        return value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private static bool TryParse(string? text, out float value)
    {
        return (float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
                float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) && value > 0.0f && float.IsFinite(value);
    }
}

public partial class ViewportViewModel : ReactiveObject
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private ViewportObject? _selectedObject;

    [Reactive(SetModifier = AccessModifier.Private)]
    private TransformMode _activeTool = TransformMode.SELECTION;

    [Reactive(SetModifier = AccessModifier.Private)]
    private TransformLocality _transformSpace = TransformLocality.LOCAL;

    [Reactive]
    private bool _isSnapping;

    [Reactive]
    private bool _isGridShown = true;

    // Where the selection is, shown in the viewport's corner
    [Reactive(SetModifier = AccessModifier.Private)]
    private string _selectionInfo = string.Empty;

    // Tools that stand in for the chosen one when the selection can't be transformed that way, in the order they're tried
    private static readonly TransformMode[] FallbackTools = [TransformMode.TRANSLATE, TransformMode.ROTATE, TransformMode.SCALE];

    // Tool the user chose last, it's used again once something that can be transformed with it gets selected
    private TransformMode _chosenTool = TransformMode.SELECTION;

    // Replaced as a whole whenever objects come or go, so the UI thread can go through them while the render thread changes them
    private ViewportObject[] _viewportObjects = [];
    private readonly object _viewportObjectsLock = new();
    private Renderer? _renderer;

    private bool _isChunkViewport = false;
    // Set while the viewport puts an element into a list itself, it shows and selects the element's objects on its own
    private bool _isPlacingElement;
    private IInputContext? _inputContext;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;
    private Scene? _scene;
    private volatile bool _renderInit;
    private bool _firstRender = true;
    private RenderContext? _renderContext;
    private EditingContext? _editingContext;
    private DocumentViewModel? _document;
    private ivec2 ViewportSize => _renderContext == null ? ivec2.Ones : new ivec2((int)_renderContext.ViewportSize.x, (int)_renderContext.ViewportSize.y);
    private readonly CompositeDisposable _closeDisposables = new();
    private readonly FloorGrid _floorGrid = new();

    // Viewers of a single model turn the camera around the model instead of flying it
    private readonly OrbitCamera _orbit = new();
    private vec3 _modelMin = new(float.MaxValue);
    private vec3 _modelMax = new(float.MinValue);

    public ViewportViewModel()
    {
        Host = new ViewportHost();
        Host.Initialized += PrepareRender;
        LayerToggles =
        [
            new ViewportLayerToggle("Scenery", ViewportObjectCategory.Scenery, true),
            new ViewportLayerToggle("Dynamic scenery", ViewportObjectCategory.DynamicScenery, true),
            new ViewportLayerToggle("Dynamic scenery bounds", ViewportObjectCategory.DynamicSceneryBounds, true),
            new ViewportLayerToggle("Collision", ViewportObjectCategory.Collision, false),
            new ViewportLayerToggle("Skydome", ViewportObjectCategory.Skydome, true),
            new ViewportLayerToggle("Linked scenery", ViewportObjectCategory.LinkedScenery, true),
            new ViewportLayerToggle("Load walls", ViewportObjectCategory.LoadWalls, true),
            new ViewportLayerToggle("Link hulls", ViewportObjectCategory.LinkHulls, true),
            new ViewportLayerToggle("Instances", ViewportObjectCategory.Instances, true),
            new ViewportLayerToggle("Triggers", ViewportObjectCategory.Triggers, true),
            new ViewportLayerToggle("Cameras", ViewportObjectCategory.Cameras, true),
            new ViewportLayerToggle("Camera paths", ViewportObjectCategory.CameraPaths, true),
            new ViewportLayerToggle("Positions", ViewportObjectCategory.Positions, true),
            new ViewportLayerToggle("Paths", ViewportObjectCategory.Paths, true),
            new ViewportLayerToggle("AI positions", ViewportObjectCategory.AiPositions, true),
            new ViewportLayerToggle("AI paths", ViewportObjectCategory.AiPaths, true),
            new ViewportLayerToggle("Particles", ViewportObjectCategory.Particles, true),
            new ViewportLayerToggle("Lights", ViewportObjectCategory.Lights, true),
            new ViewportLayerToggle("Scenery bounds", ViewportObjectCategory.SceneryBounds, true),
        ];

        foreach (var toggle in LayerToggles)
        {
            toggle.WhenAnyValue(x => x.IsShown).Skip(1)
                .Subscribe(isShown => SetCategoryShown(toggle.Category, isShown))
                .DisposeWith(_closeDisposables);
        }

        TranslationSnap = new SnapStep(1.0f, 0.1f, 0.25f, 0.5f, 1.0f, 2.0f, 5.0f, 10.0f);
        RotationSnap = new SnapStep(15.0f, 1.0f, 5.0f, 10.0f, 15.0f, 22.5f, 30.0f, 45.0f, 90.0f);
        ScaleSnap = new SnapStep(0.1f, 0.01f, 0.05f, 0.1f, 0.25f, 0.5f, 1.0f);
        LoadSnappingPreferences();
        // Snapping is set up the same in all viewports, changing it in one changes it everywhere
        Preferences.PreferenceChanged += OnPreferenceChanged;
        Disposable.Create(() => Preferences.PreferenceChanged -= OnPreferenceChanged).DisposeWith(_closeDisposables);

        SelectToolCommand = ReactiveCommand.Create<TransformMode>(SetTool);
        ToggleTransformSpaceCommand = ReactiveCommand.Create(ToggleTransformSpace);
        ToggleSnappingCommand = ReactiveCommand.Create(() => { IsSnapping = !IsSnapping; });
        FrameSelectionCommand = ReactiveCommand.Create(FrameSelection);
        InitPrefabs();
        InitGameLaunch();
        InitSceneryMode();
        this.WhenAnyValue(x => x.ActiveTool).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsSelectTool));
            this.RaisePropertyChanged(nameof(IsTranslateTool));
            this.RaisePropertyChanged(nameof(IsRotateTool));
            this.RaisePropertyChanged(nameof(IsScaleTool));
            this.RaisePropertyChanged(nameof(SnapStepText));
        }).DisposeWith(_closeDisposables);
        this.WhenAnyValue(x => x.IsSnapping, x => x.IsGridShown, x => x.TranslationSnap.Value, x => x.RotationSnap.Value, x => x.ScaleSnap.Value)
            .Subscribe(_ =>
            {
                ApplySnapping();
                SaveSnappingPreferences();
                this.RaisePropertyChanged(nameof(SnapStepText));
            }).DisposeWith(_closeDisposables);
        this.WhenAnyValue(x => x.TransformSpace).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsWorldSpace));
            this.RaisePropertyChanged(nameof(TransformSpaceName));
        }).DisposeWith(_closeDisposables);
        this.WhenAnyValue(x => x.SelectedObject).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(CanTranslate));
            this.RaisePropertyChanged(nameof(CanRotate));
            this.RaisePropertyChanged(nameof(CanScale));
            ApplyTool();
        }).DisposeWith(_closeDisposables);
    }

    public IReadOnlyList<ViewportLayerToggle> LayerToggles { get; }
    public ReactiveCommand<TransformMode, Unit> SelectToolCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleTransformSpaceCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleSnappingCommand { get; }
    public ReactiveCommand<Unit, Unit> FrameSelectionCommand { get; }
    public bool IsSelectTool => ActiveTool == TransformMode.SELECTION;
    public bool IsTranslateTool => ActiveTool == TransformMode.TRANSLATE;
    public bool IsRotateTool => ActiveTool == TransformMode.ROTATE;
    public bool IsScaleTool => ActiveTool == TransformMode.SCALE;
    public bool CanTranslate => IsToolUsable(TransformMode.TRANSLATE);
    public bool CanRotate => IsToolUsable(TransformMode.ROTATE);
    public bool CanScale => IsToolUsable(TransformMode.SCALE);
    public bool IsWorldSpace => TransformSpace == TransformLocality.WORLD;
    public string TransformSpaceName => IsWorldSpace ? "World" : "Local";
    public bool IsChunkViewport => _isChunkViewport;
    public SnapStep TranslationSnap { get; }
    public SnapStep RotationSnap { get; }
    public SnapStep ScaleSnap { get; }

    // Step of what the active tool does, moving is what the grids show when only selecting
    public string SnapStepText => ActiveTool switch
    {
        TransformMode.ROTATE => $"{SnapStep.Format(RotationSnap.Value)}°",
        TransformMode.SCALE => $"×{SnapStep.Format(ScaleSnap.Value)}",
        _ => SnapStep.Format(TranslationSnap.Value),
    };

    public void Init(DocumentViewModel document)
    {
        _document = document;
        _isChunkViewport = document.DocumentModel is LevelChunk;
        this.RaisePropertyChanged(nameof(IsChunkViewport));
        FollowGameLaunch(document);

        this.WhenAnyValue(x => x._document!.IsReady)
            .Where(x => x)
            .Take(1)
            .Subscribe(_ =>
            {
                // Without a render context the scene gets built once PrepareRender receives one
                if (_renderInit || _renderContext == null)
                {
                    if (!_renderInit)
                    {
                        ReportLoading(StartingStage);
                    }

                    return;
                }

                _renderContext.QueueRenderAction(InitScene);
            }).DisposeWith(_closeDisposables);

        this.WhenAnyValue(x => x._document!.Inspector).ObserveOn(RxSchedulers.MainThreadScheduler)
            .WhereNotNull()
            .Subscribe(inspector => SelectInspected(inspector.Property))
            .DisposeWith(_closeDisposables);

        document.PropertyGraph.Changed += PropertyGraphOnChanged;
        document.PropertyGraph.Changed += FollowCollisionShape;
        Disposable.Create(() =>
        {
            document.PropertyGraph.Changed -= PropertyGraphOnChanged;
            document.PropertyGraph.Changed -= FollowCollisionShape;
        }).DisposeWith(_closeDisposables);
    }

    public RenderContext? GetRenderContext()
    {
        return _renderContext;
    }

    public IReadOnlyList<ViewportObject> GetViewportObjects()
    {
        return _viewportObjects;
    }

    public void Close()
    {
        _closeDisposables.Dispose();
        Host.Initialized -= PrepareRender;
        if (_keyboard != null)
        {
            _keyboard.KeyDown -= KeyboardOnKeyDown;
        }

        if (_mouse != null)
        {
            _mouse.MouseMove -= OnMouseMove;
            _mouse.MouseDown -= OnMouseDown;
            _mouse.MouseUp -= OnMouseUp;
            _mouse.Scroll -= OnMouseScroll;
        }

        _inputContext?.Dispose();
        // Everything made for the context goes with it on the render thread
        Host.Dispose();
    }

    /// <summary>
    /// GL context, render thread and frames of the viewport, kept while the editor is open no matter which control shows them
    /// </summary>
    public ViewportHost Host { get; }

    // The host's context lives as long as this, so the scene only gets built once
    private void PrepareRender(RenderContext renderContext)
    {
        _renderContext = renderContext;
        var inputContext = new LabInputContext(Host);
        _inputContext = inputContext;
        _mouse = inputContext.Mice[0];
        _keyboard = inputContext.Keyboards[0];
        _keyboard.KeyDown += KeyboardOnKeyDown;
        _mouse.MouseMove += OnMouseMove;
        _mouse.MouseDown += OnMouseDown;
        _mouse.MouseUp += OnMouseUp;
        _mouse.Scroll += OnMouseScroll;

        renderContext.QueueRenderAction(() =>
        {
            _renderer = new Renderer(renderContext);
            _renderer.FinishRender += RendererOnFinishRender;
            _renderer.SceneInitialized += RendererOnSceneInitialized;
            _renderer.FramebufferResize += RendererOnFramebufferResize;
            inputContext.SetView(_renderer);
            _renderer.InitInput(inputContext);

            _scene = new Scene(renderContext, "ROOT_SCENE");
            _scene.UpdateResolution(ViewportSize);
            _renderer.RegisterForRendering(_scene.Camera);
            _renderer.Camera = _scene.Camera;

            _editingContext = new EditingContext(renderContext, _scene);
            _editingContext.SetTransformMode(ActiveTool);
            _editingContext.SetTransformLocality(TransformSpace);
            ApplySnapping();
            _renderer.DrawPrimitives += _editingContext.DrawPrimitives;
            _renderer.DrawPrimitives += DrawFloorGrid;
            _renderer.DrawPrimitives += DrawSceneryEditing;
            _renderer.DrawPrimitives += DrawPrefabPreview;

            if (_document is { IsReady: true })
            {
                renderContext.QueueRenderAction(InitScene);
            }

            _renderer.Update += RendererOnUpdate;
        });
    }

    private void RendererOnFramebufferResize(Vector2D<int> _)
    {
        _scene?.UpdateResolution(ViewportSize);
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button != MouseButton.Left)
        {
            return;
        }

        if (_editingContext is { IsDraggingGizmo: true })
        {
            _editingContext.EndGizmoDrag();
            EndDragStep();
            return;
        }

        var pressed = _leftPress;
        _leftPress = null;
        if (IsRubberBanding)
        {
            IsRubberBanding = false;
            SelectInRubberBand();
            return;
        }

        if (pressed != null)
        {
            MouseSelect(mouse.Position.X, mouse.Position.Y);
        }
    }

    // A drag of the gizmo is one step to undo, however long it takes
    private IDisposable? _dragStep;

    private void BeginDragStep()
    {
        _dragStep?.Dispose();
        var name = SelectedObject == _trianglePivot && _selectedTriangles.Length > 0 ? (_selectedTriangles.Length > 1 ? $"{_selectedTriangles.Length} collision triangles" : "a collision triangle")
            : SelectedObject?.DuplicatedElement?.GetValue() is AssetData.Instance.Scenery.SceneryPlacement ? (SelectionCount > 1 ? $"{SelectionCount} meshes" : "a mesh")
            : (SelectedObject?.Property.Find("[data]")?.GetValue() as IAsset)?.Alias ?? SelectedObject?.DocumentName ?? "the selection";
        var verb = ActiveTool switch
        {
            TransformMode.ROTATE => "Rotated",
            TransformMode.SCALE => "Scaled",
            _ => "Moved",
        };
        _dragStep = _document?.History.BeginGroup($"{verb} {name}");
    }

    private void EndDragStep()
    {
        _dragStep?.Dispose();
        _dragStep = null;
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (_editingContext == null || _scene == null || !_isChunkViewport)
        {
            return;
        }

        if (_editingContext.IsDraggingGizmo)
        {
            // Another button during a drag gives up on it
            if (button != MouseButton.Left)
            {
                _editingContext.CancelGizmoDrag();
                EndDragStep();
            }

            return;
        }

        if (button != MouseButton.Left)
        {
            return;
        }

        var pos = mouse.Position;
        if (_editingContext.BeginGizmoDrag(_scene.Camera.GetFrameCamera(), new vec2(pos.X, pos.Y)))
        {
            BeginDragStep();
            return;
        }

        // A click selects on release, dragging from here on selects everything within the rectangle instead
        _leftPress = new vec2(pos.X, pos.Y);
    }

    // Where the left button went down outside the gizmo, until it comes up
    private vec2? _leftPress;
    private const float RubberBandThreshold = 4.0f;

    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isRubberBanding;

    [Reactive(SetModifier = AccessModifier.Private)]
    private Rect _rubberBand;

    private void UpdateRubberBand(vec2 pos)
    {
        if (_leftPress is not { } press)
        {
            return;
        }

        if (!IsRubberBanding && MathF.Abs(pos.x - press.x) < RubberBandThreshold && MathF.Abs(pos.y - press.y) < RubberBandThreshold)
        {
            return;
        }

        IsRubberBanding = true;
        RubberBand = new Rect(new Point(MathF.Min(press.x, pos.x), MathF.Min(press.y, pos.y)), new Point(MathF.Max(press.x, pos.x), MathF.Max(press.y, pos.y)));
    }

    // Selects every visible object whose box's center lies within the rectangle, added to the selection with Shift held
    private void SelectInRubberBand()
    {
        if (_scene == null || _editingContext == null)
        {
            return;
        }

        if (IsEditingTriangles)
        {
            SelectTrianglesInRubberBand();
            return;
        }

        var camera = _scene.Camera.GetFrameCamera();
        var rect = RubberBand;
        var inside = _viewportObjects.Where(viewportObject => IsPickable(viewportObject)
                                                             && viewportObject.PositionConverter == null
                                                             && camera.WorldToScreen(viewportObject.Render.GetBoundsTransform().Column3.xyz, out var screen)
                                                             && rect.Contains(new Point(screen.x, screen.y)))
            .ToList();
        if (IsShiftPressed())
        {
            inside = _editingContext.Selection.Concat(inside).Distinct().ToList();
        }

        SelectObjects(inside, false);
    }

    private bool IsShiftPressed()
    {
        return _keyboard != null && (_keyboard.IsKeyPressed(Key.ShiftLeft) || _keyboard.IsKeyPressed(Key.ShiftRight));
    }

    /// <summary>
    /// Everything selected, the object the gizmo edits first
    /// </summary>
    public IReadOnlyList<ViewportObject> SelectedObjects => _editingContext?.Selection.ToList() ?? [];

    public int SelectionCount => _editingContext?.SelectionCount ?? 0;

    /// <summary>
    /// Selects the objects together, the first gets the gizmo and is the one shown in the inspector when asked
    /// </summary>
    internal void SelectObjects(IReadOnlyList<ViewportObject> objects, bool openInspector)
    {
        if (_editingContext == null)
        {
            return;
        }

        if (objects.Count == 0)
        {
            _editingContext.Deselect();
            SelectedObject = null;
            return;
        }

        _editingContext.SelectMany(objects);
        SelectedObject = objects[0];
        RaiseSelectionChanged();
        if (openInspector)
        {
            OpenInspectorFor(objects[0]);
        }
    }

    private void SelectAll()
    {
        if (IsEditingTriangles)
        {
            SelectAllTriangles();
            return;
        }

        if (IsEditingMeshes)
        {
            SelectAllPlacements();
            return;
        }

        SelectObjects(_viewportObjects.Where(viewportObject => viewportObject.Render.IsVisible && viewportObject.Render.IsSelectable
                                                               && viewportObject.PositionConverter == null && viewportObject.Property.Find("[data]")?.GetValue() is SerializableInstance { LayoutID: not null }).ToList(), false);
    }

    private void RaiseSelectionChanged()
    {
        this.RaisePropertyChanged(nameof(SelectedObjects));
        this.RaisePropertyChanged(nameof(SelectionCount));
    }

    private void MouseSelect(float x, float y)
    {
        if (_renderer == null || _scene == null || _keyboard == null || _editingContext == null)
        {
            return;
        }

        if (IsDrawingTriangle)
        {
            AddTrianglePoint(x, y);
            return;
        }

        if (IsEditingTriangles && !_keyboard.IsKeyPressed(Key.ControlLeft))
        {
            SelectTriangleAt(x, y);
            return;
        }

        var ray = _scene.Camera.GetFrameCamera().ScreenRay(new vec2(x, y));
        ViewportObject? result = null;
        if (!_keyboard.IsKeyPressed(Key.ControlLeft))
        {
            var minDistance = float.MaxValue;
            // Placed meshes are picked by their triangles, the boxes of big ones cover the ones in front of them
            result = IsEditingMeshes ? PickPlacement(ray) : null;
            foreach (var viewportObject in IsEditingMeshes ? Array.Empty<ViewportObject>() : _viewportObjects)
            {
                var instance = viewportObject.Render;
                if (!IsPickable(viewportObject))
                {
                    continue;
                }

                var distance = GizmoMath.IntersectBox(ray, instance.GetBoundsTransform());
                if (distance == null || !(distance < minDistance))
                {
                    continue;
                }

                result = viewportObject;
                minDistance = distance.Value;
            }

            if (result != null)
            {
                if (IsShiftPressed())
                {
                    ToggleSelection(result);
                }
                else
                {
                    SelectObject(result, true);
                }

                return;
            }
        }

        if (IsShiftPressed())
        {
            return;
        }

        _editingContext.Deselect();
        SelectedObject = null;
        if (!TryHitCollision(x, y, out var cursorHit))
        {
            return;
        }

        _editingContext.SetCursorCoordinates(cursorHit);
    }

    // Where the ray through the viewport point hits the chunk's collision, if it does
    private bool TryHitCollision(float x, float y, out vec3 hit)
    {
        hit = default;
        var collision = _viewportObjects.FirstOrDefault(viewportObject => viewportObject.Category == ViewportObjectCategory.Collision);
        if (_scene == null || collision?.UserData is not CollisionData colData)
        {
            return false;
        }

        var ray = _scene.Camera.GetFrameCamera().ScreenRay(new vec2(x, y));
        var closestHit = float.MaxValue;
        foreach (var triangle in colData.Triangles)
        {
            var hitPos = new vec3();
            var distance = float.MaxValue;
            var p1 = colData.Vertexes[triangle.Face.Indexes![0]];
            var p2 = colData.Vertexes[triangle.Face.Indexes[1]];
            var p3 = colData.Vertexes[triangle.Face.Indexes[2]];
            if (!MathExtension.IntersectRayTriangle(ray.Origin, ray.Direction, new vec3(p1.X, p1.Y, p1.Z), new vec3(p2.X, p2.Y, p2.Z), new vec3(p3.X, p3.Y, p3.Z), ref distance, ref hitPos)
                || !(distance < closestHit))
            {
                continue;
            }

            hit = hitPos;
            closestHit = distance;
        }

        return closestHit < float.MaxValue;
    }

    // Shift+click takes an object into the selection or out of it
    private void ToggleSelection(ViewportObject viewportObject)
    {
        if (_editingContext == null)
        {
            return;
        }

        if (_editingContext.IsSelected(viewportObject))
        {
            _editingContext.RemoveFromSelection(viewportObject);
            SelectedObject = _editingContext.SelectedInstance;
        }
        else
        {
            _editingContext.AddToSelection(viewportObject);
            SelectedObject = _editingContext.SelectedInstance;
        }

        RaiseSelectionChanged();
    }

    internal void SelectObject(ViewportObject viewportObject, bool openInspector)
    {
        _editingContext?.Select(viewportObject);
        SelectedObject = viewportObject;
        RaiseSelectionChanged();
        if (openInspector)
        {
            OpenInspectorFor(viewportObject);
        }
    }

    // Parts of something, like a point of a path, open what they belong to and bring themselves into view in it
    private void OpenInspectorFor(ViewportObject viewportObject)
    {
        if (viewportObject.InspectorRoot != null)
        {
            _document?.OpenInspector(viewportObject.InspectorRoot);
            return;
        }

        var inspected = viewportObject.Property.PropertyType == typeof(LabURI) ? viewportObject.Property["[data]"] : viewportObject.Property;
        _document?.OpenInspector(inspected, viewportObject.InspectorFocus);
    }

    private void SelectInspected(PropertyNode inspected)
    {
        if (_editingContext == null || (SelectedObject != null && IsInspecting(SelectedObject, inspected)))
        {
            return;
        }

        var viewportObject = _viewportObjects.FirstOrDefault(viewportObject => viewportObject.Render.IsSelectable && viewportObject.Mode == EditMode && IsInspecting(viewportObject, inspected));
        if (viewportObject != null)
        {
            SelectObject(viewportObject, false);
        }
    }

    private static bool IsInspecting(ViewportObject viewportObject, PropertyNode inspected)
    {
        if (viewportObject.InspectorRoot != null)
        {
            return viewportObject.InspectorRoot == inspected;
        }

        return viewportObject.Property == inspected || viewportObject.Property.Find("[data]") == inspected;
    }

    // A path keeps as many points as the game's shortest ones have
    private const int MinPathPoints = PathData.MinPoints;

    private void DeleteInstance()
    {
        if (SelectedObject == null)
        {
            return;
        }

        DeleteObjects(SelectedObjects);
    }

    // Instances go out of the chunk and objects standing for an element of a list (an emitter, a link, a point of a path) take the
    // element out of its list, as one step. Handles of other parts, like a camera's points, stay: they aren't things of their own. The
    // selection's resource used to go whole, an emitter's took every emitter of the chunk with it
    private void DeleteObjects(IReadOnlyList<ViewportObject> objects)
    {
        if (_document == null)
        {
            return;
        }

        var resources = objects.Where(viewportObject => viewportObject.DuplicatedElement == null && viewportObject.InspectorFocus == null)
            .Select(viewportObject => viewportObject.Property).Distinct().ToList();
        var elements = objects.Select(viewportObject => viewportObject.DuplicatedElement).OfType<PropertyNode>().Distinct()
            .Where(element => !resources.Any(resource => IsWithin(element, resource)))
            .ToList();
        foreach (var points in elements.Where(element => element.Parent?.GetValue() is List<Twinsanity.TwinsanityInterchange.Common.Vector3> && element.Parent.Name == nameof(PathData.Points))
                     .GroupBy(element => element.Parent).ToList())
        {
            if (points.Key!.Children.Count - points.Count() >= MinPathPoints)
            {
                continue;
            }

            elements.RemoveAll(points.Contains);
            // Every point of it selected is the path itself, fewer would leave it shorter than the game's shortest
            if (points.Count() == points.Key.Children.Count && objects.FirstOrDefault(viewportObject => points.Contains(viewportObject.DuplicatedElement!))?.Property is { } path)
            {
                resources.Add(path);
            }
            else
            {
                Log.WriteLine($"A path keeps at least {MinPathPoints} points, select all of them to delete the path", Log.LogType.Warning);
            }
        }

        if (resources.Count + elements.Count == 0)
        {
            return;
        }

        _editingContext?.Deselect();
        SelectedObject = null;
        RaiseSelectionChanged();
        var count = resources.Count + elements.Count;
        using var step = count > 1 ? _document.History.BeginGroup($"Deleted {count} things") : null;
        // From the last of each list, the indexes of the others stay what they are
        foreach (var element in elements.OrderByDescending(element => element.Index ?? 0))
        {
            element.Parent?.RemoveElement(element);
        }

        DeleteResources(resources);
    }

    /// <summary>
    /// Takes the chunk's resources out of it, one step to undo
    /// </summary>
    internal void DeleteResources(IReadOnlyList<PropertyNode> properties)
    {
        if (_document == null || properties.Count == 0)
        {
            return;
        }

        if (SelectedObjects.Any(viewportObject => properties.Contains(viewportObject.Property)))
        {
            _editingContext?.Deselect();
            SelectedObject = null;
            RaiseSelectionChanged();
        }

        _renderContext?.QueueRenderAction(() => RemoveViewportObjects(_viewportObjects.Where(viewportObject => properties.Contains(viewportObject.Property)).ToList()));
        var chunkResources = _document.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources))!;
        using var step = properties.Count > 1 ? _document.History.BeginGroup($"Deleted {properties.Count} resources") : null;
        foreach (var property in properties)
        {
            chunkResources.RemoveElement(property);
        }
    }

    /// <summary>
    /// A copy of the chunk's instance in the same place and layout, selected, one step to undo
    /// </summary>
    internal IAsset? DuplicateResource(PropertyNode property)
    {
        if (property.GetValue() is not LabURI uri || !AssetManager.Get().DoesAssetExist(uri) || AssetManager.Get().GetAsset(uri) is not SerializableInstance { LayoutID: not null } instance)
        {
            return null;
        }

        var copy = CreateInstanceCopy(instance);
        PlaceInstances([(copy, null)], $"Duplicated {instance.Alias}");
        return copy;
    }

    /// <summary>
    /// Selects the resource's object and moves the camera back until it's in view
    /// </summary>
    internal void ShowResource(PropertyNode property)
    {
        var viewportObject = _viewportObjects.FirstOrDefault(viewportObject => viewportObject.Property == property && viewportObject.Render.IsSelectable && viewportObject.Mode == EditMode);
        if (viewportObject == null)
        {
            return;
        }

        SelectObject(viewportObject, false);
        FrameSelection();
    }

    // Copies the selection where it is and selects the copy, so it can be moved away right after. An object standing for an element of
    // a list (an emitter, a point of a path, a link) gets a copy of the element next to it, an instance of a layout a copy of the instance
    private void DuplicateSelection()
    {
        var selected = SelectedObject;
        if (selected == null || _renderContext == null)
        {
            return;
        }

        if (SelectionCount > 1)
        {
            // Several instances get their copies together, in place, selected in their stead
            var copies = SelectedObjects.Where(viewportObject => viewportObject.Property.Find("[data]")?.GetValue() is SerializableInstance { LayoutID: not null })
                .Select(viewportObject => viewportObject.Property).Distinct()
                .Select(property => (CreateInstanceCopy(property.Find("[data]")!.GetValue<IAsset>()!), (vec3?)null)).ToList();
            if (copies.Count > 0)
            {
                PlaceInstances(copies, $"Duplicated {copies.Count} instances");
            }

            return;
        }

        if (selected.DuplicatedElement is { } element)
        {
            DuplicateElement(selected, element);
            return;
        }

        if (selected.Property.Find("[data]")?.GetValue() is SerializableInstance { LayoutID: not null })
        {
            CreateNewInstance(selected, false);
        }
    }

    private void DuplicateElement(ViewportObject selected, PropertyNode element)
    {
        if (element.Parent is not { } list || element.Index is not { } index || element.GetValue() is not { } value)
        {
            return;
        }

        if (list.IsFull)
        {
            Log.WriteLine($"{list.Name} has {list.MaxElements}, as many as the game takes", Log.LogType.Warning);
            return;
        }

        _editingContext?.Deselect();
        SelectedObject = null;
        PropertyNode? copy;
        _isPlacingElement = true;
        try
        {
            copy = list.InsertElement(index + 1, CloneUtils.DeepClone(value, value.GetType()));
        }
        finally
        {
            _isPlacingElement = false;
        }

        if (copy == null)
        {
            return;
        }

        // The objects of the element are named after its path
        RebuildViewportObjects(selected.Property, selected.DocumentName.Replace(element.Path, copy.Path));
    }

    private void CreateNewInstance(ViewportObject? objectToSpawn, bool atCursor = true)
    {
        if (objectToSpawn == null || _renderContext == null)
        {
            return;
        }

        var basedOn = objectToSpawn.Property["[data]"]!.GetValue<IAsset>();
        if (basedOn == null)
        {
            return;
        }

        var newInstance = CreateInstanceCopy(basedOn, atCursor);
        PlaceInstance(newInstance, atCursor, $"Placed {newInstance.Alias}");
    }

    // A new instance of the chunk with a copy of the instance's data, in the same layout
    private IAsset CreateInstanceCopy(IAsset basedOn, bool isNew = false)
    {
        var chunk = (LevelChunk)_document!.DocumentModel;
        var name = isNew ? $"New {basedOn.Type.Name} {(uint)Guid.NewGuid().GetHashCode()}" : $"{basedOn.Name} Copy {(uint)Guid.NewGuid().GetHashCode():X8}";
        return CreateInstance(basedOn.Type, name, (Enums.Layouts)basedOn.LayoutID!, asset =>
        {
            asset.SetData(basedOn.GetData<AbstractAssetData>().CopyFor(asset));
            return AssetCreationStatus.Success;
        });
    }

    // A new instance of the chunk's layout with the data the creator gives it, not among the chunk's resources yet
    private IAsset CreateInstance(Type type, string name, Enums.Layouts layout, Func<IAsset, AssetCreationStatus> createData)
    {
        var chunk = (LevelChunk)_document!.DocumentModel;
        return AssetFactory.CreateAsset(type, chunk.GetChunkFolder(), name, "",
            TwinIdGeneratorServiceProvider.GetGeneratorForChunk(type, chunk.AdditionalPath!, chunk.Package, layout),
            asset =>
            {
                var instanceAsset = (SerializableInstance)asset;
                instanceAsset.Chunk = chunk.AdditionalPath!;
                instanceAsset.AdditionalPath = chunk.AdditionalPath;
                instanceAsset.RegenerateLinks();
                return createData(asset);
            },
            layout)!;
    }

    // Puts the new instance among the chunk's resources and shows it, at the cursor when asked, and selects it
    private void PlaceInstance(IAsset newInstance, bool atCursor, string description)
    {
        PlaceInstances([(newInstance, atCursor ? _editingContext!.GetCursorCoordinates() : null)], description);
    }

    /// <summary>
    /// Puts the new instances among the chunk's resources and shows them, each at its place when it has one, and selects them all,
    /// the first shown in the inspector. One step to undo
    /// </summary>
    internal void PlaceInstances(IReadOnlyList<(IAsset Instance, vec3? Position)> placements, string description)
    {
        var placing = _document!.History.BeginGroup(description);
        var chunkResources = _document.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources))!;
        var elements = placements.Select(placement =>
        {
            var element = chunkResources.AddElement()!;
            element.SetValue(placement.Instance.URI);
            return (placement.Instance, placement.Position, Element: element);
        }).ToList();
        var renderContext = _renderContext;
        if (renderContext == null)
        {
            placing.Dispose();
            return;
        }

        renderContext.QueueRenderAction(() =>
        {
            var placed = elements.Select(element => (element.Position, Objects: element.Instance.GetViewportObjects(new ViewportContext(renderContext, _editingContext!, _renderer!), element.Element))).ToList();
            foreach (var (_, objects) in placed)
            {
                AddViewportObjects(objects);
            }

            Dispatcher.UIThread.Post(() =>
            {
                using (placing)
                {
                    foreach (var (position, objects) in placed)
                    {
                        foreach (var viewportObject in objects.Where(viewportObject => position != null && viewportObject.PositionConverter == null))
                        {
                            viewportObject.Render.SetPosition(position!.Value);
                            viewportObject.Position?.SetValue(ViewportObject.PositionValue(viewportObject.Position, position.Value));
                        }
                    }
                }

                var selectables = placed.Select(entry => entry.Objects.FirstOrDefault(viewportObject => viewportObject.Render.IsSelectable)).OfType<ViewportObject>().ToList();
                if (selectables.Count > 0)
                {
                    SelectObjects(selectables, true);
                }
            });
        });
    }

    private void InitScene()
    {
        // Both PrepareRender and the document becoming ready can queue this
        if (_renderInit)
        {
            return;
        }

        ReportLoading(BuildingStage);
        if (_document != null)
        {
            var viewportContext = new ViewportContext(_renderContext!, _editingContext!, _renderer!)
            {
                Progress = (what, done, total) => ReportLoading(BuildingStage, $"{what} ({done + 1} of {total})", done, total),
            };
            AddViewportObjects(_document.DocumentModel.GetViewportObjects(viewportContext, _document.PropertyGraph.Root));
        }

        FitFloorGrid();
        FitOrbitToModel();
        FinalizeSceneInit();
    }

    // A chunk's camera as it was before its editor got made again (another program changed what it shows)
    private mat4? _keptView;

    /// <summary>
    /// Where a chunk's camera is, a single asset's viewer frames what it shows again
    /// </summary>
    internal mat4? CurrentView => _isChunkViewport && _renderInit ? _scene?.Camera.LocalTransform : null;

    internal void KeepView(mat4 view) => _keptView = view;

    /// <summary>
    /// Whether the scene drew something of the asset (a texture, a material, a mesh), which its document doesn't always reach
    /// </summary>
    internal bool HasRead(LabURI asset) => _renderContext?.ReadAssets.ContainsKey(asset) == true;

    private void FinalizeSceneInit()
    {
        _renderInit = true;

        _renderer!.FireSceneInitialized();
        _renderer.RegisterForRendering(_scene!, true);
        _renderer.RegisterForUpdating(_scene!);

        if (_isChunkViewport && _keptView is { } view)
        {
            _scene!.Camera.LocalTransform = view;
        }
        else if (_isChunkViewport)
        {
            var camForward = -_scene!.Camera.GetForward();
            _scene.Camera.Translate(camForward * -5);
        }
        else
        {
            PlaceOrbitCamera();
        }

        CanRender = true;
        this.RaisePropertyChanged(nameof(CanRender));

        // What got inspected before the scene had its objects (a chunk made again keeps what it inspected) gets selected now
        Dispatcher.UIThread.Post(() =>
        {
            if (_document?.Inspector is { } inspector)
            {
                SelectInspected(inspector.Property);
            }
        });
    }

    // Scene graph changes happen on the render thread
    private void AddViewportObjects(IReadOnlyCollection<ViewportObject> viewportObjects)
    {
        if (viewportObjects.Count == 0)
        {
            return;
        }

        lock (_viewportObjectsLock)
        {
            _viewportObjects = [.. _viewportObjects, .. viewportObjects];
        }

        foreach (var viewportObject in viewportObjects)
        {
            var toggle = LayerToggles.FirstOrDefault(toggle => toggle.Category == viewportObject.Category);
            if (toggle is { IsShown: false })
            {
                viewportObject.Render.IsVisible = false;
            }

            _scene?.AddChild(viewportObject.Render);
        }
    }

    private void RemoveViewportObjects(IReadOnlyCollection<ViewportObject> viewportObjects)
    {
        if (viewportObjects.Count == 0)
        {
            return;
        }

        lock (_viewportObjectsLock)
        {
            _viewportObjects = _viewportObjects.Except(viewportObjects).ToArray();
        }

        foreach (var viewportObject in viewportObjects)
        {
            _scene?.RemoveChild(viewportObject.Render);
            ReleaseBillboards(viewportObject.Render);
        }
    }

    private static void ReleaseBillboards(Renderable renderable)
    {
        if (renderable is Billboard billboard)
        {
            billboard.Release();
        }

        foreach (var child in renderable.Children)
        {
            ReleaseBillboards(child);
        }
    }

    internal bool IsChunkSky(PropertyNode node) => _isChunkViewport && node == _document?.PropertyGraph.Root.Find(nameof(LevelChunk.Skydome));

    private void PropertyGraphOnChanged(PropertyChange change)
    {
        if (!_renderInit)
        {
            return;
        }

        var changed = change.Node;
        List<PropertyNode>? rebuilds = null;
        // Undoing and redoing take instances away and put them back, placing and deleting them here updates the objects right away
        var chunkResources = _document?.PropertyGraph.Root.Find(nameof(LevelChunk.ChunkResources));
        if (_document?.History.IsApplying == true && chunkResources != null)
        {
            if (change.Node == chunkResources && change.Kind != PropertyChangeKind.Value)
            {
                RemoveObjectsOfGoneResources(chunkResources);
                if (change.Kind == PropertyChangeKind.Insert && change.Index < chunkResources.Children.Count)
                {
                    AddRebuild(ref rebuilds, chunkResources.Children[change.Index]);
                }
            }
            else if (change.Node.Parent == chunkResources && _viewportObjects.All(viewportObject => viewportObject.Property != change.Node))
            {
                AddRebuild(ref rebuilds, change.Node);
            }
        }

        // A sky picked for a chunk that had none has no objects yet to follow its link
        if (change.Kind == PropertyChangeKind.Value && IsChunkSky(change.Node) && _viewportObjects.All(viewportObject => viewportObject.Property != change.Node))
        {
            AddRebuild(ref rebuilds, change.Node);
        }

        List<PropertyNode>? structureRebuilds = null;
        foreach (var viewportObject in _viewportObjects)
        {
            // Links to other resources have their whole data below them, pointing one to another resource replaces it
            if (viewportObject.Property == change.Node)
            {
                AddRebuild(ref rebuilds, viewportObject.Property);
                continue;
            }

            // Elements put into or taken out of the resource's lists (emitters, links, points) have objects of their own. What the viewport
            // puts in itself it shows and selects on its own
            if (change.Kind != PropertyChangeKind.Value && !_isPlacingElement && IsWithin(changed, viewportObject.Property))
            {
                AddRebuild(ref structureRebuilds, viewportObject.Property);
                continue;
            }

            if (IsWithin(changed, viewportObject.Position) || IsWithin(changed, viewportObject.Rotation) ||
                IsWithin(changed, viewportObject.Scale) || IsWithin(changed, viewportObject.Transform))
            {
                ApplyTransformFromData(viewportObject);
            }

            if (viewportObject.Refresh != null && viewportObject.RenderDependencies.Any(dependency => IsWithin(changed, dependency)) &&
                !viewportObject.Refresh())
            {
                AddRebuild(ref rebuilds, viewportObject.Property);
            }
        }

        // Whether the selection can be transformed depends on its data too, like the load wall of a link that doesn't use it
        if (SelectedObject != null)
        {
            this.RaisePropertyChanged(nameof(CanTranslate));
            this.RaisePropertyChanged(nameof(CanRotate));
            this.RaisePropertyChanged(nameof(CanScale));
            ApplyTool();
        }

        foreach (var property in structureRebuilds ?? [])
        {
            // The selected element can be gone or somewhere else in its list
            if (SelectedObjects.Any(viewportObject => viewportObject.Property == property))
            {
                _editingContext?.Deselect();
                SelectedObject = null;
                RaiseSelectionChanged();
            }

            QueueStructureRebuild(property);
        }

        if (rebuilds == null)
        {
            return;
        }

        foreach (var property in rebuilds.Where(property => structureRebuilds?.Contains(property) != true))
        {
            RebuildViewportObjects(property);
        }
    }

    // Resources whose lists changed, made again once the changes in a row are done: deleting or undoing many placed meshes made the
    // scenery's hundreds of objects again for every one of them
    private readonly HashSet<PropertyNode> _pendingStructureRebuilds = [];

    private void QueueStructureRebuild(PropertyNode property)
    {
        if (!_pendingStructureRebuilds.Add(property) || _pendingStructureRebuilds.Count > 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var pending = _pendingStructureRebuilds.ToList();
            _pendingStructureRebuilds.Clear();
            foreach (var resource in pending)
            {
                RebuildViewportObjects(resource, null, false);
            }
        }, DispatcherPriority.Background);
    }

    private void RemoveObjectsOfGoneResources(PropertyNode chunkResources)
    {
        if (_renderContext == null)
        {
            return;
        }

        if (SelectedObjects.Any(viewportObject => viewportObject.Property.Parent == chunkResources && !chunkResources.Children.Contains(viewportObject.Property)))
        {
            _editingContext?.Deselect();
            SelectedObject = null;
            RaiseSelectionChanged();
        }

        // Worked out when it runs, objects of the resource still waiting to be made are gone with it too
        _renderContext.QueueRenderAction(() => RemoveViewportObjects(_viewportObjects
            .Where(viewportObject => viewportObject.Property.Parent == chunkResources && !chunkResources.Children.Contains(viewportObject.Property)).ToList()));
    }

    private static void AddRebuild(ref List<PropertyNode>? rebuilds, PropertyNode property)
    {
        rebuilds ??= [];
        if (!rebuilds.Contains(property))
        {
            rebuilds.Add(property);
        }
    }

    // Whether the changed node is the node or something under it, by going up from it: paths would have to be worked out again after
    // every change of the graph's structure
    private static bool IsWithin(PropertyNode changed, PropertyNode? node)
    {
        if (node == null)
        {
            return false;
        }

        for (var current = changed; current != null; current = current.Parent)
        {
            if (current == node)
            {
                return true;
            }
        }

        return false;
    }

    private static void ApplyTransformFromData(ViewportObject viewportObject)
    {
        var render = viewportObject.Render;
        if (viewportObject.Transform?.GetValue() is Matrix4 matrix)
        {
            render.SetLocalTransform(viewportObject.GetTransformFromData(matrix));
            return;
        }

        if (viewportObject.PositionConverter != null && viewportObject.Position != null)
        {
            render.SetPosition(viewportObject.PositionConverter.ToPosition(viewportObject.Position.GetValue()));
        }
        else if (viewportObject.Position?.GetValue() is Vector3 position)
        {
            render.SetPosition(position.ToGlm());
        }
        else if (viewportObject.Position?.GetValue() is Vector4 point)
        {
            render.SetPosition(new vec3(point.X, point.Y, point.Z));
        }

        if (viewportObject.RotationConverter != null && viewportObject.Rotation != null)
        {
            render.SetRotation(viewportObject.RotationConverter.ToRotation(viewportObject.Rotation.GetValue()));
        }
        else if (viewportObject.Rotation?.GetValue() is Vector3 rotation)
        {
            render.SetRotation(new quat(rotation.ToRadiansGlm()));
        }

        if (viewportObject.Scale?.GetValue() is Vector3 scale)
        {
            render.SetScale(scale.ToGlm());
        }
    }

    /// <summary>
    /// Creates the objects of a linked resource from scratch, reselecting what was selected of it or selecting the named one
    /// </summary>
    private void RebuildViewportObjects(PropertyNode property, string? selectName = null)
    {
        var selectedName = selectName ?? (SelectedObject?.Property == property ? SelectedObject.DocumentName : null);
        RebuildViewportObjects(property, selectedName == null ? null : viewportObject => viewportObject.DocumentName == selectedName, selectName != null);
    }

    // A rebuild reading the graph while the UI thread changes it, like undoing many steps in a row, is tried again this many times
    private const int RebuildAttempts = 3;

    // A chunk's resources are links to them, the viewer of a single asset shows the document's own at the root: read as a link, a shader
    // put into or taken out of a material made its preview again from nothing, and it stayed empty
    internal LabURI? ResourceOf(PropertyNode property)
    {
        return property.GetValue() switch
        {
            LabURI link => link,
            IAsset asset when ReferenceEquals(asset, _document?.DocumentModel) => asset.URI,
            _ => null,
        };
    }

    // Makes the resource's objects again and selects the one picked among them (a duplicate, what a prefab placed), shown in the
    // inspector when asked. Placed gets the new objects on the UI thread before the selection, or none when the scene is gone
    private void RebuildViewportObjects(PropertyNode property, Func<ViewportObject, bool>? select, bool openInspector, Action<List<ViewportObject>>? placed = null, int attempt = 0)
    {
        var renderContext = _renderContext;
        if (renderContext == null || _editingContext == null || _renderer == null)
        {
            placed?.Invoke([]);
            return;
        }

        if (select != null)
        {
            _editingContext.Deselect();
            SelectedObject = null;
        }

        // The resource it is now: by the time the render thread gets to it an undo can have taken the node out, which then read another
        // resource at its index and made that one's objects again next to its own
        var uri = property.Parent?.Children.Contains(property) != false ? ResourceOf(property) : null;
        renderContext.QueueRenderAction(() =>
        {
            var viewportObjects = new List<ViewportObject>();
            if (_renderInit && _renderContext == renderContext)
            {
                RemoveViewportObjects(_viewportObjects.Where(viewportObject => viewportObject.Property == property).ToList());
                // Taken out since, its objects would be read from whatever came to its place
                if (uri != null && uri != LabURI.Empty && AssetManager.Get().DoesAssetExist(uri) && property.Parent?.Children.Contains(property) != false)
                {
                    try
                    {
                        viewportObjects = AssetManager.Get().GetAssetData(uri).GetViewportObjects(new ViewportContext(renderContext, _editingContext!, _renderer!), property);
                    }
                    catch (Exception ex) when (attempt < RebuildAttempts && ex is InvalidOperationException or NullReferenceException or ArgumentOutOfRangeException)
                    {
                        // The graph changed under it, the resource's objects get made once the UI thread is done with it
                        Dispatcher.UIThread.Post(() => RebuildViewportObjects(property, select, openInspector, placed, attempt + 1), DispatcherPriority.Background);
                        return;
                    }

                    AddViewportObjects(viewportObjects);
                }
            }

            if (select == null && placed == null)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                placed?.Invoke(viewportObjects);
                var reselected = select == null ? null : viewportObjects.FirstOrDefault(select);
                if (reselected != null && SelectedObject == null)
                {
                    // What got made, like a duplicate, is shown in the inspector as well
                    SelectObject(reselected, openInspector);
                }
            });
        });
    }

    private void SetCategoryShown(ViewportObjectCategory category, bool isShown)
    {
        foreach (var viewportObject in _viewportObjects.Where(viewportObject => viewportObject.Category == category))
        {
            viewportObject.Render.IsVisible = isShown;
            if (isShown)
            {
                // Showing an object shows everything of it, including what its data hides like the scenery of a link that isn't visible
                viewportObject.Refresh?.Invoke();
            }

            if (!isShown && viewportObject == SelectedObject)
            {
                _editingContext?.Deselect();
                SelectedObject = null;
            }
        }
    }

    private void SetTool(TransformMode tool)
    {
        if (_editingContext is { IsDraggingGizmo: true } || !IsToolUsable(tool))
        {
            return;
        }

        _chosenTool = tool;
        ApplyTool();
    }

    private void ApplyTool()
    {
        var tool = IsToolUsable(_chosenTool) ? _chosenTool : FallbackTools.FirstOrDefault(IsToolUsable, TransformMode.SELECTION);
        ActiveTool = tool;
        _editingContext?.SetTransformMode(tool);
    }

    // Without a selection any tool can be picked for what gets selected next
    private bool IsToolUsable(TransformMode tool)
    {
        return tool == TransformMode.SELECTION || SelectedObject == null || SelectedObject.IsTransformSupported(tool);
    }

    private void ApplySnapping()
    {
        if (_editingContext == null)
        {
            return;
        }

        _editingContext.SetSnapping(IsSnapping, new GizmoSnapping(TranslationSnap.Value, RotationSnap.Value, ScaleSnap.Value));
        _editingContext.IsGridShown = IsGridShown;
    }

    private void LoadSnappingPreferences()
    {
        IsSnapping = Preferences.GetPreference<bool>(Preferences.ViewportSnapping);
        IsGridShown = Preferences.GetPreference<bool>(Preferences.ViewportGridShown);
        TranslationSnap.Value = Preferences.GetPreference<float>(Preferences.ViewportTranslationSnap);
        RotationSnap.Value = Preferences.GetPreference<float>(Preferences.ViewportRotationSnap);
        ScaleSnap.Value = Preferences.GetPreference<float>(Preferences.ViewportScaleSnap);
    }

    private void SaveSnappingPreferences()
    {
        Preferences.SetPreference(Preferences.ViewportSnapping, IsSnapping);
        Preferences.SetPreference(Preferences.ViewportGridShown, IsGridShown);
        Preferences.SetPreference(Preferences.ViewportTranslationSnap, (double)TranslationSnap.Value);
        Preferences.SetPreference(Preferences.ViewportRotationSnap, (double)RotationSnap.Value);
        Preferences.SetPreference(Preferences.ViewportScaleSnap, (double)ScaleSnap.Value);
    }

    private void OnPreferenceChanged(object? sender, Preferences.PreferenceChangedArgs e)
    {
        if (e.PreferenceName.StartsWith("Viewport", StringComparison.Ordinal))
        {
            LoadSnappingPreferences();
        }
    }

    // Viewers of a single model get a floor sized to fit it, chunks have their own
    private void FitFloorGrid()
    {
        var extent = 0.0f;
        foreach (var viewportObject in _viewportObjects)
        {
            var bounds = viewportObject.Render.GetBoundsTransform();
            for (var corner = 0; corner < 8; corner++)
            {
                var point = (bounds * new vec4((corner & 1) == 0 ? -1.0f : 1.0f, (corner & 2) == 0 ? -1.0f : 1.0f, (corner & 4) == 0 ? -1.0f : 1.0f, 1.0f)).xyz;
                extent = MathF.Max(extent, MathF.Max(MathF.Abs(point.x), MathF.Max(MathF.Abs(point.y), MathF.Abs(point.z))));
            }
        }

        _floorGrid.FitTo(extent);
    }

    private void FitOrbitToModel()
    {
        _modelMin = new vec3(float.MaxValue);
        _modelMax = new vec3(float.MinValue);
        foreach (var viewportObject in _viewportObjects)
        {
            var bounds = viewportObject.Render.GetBoundsTransform();
            for (var corner = 0; corner < 8; corner++)
            {
                var point = (bounds * new vec4((corner & 1) == 0 ? -1.0f : 1.0f, (corner & 2) == 0 ? -1.0f : 1.0f, (corner & 4) == 0 ? -1.0f : 1.0f, 1.0f)).xyz;
                _modelMin = vec3.Min(_modelMin, point);
                _modelMax = vec3.Max(_modelMax, point);
            }
        }
    }

    private void PlaceOrbitCamera()
    {
        var camera = _scene!.Camera;
        camera.LocalTransform = _orbit.Frame(_modelMin, _modelMax, camera.GetFrameCamera().FovY);
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel wheel)
    {
        if (_isChunkViewport || _scene == null || !_renderInit || wheel.Y == 0)
        {
            return;
        }

        _scene.Camera.LocalTransform = _orbit.Zoom(_scene.Camera.LocalTransform, _orbit.Distance * MathF.Pow(0.9f, wheel.Y));
    }

    private void DrawFloorGrid(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (!_isChunkViewport && _renderInit)
        {
            _floorGrid.Draw(renderer, camera);
        }
    }

    private void ToggleTransformSpace()
    {
        if (_editingContext is { IsDraggingGizmo: true })
        {
            return;
        }

        TransformSpace = TransformSpace == TransformLocality.LOCAL ? TransformLocality.WORLD : TransformLocality.LOCAL;
        _editingContext?.SetTransformLocality(TransformSpace);
    }

    /// <summary>
    /// Moves the camera back from the selection until all of it is in view
    /// </summary>
    private void FrameSelection()
    {
        if (SelectedObject == null || _scene == null)
        {
            return;
        }

        var min = new vec3(float.MaxValue);
        var max = new vec3(float.MinValue);
        foreach (var bounds in SelectedObjects.Select(viewportObject => viewportObject.Render.GetBoundsTransform()))
        {
            var extent = vec3.Abs(bounds.Column0.xyz) + vec3.Abs(bounds.Column1.xyz) + vec3.Abs(bounds.Column2.xyz);
            min = vec3.Min(min, bounds.Column3.xyz - extent);
            max = vec3.Max(max, bounds.Column3.xyz + extent);
        }

        var center = (min + max) * 0.5f;
        var radius = Math.Max(((max - min) * 0.5f).Length, 0.5f);
        var camera = _scene.Camera.GetFrameCamera();
        var distance = radius / MathF.Tan(camera.FovY * 0.5f) * 1.2f;
        _scene.Camera.SetPosition(center - camera.Forward * distance);
    }

    private void RendererOnSceneInitialized()
    {
        if (!_firstRender)
        {
            return;
        }

        _firstRender = false;
    }

    private void RendererOnFinishRender()
    {
        _scene?.UpdateRenderTransform();
        Dispatcher.UIThread.Post(() =>
        {
            _renderer?.DoUpdate();
        });
    }

    private void RendererOnUpdate(double delta)
    {
        if (_scene == null || _keyboard == null)
        {
            return;
        }

        var selectionInfo = !_isChunkViewport ? string.Empty
            : _editingContext?.SelectionCount > 1 ? $"{_editingContext.SelectionCount} selected, {_editingContext.SelectedRenderable?.Describe()}"
            : _editingContext?.SelectedRenderable is { IsSelected: true } selected ? selected.Describe() : string.Empty;
        if (selectionInfo != SelectionInfo)
        {
            SelectionInfo = selectionInfo;
        }

        // Shortcuts like duplicating with Ctrl+D share their keys with moving
        if (IsControlPressed())
        {
            return;
        }

        var camForward = -_scene.Camera.GetForward();
        var camLeft = -_scene.Camera.GetLeft();
        var fast = _keyboard.IsKeyPressed(Key.ShiftLeft) || _keyboard.IsKeyPressed(Key.ShiftRight);
        if (!_isChunkViewport)
        {
            MoveOrbitCamera(camLeft, (float)delta * (fast ? 3.0f : 1.0f));
            return;
        }

        var camSpeed = 10.0f;
        if (fast)
        {
            camSpeed *= 5.0f;
        }
        if (_keyboard.IsKeyPressed(Key.W))
        {
            _scene.Camera.Translate(camForward * camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.S))
        {
            _scene.Camera.Translate(camForward * -camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.A))
        {
            _scene.Camera.Translate(camLeft * -camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.D))
        {
            _scene.Camera.Translate(camLeft * camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.Space))
        {
            _scene.Camera.Translate(vec3.UnitY * camSpeed * (float)delta);
        }
        if (_keyboard.IsKeyPressed(Key.C))
        {
            _scene.Camera.Translate(vec3.UnitY * -camSpeed * (float)delta);
        }
    }

    // W and S move closer and further away, A and D move the camera and what it turns around sideways, Space and C up and down
    private void MoveOrbitCamera(vec3 camLeft, float delta)
    {
        if (!_renderInit)
        {
            return;
        }

        var step = _orbit.Distance * delta;
        var zoom = (_keyboard!.IsKeyPressed(Key.S) ? step : 0) - (_keyboard.IsKeyPressed(Key.W) ? step : 0);
        var pan = (_keyboard.IsKeyPressed(Key.D) ? step : 0) - (_keyboard.IsKeyPressed(Key.A) ? step : 0);
        var rise = (_keyboard.IsKeyPressed(Key.Space) ? step : 0) - (_keyboard.IsKeyPressed(Key.C) ? step : 0);
        if (zoom == 0 && pan == 0 && rise == 0)
        {
            return;
        }

        var camera = _scene!.Camera;
        camera.LocalTransform = _orbit.Zoom(_orbit.Pan(camera.LocalTransform, camLeft * pan + vec3.UnitY * rise), _orbit.Distance + zoom);
    }

    private void KeyboardOnKeyDown(IKeyboard keyboard, Key key, int scanCode)
    {
        if (_scene == null || _editingContext == null || !_isChunkViewport)
        {
            return;
        }

        if (IsControlPressed())
        {
            if (key == Key.D)
            {
                if (IsEditingTriangles)
                {
                    DuplicateSelectedTriangles();
                }
                else if (IsEditingMeshes)
                {
                    DuplicatePlacements();
                }
                else
                {
                    DuplicateSelection();
                }
            }
            else if (key == Key.A)
            {
                SelectAll();
            }

            return;
        }

        if (key == Key.Escape && IsRubberBanding)
        {
            IsRubberBanding = false;
            _leftPress = null;
            return;
        }

        // Points of a triangle being drawn go first, then the drawing
        if (key == Key.Escape && IsDrawingTriangle)
        {
            if (_trianglePoints.Length > 0)
            {
                _trianglePoints = [];
            }
            else
            {
                SetTriangleDrawing(false);
            }

            return;
        }

        switch (key)
        {
            case Key.Q:
                SetTool(TransformMode.SELECTION);
                break;
            case Key.T:
                SetTool(TransformMode.TRANSLATE);
                break;
            case Key.R:
                SetTool(TransformMode.ROTATE);
                break;
            case Key.E:
                SetTool(TransformMode.SCALE);
                break;
            case Key.L:
                ToggleTransformSpace();
                break;
            case Key.F:
                FrameSelection();
                break;
            case Key.Escape when _editingContext.IsDraggingGizmo:
                _editingContext.CancelGizmoDrag();
                EndDragStep();
                break;
            case Key.U:
            case Key.Escape:
                _document?.OpenInspector(null);
                _editingContext.Deselect();
                SelectedObject = null;
                RaiseSelectionChanged();
                break;
            case Key.Delete when IsEditingTriangles:
                DeleteSelectedTriangles();
                break;
            case Key.Delete:
                DeleteInstance();
                break;
        }
    }

    private bool IsControlPressed()
    {
        return _keyboard != null && (_keyboard.IsKeyPressed(Key.ControlLeft) || _keyboard.IsKeyPressed(Key.ControlRight));
    }

    private Vector2 _prevMousePosition = new(-1, -1);
    private void OnMouseMove(IMouse mouse, Vector2 mousePos)
    {
        // A drag keeps going when the mouse leaves the viewport
        if (_editingContext is { IsDraggingGizmo: true } && _scene != null)
        {
            // Holding Ctrl snaps when snapping is off and the other way around
            var invertSnapping = IsControlPressed();
            _editingContext.UpdateGizmoDrag(_scene.Camera.GetFrameCamera(), new vec2(mousePos.X, mousePos.Y), invertSnapping);
            _prevMousePosition = mousePos;
            return;
        }

        if (_leftPress != null && mouse.IsButtonPressed(MouseButton.Left))
        {
            UpdateRubberBand(new vec2(mousePos.X, mousePos.Y));
            _prevMousePosition = mousePos;
            return;
        }

        var viewRect = new Rect(0, 0, ViewportSize.x, ViewportSize.y);
        if (!viewRect.Contains(new Point(mousePos.X, mousePos.Y)))
        {
            return;
        }

        if (_prevMousePosition is { X: -1, Y: -1 })
        {
            _prevMousePosition = mousePos;
        }

        if (mouse.IsButtonPressed(MouseButton.Right))
        {
            var delta = (_prevMousePosition - mousePos).FromSystem() * 0.2f;
            if (!_isChunkViewport)
            {
                if (_scene != null && _renderInit)
                {
                    _scene.Camera.LocalTransform = _orbit.Orbit(_scene.Camera.LocalTransform, glm.Radians(-delta.x), 0.05f * delta.y);
                }

                _prevMousePosition = mousePos;
                return;
            }

            var camera = _scene!.Camera;
            camera.LocalTransform = FlyCamera.Look(camera.LocalTransform, glm.Radians(-delta.x), 0.05f * delta.y);
        }
        else if (!mouse.IsButtonPressed(MouseButton.Left) && _scene != null && _isChunkViewport)
        {
            _editingContext?.UpdateGizmoHover(_scene.Camera.GetFrameCamera(), new vec2(mousePos.X, mousePos.Y));
        }

        _prevMousePosition = mousePos;
    }

    public Action<Renderer, Scene>? SceneInitializer { get; set; }
    public bool CanRender { get; private set; }

    public bool IsBuildingScene => _renderContext != null && !_renderInit;
}

public record ViewportContext(RenderContext RenderContext, EditingContext EditingContext, Renderer Renderer)
{
    /// <summary>
    /// Told what's being built while the scene gets made: what, how many are done and how many there are
    /// </summary>
    public Action<string, int, int>? Progress { get; init; }
}
