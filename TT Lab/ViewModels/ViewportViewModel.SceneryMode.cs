using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables.Fluent;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using GlmSharp;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Collision;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Assets.Instance;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Rendering.Objects.Gizmo;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using NVector3 = System.Numerics.Vector3;
using Scenery = TT_Lab.Assets.Instance.Scenery;
using Vector2 = System.Numerics.Vector2;

namespace TT_Lab.ViewModels;

/// <summary>
/// A collision surface to pick in the scenery mode's toolbar, with its editor color
/// </summary>
public sealed record CollisionSurfaceChoice(LabURI Uri, string Name, IBrush Swatch)
{
    public override string ToString() => Name;
}

// The scenery mode: the scenery's placed meshes picked by their triangles and edited like instances, its collision's triangles selected,
// moved, deleted, duplicated and put on other surfaces, placeholders and collision made at the cursor, collision made of meshes
public partial class ViewportViewModel
{
    [Reactive(SetModifier = AccessModifier.Private)]
    private ViewportEditMode _editMode = ViewportEditMode.Instances;

    // What the scenery mode edits: the collision's triangles instead of the placed meshes
    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isEditingCollision;

    // The surface new collision gets, the selected triangles' when they share one (none when they don't)
    [Reactive]
    private CollisionSurfaceChoice? _currentSurface;

    [Reactive(SetModifier = AccessModifier.Private)]
    private IReadOnlyList<CollisionSurfaceChoice> _surfaces = [];

    // Clicks make the points of a new triangle
    [Reactive(SetModifier = AccessModifier.Private)]
    private bool _isDrawingTriangle;

    // Replaced whole: the render thread draws the selection while the UI thread changes it
    private int[] _selectedTriangles = [];
    private ViewportObject? _trianglePivot;
    private volatile CollisionGeometry? _shownGeometry;
    private volatile TriangleDragPreview? _triangleDragPreview;
    private vec3[] _trianglePoints = [];
    // Set while the scenery mode changes the collision itself, its own changes keep the selection it makes
    private bool _isEditingCollisionShape;
    // Set while the surface picker shows the selection's surface, which isn't a change of it
    private bool _isShowingSurface;
    private CollisionSurfaceChoice? _lastPickedSurface;

    private const float CornerSnapPixels = 10.0f;
    private const float FlatTriangleSize = 1.0f;
    private static readonly vec4 TriangleSelectionColor = new(1.0f, 0.85f, 0.2f, 1.0f);
    private static readonly vec4 TrianglePointColor = new(0.3f, 0.9f, 1.0f, 1.0f);

    private sealed record TriangleDragPreview(CollisionGeometry Start, int[] Triangles, System.Numerics.Matrix4x4 Transform);

    public ReactiveCommand<ViewportEditMode, Unit> SetEditModeCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> EditMeshesCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> EditCollisionCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GenerateCollisionCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ToggleTriangleDrawingCommand { get; private set; } = null!;

    public bool IsInstanceMode => EditMode == ViewportEditMode.Instances;
    public bool IsSceneryMode => EditMode == ViewportEditMode.Scenery;
    public bool IsEditingMeshes => IsSceneryMode && !IsEditingCollision;
    public bool IsEditingTriangles => IsSceneryMode && IsEditingCollision;

    /// <summary>
    /// How many collision triangles are selected
    /// </summary>
    public int SelectedTriangleCount => _selectedTriangles.Length;

    private void InitSceneryMode()
    {
        SetEditModeCommand = ReactiveCommand.Create<ViewportEditMode>(SetEditMode);
        EditMeshesCommand = ReactiveCommand.Create(() => SetSceneryTarget(false));
        EditCollisionCommand = ReactiveCommand.Create(() => SetSceneryTarget(true));
        GenerateCollisionCommand = ReactiveCommand.Create(GenerateCollisionFromSelection);
        ToggleTriangleDrawingCommand = ReactiveCommand.Create(() => SetTriangleDrawing(!IsDrawingTriangle));
        this.WhenAnyValue(x => x.EditMode, x => x.IsEditingCollision).Subscribe(_ =>
        {
            this.RaisePropertyChanged(nameof(IsInstanceMode));
            this.RaisePropertyChanged(nameof(IsSceneryMode));
            this.RaisePropertyChanged(nameof(IsEditingMeshes));
            this.RaisePropertyChanged(nameof(IsEditingTriangles));
        }).DisposeWith(_closeDisposables);
        // The triangles are selected while their gizmo's object is
        this.WhenAnyValue(x => x.SelectedObject).Subscribe(selected =>
        {
            if (selected == null || selected != _trianglePivot)
            {
                ClearTriangleSelection();
            }
        }).DisposeWith(_closeDisposables);
        this.WhenAnyValue(x => x.CurrentSurface).Skip(1).Subscribe(OnSurfacePicked).DisposeWith(_closeDisposables);
    }

    private void SetEditMode(ViewportEditMode mode)
    {
        if (EditMode == mode || _editingContext is { IsDraggingGizmo: true })
        {
            return;
        }

        SetTriangleDrawing(false);
        DeselectEverything();
        EditMode = mode;
        if (mode == ViewportEditMode.Scenery)
        {
            RefreshSurfaces();
            if (IsEditingCollision)
            {
                ShowCollisionLayer();
            }
        }
    }

    private void SetSceneryTarget(bool collision)
    {
        if (IsEditingCollision == collision || _editingContext is { IsDraggingGizmo: true })
        {
            return;
        }

        SetTriangleDrawing(false);
        DeselectEverything();
        IsEditingCollision = collision;
        if (collision)
        {
            ShowCollisionLayer();
        }
    }

    private void ShowCollisionLayer()
    {
        if (LayerToggles.FirstOrDefault(toggle => toggle.Category == ViewportObjectCategory.Collision) is { IsShown: false } collision)
        {
            collision.IsShown = true;
        }
    }

    private void DeselectEverything()
    {
        _editingContext?.Deselect();
        SelectedObject = null;
        ClearTriangleSelection();
        RaiseSelectionChanged();
    }

    // Objects only get picked in their own mode, placed meshes only while the scenery mode edits them
    private bool IsPickable(ViewportObject viewportObject)
    {
        return viewportObject.Render.IsVisible && viewportObject.Render.IsSelectable && viewportObject.Mode == EditMode && !IsEditingTriangles;
    }

    // The scenery's resource node in the chunk's document and its data
    private (PropertyNode Node, Scenery Asset, SceneryData Data)? FindScenery()
    {
        if (FindResourceData(typeof(Scenery)) is not { } node || node.GetValue() is not Scenery scenery)
        {
            return null;
        }

        return (node, scenery, ((IAsset)scenery).GetData<SceneryData>());
    }

    private PropertyNode? CollisionShapeNode() => FindResourceData(typeof(Scenery))?.Find($"AssetData.{nameof(SceneryData.CollisionShape)}");

    private CollisionGeometry? CurrentCollisionGeometry() => CollisionShapeNode()?.GetValue() as CollisionGeometry;

    #region Placed meshes

    // The placed mesh whose triangles the ray hits first, boxes that start further away than a hit already found aren't looked into
    private ViewportObject? PickPlacement(Ray ray)
    {
        var candidates = new List<(ViewportObject Object, float Distance)>();
        foreach (var viewportObject in _viewportObjects)
        {
            if (!IsPickable(viewportObject) || viewportObject.DuplicatedElement?.GetValue() is not SceneryPlacement)
            {
                continue;
            }

            if (GizmoMath.IntersectBox(ray, viewportObject.Render.GetBoundsTransform()) is { } distance)
            {
                candidates.Add((viewportObject, distance));
            }
        }

        ViewportObject? picked = null;
        var best = float.MaxValue;
        foreach (var (viewportObject, boxDistance) in candidates.OrderBy(candidate => candidate.Distance))
        {
            if (boxDistance > best)
            {
                break;
            }

            var placement = (SceneryPlacement)viewportObject.DuplicatedElement!.GetValue()!;
            var origin = ToSystem(ray.Origin);
            var direction = ToSystem(ray.Direction);
            foreach (var (a, b, c) in PlacementTriangles(placement))
            {
                if (CollisionEdits.RayTriangle(origin, direction, a, b, c) is { } hit && hit < best)
                {
                    best = hit;
                    picked = viewportObject;
                }
            }
        }

        return picked;
    }

    // The triangles of a placed mesh where it's placed, a LOD's as it's drawn up close, counter-clockwise seen from where its vertexes'
    // normals point
    internal static IEnumerable<(NVector3 A, NVector3 B, NVector3 C)> PlacementTriangles(SceneryPlacement placement)
    {
        var assetManager = AssetManager.Get();
        var model = placement.Model;
        if (placement.IsLod)
        {
            if (!assetManager.DoesAssetExist(model) || assetManager.GetAssetData<LodModelData>(model).Meshes is not { Count: > 0 } meshes)
            {
                yield break;
            }

            model = meshes[0];
        }

        if (!assetManager.DoesAssetExist(model) || assetManager.GetAssetData<MeshData>(model).Model is not { } modelUri || !assetManager.DoesAssetExist(modelUri))
        {
            yield break;
        }

        var matrix = placement.Matrix.ToSystem();
        var modelData = assetManager.GetAssetData<ModelData>(modelUri);
        for (var part = 0; part < modelData.Vertexes.Count && part < modelData.Faces.Count; part++)
        {
            var vertexes = modelData.Vertexes[part];
            foreach (var face in modelData.Faces[part])
            {
                if (face.Indexes is not { Length: 3 } corners || corners.Any(corner => corner < 0 || corner >= vertexes.Count))
                {
                    continue;
                }

                var a = vertexes[corners[0]];
                var b = vertexes[corners[1]];
                var c = vertexes[corners[2]];
                var pa = NVector3.Transform(new NVector3(a.Position.X, a.Position.Y, a.Position.Z), matrix);
                var pb = NVector3.Transform(new NVector3(b.Position.X, b.Position.Y, b.Position.Z), matrix);
                var pc = NVector3.Transform(new NVector3(c.Position.X, c.Position.Y, c.Position.Z), matrix);
                // The game's strips don't keep their triangles facing one way, the vertexes' normals tell which way is out
                var normal = new NVector3(a.Normal.X + b.Normal.X + c.Normal.X, a.Normal.Y + b.Normal.Y + c.Normal.Y, a.Normal.Z + b.Normal.Z + c.Normal.Z);
                var worldNormal = NVector3.TransformNormal(normal, matrix);
                if (a.HasNormals && worldNormal.LengthSquared() > 1e-12f && NVector3.Dot(NVector3.Cross(pb - pa, pc - pa), worldNormal) < 0)
                {
                    yield return (pa, pc, pb);
                    continue;
                }

                yield return (pa, pb, pc);
            }
        }
    }

    // Where the ray through the viewport point hits the collision or a placed mesh, whichever is closer
    private bool TryHitScenery(float x, float y, out vec3 hit)
    {
        hit = default;
        if (_scene == null)
        {
            return false;
        }

        var ray = _scene.Camera.GetFrameCamera().ScreenRay(new vec2(x, y));
        var best = TryHitCollision(x, y, out var collisionHit) ? (collisionHit - ray.Origin).Length : float.MaxValue;
        var origin = ToSystem(ray.Origin);
        var direction = ToSystem(ray.Direction);
        foreach (var viewportObject in _viewportObjects)
        {
            if (!viewportObject.Render.IsVisible || viewportObject.DuplicatedElement?.GetValue() is not SceneryPlacement placement
                || GizmoMath.IntersectBox(ray, viewportObject.Render.GetBoundsTransform()) is not { } boxDistance || boxDistance > best)
            {
                continue;
            }

            foreach (var (a, b, c) in PlacementTriangles(placement))
            {
                if (CollisionEdits.RayTriangle(origin, direction, a, b, c) is { } distance && distance < best)
                {
                    best = distance;
                }
            }
        }

        if (best == float.MaxValue)
        {
            return false;
        }

        hit = ray.Origin + ray.Direction * best;
        return true;
    }

    // The placed meshes selected, in the order they got selected
    private List<(ViewportObject Object, SceneryPlacement Placement)> SelectedPlacements()
    {
        return SelectedObjects.Where(viewportObject => viewportObject.Mode == ViewportEditMode.Scenery)
            .Select(viewportObject => (viewportObject, viewportObject.DuplicatedElement?.GetValue() as SceneryPlacement))
            .Where(entry => entry.Item2 != null).Select(entry => (entry.viewportObject, entry.Item2!)).ToList();
    }

    private void SelectAllPlacements()
    {
        SelectObjects(_viewportObjects.Where(viewportObject => IsPickable(viewportObject) && viewportObject.DuplicatedElement?.GetValue() is SceneryPlacement).ToList(), false);
    }

    /// <summary>
    /// A placeholder of the shape standing at the cursor, on the checker material, selected, one step to undo
    /// </summary>
    internal PropertyNode? AddPlaceholder(PlaceholderShape shape)
    {
        if (FindScenery() is not { } scenery)
        {
            Log.WriteLine("The chunk has no scenery to put a placeholder into", Log.LogType.Warning);
            return null;
        }

        var placement = scenery.Data.CreatePlaceholder(shape, ToSystem(NewResourcePosition()));
        return CreateElement(typeof(Scenery), $"AssetData.{nameof(SceneryData.Placements)}", placement, $"Added a {shape.ToString().ToLowerInvariant()} placeholder");
    }

    /// <summary>
    /// Copies of the selected meshes around the cursor the way they stand to each other, sharing their meshes, selected, one step to undo
    /// </summary>
    internal void PlaceCopiesOfSelection()
    {
        var selected = SelectedPlacements();
        if (selected.Count == 0)
        {
            return;
        }

        var anchor = selected[0].Placement.Matrix.ToSystem().Translation;
        var cursor = ToSystem(NewResourcePosition());
        var copies = selected.Select(entry =>
        {
            var copy = (SceneryPlacement)Util.CloneUtils.DeepClone(entry.Placement, typeof(SceneryPlacement));
            var matrix = entry.Placement.Matrix.ToSystem();
            matrix.Translation = matrix.Translation - anchor + cursor;
            copy.Matrix = matrix.ToTwin();
            return copy;
        }).ToList();
        InsertPlacements(copies, copies.Count > 1 ? $"Placed copies of {copies.Count} meshes" : "Placed a copy of a mesh");
    }

    // Puts the placements at the end of the scenery's list and selects them once their objects are there, one step to undo
    private void InsertPlacements(IReadOnlyList<SceneryPlacement> placements, string description)
    {
        if (FindScenery() is not { } scenery || _document == null || placements.Count == 0)
        {
            return;
        }

        var list = scenery.Node.Find($"AssetData.{nameof(SceneryData.Placements)}");
        if (list == null)
        {
            return;
        }

        var placing = _document.History.BeginGroup(description);
        var paths = new List<string>();
        _isPlacingElement = true;
        try
        {
            var count = (list.GetValue() as System.Collections.IList)?.Count ?? list.Children.Count;
            foreach (var placement in placements)
            {
                if (list.InsertElement(count++, placement) is { } element)
                {
                    paths.Add(element.Path);
                }
            }
        }
        finally
        {
            _isPlacingElement = false;
        }

        var resource = _document.PropertyGraph.Find(scenery.Node.Path[..scenery.Node.Path.IndexOf("[data]", StringComparison.Ordinal)]);
        if (resource == null)
        {
            placing.Dispose();
            return;
        }

        _editingContext?.Deselect();
        SelectedObject = null;
        RebuildViewportObjects(resource, null, false, viewportObjects =>
        {
            placing.Dispose();
            var placed = viewportObjects.Where(viewportObject => viewportObject.DuplicatedElement != null && paths.Contains(viewportObject.DuplicatedElement.Path)).ToList();
            if (placed.Count > 0)
            {
                SelectObjects(placed, true);
            }
        });
    }

    // Copies of the selected meshes where they are, sharing their meshes, selected in their stead
    private void DuplicatePlacements()
    {
        var selected = SelectedPlacements();
        if (selected.Count == 0)
        {
            return;
        }

        var copies = selected.Select(entry => (SceneryPlacement)Util.CloneUtils.DeepClone(entry.Placement, typeof(SceneryPlacement))).ToList();
        InsertPlacements(copies, copies.Count > 1 ? $"Duplicated {copies.Count} meshes" : "Duplicated a mesh");
    }

    /// <summary>
    /// Collision made of the selected meshes on the picked surface, added to the chunk's collision like the add-on adds it: as coarse as
    /// the game's, which only collides the player with 32 triangles at a time (<see cref="CollisionBuilder.AddMeshes"/>). One step to undo
    /// </summary>
    internal void GenerateCollisionFromSelection()
    {
        var selected = SelectedPlacements();
        if (selected.Count == 0)
        {
            Log.WriteLine("Select the meshes to make collision of first", Log.LogType.Warning);
            return;
        }

        if (SurfaceForNewCollision() is not { } surface)
        {
            Log.WriteLine("The chunk's version of the game has no collision surfaces", Log.LogType.Warning);
            return;
        }

        var meshes = selected.Select(entry => PlacementTriangles(entry.Placement).ToList()).ToList();
        CollisionBuilder.Result? built = null;
        CollisionBuilder.Crowding crowding = default;
        var description = selected.Count > 1 ? $"Made collision of {selected.Count} meshes" : "Made collision of a mesh";
        if (!EditCollision(description, geometry =>
            {
                var (edited, added, result) = CollisionEdits.AddMeshes(geometry, meshes, surface);
                built = result;
                crowding = CollisionEdits.Crowding(edited, added);
                return (edited, null);
            }, keepSelection: true) || built == null)
        {
            if (built is { Triangles.Count: 0 })
            {
                Log.WriteLine($"No collision was made of the {built.Sources} triangles: {built.Skipped} the collision already had, {built.Dropped} flat", Log.LogType.Warning);
            }

            return;
        }

        var hulls = built.Hulls > 0 ? $", {built.Hulls} convex hulls" : string.Empty;
        var coarsened = built.Coarsened > 0 ? $", {built.Coarsened} meshes made coarser where Crash would touch too many triangles" : string.Empty;
        var covered = built.Covered > 0 ? $", {built.Covered} layers lying on others left out" : string.Empty;
        var skipped = built.Skipped > 0 ? $", {built.Skipped} triangles the collision already had" : string.Empty;
        var dropped = built.Dropped > 0 ? $", {built.Dropped} flat ones" : string.Empty;
        Log.WriteLine($"Added {built.Triangles.Count} collision triangles made of the meshes' {built.Sources}{hulls}{coarsened}{covered}{skipped}{dropped}", Log.LogType.Info);
        if (crowding is { Places: > 0, Worst: { } worst })
        {
            Log.WriteLine($"Crash would touch up to {crowding.Most} collision triangles at {crowding.Places} places on the new collision, the most at " +
                          $"({worst.X:0.#}, {worst.Y:0.#}, {worst.Z:0.#}): the game only takes {CollisionBuilder.MostTriangles} at a time and slows him down where " +
                          "there are more. Leave out small meshes there or simplify them", Log.LogType.Warning);
        }

        ShowCollisionLayer();
    }

    #endregion

    #region Collision triangles

    private void RefreshSurfaces()
    {
        if (_document?.DocumentModel is not LevelChunk chunk)
        {
            return;
        }

        var surfaces = AssetManager.Get().GetRelatedAssetsOf<CollisionSurface>(chunk.Package)
            .OrderBy(surface => surface.ID).ThenBy(surface => surface.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(surface =>
            {
                var color = CollisionSurface.GetEditorColor(surface);
                return new CollisionSurfaceChoice(surface.URI, surface.Alias, new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)));
            }).ToList();
        Surfaces = surfaces;
        _lastPickedSurface = surfaces.FirstOrDefault(surface => surface.Uri == _lastPickedSurface?.Uri) ?? surfaces.FirstOrDefault();
        ShowSurfaceOf(_selectedTriangles);
    }

    // The surface new collision goes on: the one last picked, or the version's first
    private LabURI? SurfaceForNewCollision()
    {
        if (Surfaces.Count == 0)
        {
            RefreshSurfaces();
        }

        return (CurrentSurface ?? _lastPickedSurface ?? Surfaces.FirstOrDefault())?.Uri;
    }

    // The picker shows the selected triangles' surface while they share one, and the one new collision gets otherwise
    private void ShowSurfaceOf(IReadOnlyCollection<int> triangles)
    {
        var geometry = _shownGeometry ?? CurrentCollisionGeometry();
        CollisionSurfaceChoice? shown;
        if (triangles.Count == 0 || geometry == null)
        {
            shown = _lastPickedSurface;
        }
        else
        {
            var surfaces = triangles.Where(index => index < geometry.Triangles.Count).Select(index => geometry.Triangles[index].Surface).Distinct().ToList();
            shown = surfaces.Count == 1 ? Surfaces.FirstOrDefault(surface => surface.Uri == surfaces[0]) : null;
        }

        _isShowingSurface = true;
        try
        {
            CurrentSurface = shown;
        }
        finally
        {
            _isShowingSurface = false;
        }
    }

    // A surface picked puts the selected triangles on it, and is what new collision gets
    private void OnSurfacePicked(CollisionSurfaceChoice? surface)
    {
        if (_isShowingSurface || surface == null)
        {
            return;
        }

        _lastPickedSurface = surface;
        var triangles = _selectedTriangles;
        if (triangles.Length == 0)
        {
            return;
        }

        EditCollision(triangles.Length > 1 ? $"Put {triangles.Length} triangles on {surface.Name}" : $"Put a triangle on {surface.Name}",
            geometry => (CollisionEdits.SetSurface(geometry, triangles, surface.Uri), triangles));
    }

    /// <summary>
    /// Changes the chunk's collision as one step to undo and selects the triangles the edit gives, or keeps the selection's
    /// </summary>
    internal bool EditCollision(string description, Func<CollisionGeometry, (CollisionGeometry Geometry, int[]? Selection)> edit, bool keepSelection = false)
    {
        var node = CollisionShapeNode();
        if (node?.GetValue() is not CollisionGeometry geometry || _document == null)
        {
            Log.WriteLine("The chunk's scenery has no collision", Log.LogType.Warning);
            return false;
        }

        var (edited, selection) = edit(geometry);
        if (ReferenceEquals(edited, geometry))
        {
            return false;
        }

        _isEditingCollisionShape = true;
        try
        {
            using var step = _document.History.BeginGroup(description);
            node.SetValue(edited);
        }
        finally
        {
            _isEditingCollisionShape = false;
        }

        _shownGeometry = edited;
        if (selection != null)
        {
            SelectTriangles(selection);
        }
        else if (!keepSelection)
        {
            SelectTriangles([]);
        }

        return true;
    }

    // The collision changed from elsewhere (undo, redo): the triangles selected may be other ones now
    private void FollowCollisionShape(PropertyChange change)
    {
        if (_isEditingCollisionShape || change.Node.Name != nameof(SceneryData.CollisionShape) || change.Node != CollisionShapeNode())
        {
            return;
        }

        _shownGeometry = change.Node.GetValue() as CollisionGeometry;
        if (_selectedTriangles.Length > 0)
        {
            DeselectEverything();
        }
    }

    private void ClearTriangleSelection()
    {
        if (_selectedTriangles.Length == 0 && _trianglePivot == null)
        {
            return;
        }

        _selectedTriangles = [];
        _trianglePivot = null;
        _triangleDragPreview = null;
        this.RaisePropertyChanged(nameof(SelectedTriangleCount));
        ShowSurfaceOf([]);
    }

    /// <summary>
    /// Selects the collision's triangles, the gizmo on the middle of them
    /// </summary>
    internal void SelectTriangles(IReadOnlyCollection<int> triangles)
    {
        var geometry = CurrentCollisionGeometry();
        var node = FindResourceData(typeof(Scenery));
        if (geometry == null || node == null || _renderContext == null || _editingContext == null)
        {
            return;
        }

        _shownGeometry = geometry;
        var selection = triangles.Where(index => index >= 0 && index < geometry.Triangles.Count).Distinct().Order().ToArray();
        if (selection.Length == 0 || CollisionEdits.Bounds(geometry, selection) is not { } bounds)
        {
            if (_trianglePivot != null && SelectedObject == _trianglePivot)
            {
                DeselectEverything();
            }

            ClearTriangleSelection();
            return;
        }

        var size = NVector3.Max(bounds.Max - bounds.Min, new NVector3(0.05f));
        var render = new EditableObject(_renderContext, null, DescribeTriangles(geometry, selection), new vec3(-size.X, -size.Y, -size.Z) * 0.5f, new vec3(size.X, size.Y, size.Z));
        render.SetPosition(ToGlm((bounds.Min + bounds.Max) * 0.5f));
        var pivot = new ViewportObject(render, "COLLISION_SELECTION", node)
        {
            Mode = ViewportEditMode.Scenery,
            Category = ViewportObjectCategory.Collision,
            TransformTarget = new TriangleDragTarget(this, geometry, selection),
        };
        _selectedTriangles = selection;
        _trianglePivot = pivot;
        _editingContext.Select(pivot);
        SelectedObject = pivot;
        RaiseSelectionChanged();
        this.RaisePropertyChanged(nameof(SelectedTriangleCount));
        ShowSurfaceOf(selection);
    }

    // What the viewport's corner says about the selection
    private string DescribeTriangles(CollisionGeometry geometry, int[] selection)
    {
        var surfaces = selection.Select(index => geometry.Triangles[index].Surface).Distinct().ToList();
        var surface = surfaces.Count == 1 ? Surfaces.FirstOrDefault(choice => choice.Uri == surfaces[0])?.Name ?? "one surface" : $"{surfaces.Count} surfaces";
        return selection.Length == 1 ? $"Collision triangle {selection[0]} on {surface}" : $"{selection.Length} collision triangles on {surface}";
    }

    private void SelectTriangleAt(float x, float y)
    {
        if (_scene == null || CurrentCollisionGeometry() is not { } geometry)
        {
            return;
        }

        var ray = _scene.Camera.GetFrameCamera().ScreenRay(new vec2(x, y));
        var picked = CollisionEdits.Pick(geometry, ToSystem(ray.Origin), ToSystem(ray.Direction), out var distance);
        if (picked == null)
        {
            if (IsShiftPressed())
            {
                return;
            }

            DeselectEverything();
            if (TryHitCollision(x, y, out var cursorHit))
            {
                _editingContext?.SetCursorCoordinates(cursorHit);
            }

            return;
        }

        _editingContext?.SetCursorCoordinates(ray.Origin + ray.Direction * distance);
        if (!IsShiftPressed())
        {
            SelectTriangles([picked.Value]);
            return;
        }

        SelectTriangles(_selectedTriangles.Contains(picked.Value) ? _selectedTriangles.Where(index => index != picked.Value).ToArray() : [.. _selectedTriangles, picked.Value]);
    }

    // Every triangle whose middle lies within the rectangle, added to the selection with Shift held
    private void SelectTrianglesInRubberBand()
    {
        if (_scene == null || CurrentCollisionGeometry() is not { } geometry)
        {
            return;
        }

        var camera = _scene.Camera.GetFrameCamera();
        var rect = RubberBand;
        var inside = new List<int>();
        for (var index = 0; index < geometry.Triangles.Count; index++)
        {
            var (a, b, c) = geometry.Corners(index);
            if (camera.WorldToScreen(ToGlm((a + b + c) / 3.0f), out var screen) && rect.Contains(new Point(screen.x, screen.y)))
            {
                inside.Add(index);
            }
        }

        SelectTriangles(IsShiftPressed() ? [.. _selectedTriangles, .. inside] : inside);
    }

    private void SelectAllTriangles()
    {
        if (CurrentCollisionGeometry() is { } geometry)
        {
            SelectTriangles(Enumerable.Range(0, geometry.Triangles.Count).ToArray());
        }
    }

    private void DeleteSelectedTriangles()
    {
        var triangles = _selectedTriangles;
        if (triangles.Length > 0)
        {
            EditCollision(triangles.Length > 1 ? $"Deleted {triangles.Length} collision triangles" : "Deleted a collision triangle",
                geometry => (CollisionEdits.Delete(geometry, triangles), []));
        }
    }

    private void DuplicateSelectedTriangles()
    {
        var triangles = _selectedTriangles;
        if (triangles.Length > 0)
        {
            EditCollision(triangles.Length > 1 ? $"Duplicated {triangles.Length} collision triangles" : "Duplicated a collision triangle", geometry =>
            {
                var (edited, copies) = CollisionEdits.Duplicate(geometry, triangles);
                return (edited, copies);
            });
        }
    }

    /// <summary>
    /// Collision of the shape standing at the cursor on the picked surface, its triangles selected, one step to undo
    /// </summary>
    internal void AddCollisionPrimitive(PlaceholderShape shape)
    {
        if (SurfaceForNewCollision() is not { } surface)
        {
            Log.WriteLine("The chunk's version of the game has no collision surfaces", Log.LogType.Warning);
            return;
        }

        var sources = PlaceholderShapes.Make(shape).Triangles(ToSystem(NewResourcePosition())).ToList();
        EditCollision($"Added a {shape.ToString().ToLowerInvariant()} of collision", geometry =>
        {
            var (edited, added, _) = CollisionEdits.Add(geometry, sources, surface);
            return (edited, added);
        });
    }

    /// <summary>
    /// A triangle lying flat at the cursor on the picked surface, a unit across, selected so it can be moved into place
    /// </summary>
    internal void AddFlatTriangle()
    {
        if (SurfaceForNewCollision() is not { } surface)
        {
            return;
        }

        var cursor = ToSystem(NewResourcePosition());
        var half = FlatTriangleSize / 2.0f;
        // Counter-clockwise seen from above, the collision gets it turned to the game's winding
        (NVector3, NVector3, NVector3)[] sources = [(cursor + new NVector3(-half, 0, half), cursor + new NVector3(half, 0, half), cursor + new NVector3(0, 0, -half))];
        EditCollision("Added a collision triangle", geometry =>
        {
            var (edited, added, _) = CollisionEdits.Add(geometry, sources, surface);
            return (edited, added);
        });
    }

    private void SetTriangleDrawing(bool drawing)
    {
        _trianglePoints = [];
        if (drawing && !IsEditingTriangles)
        {
            SetEditMode(ViewportEditMode.Scenery);
            SetSceneryTarget(true);
        }

        IsDrawingTriangle = drawing;
    }

    // A point of the triangle being drawn where the click hits the collision or a placed mesh, at a corner of the collision close to it
    // on screen, or on the level of the last point (the cursor's) when it hits nothing
    private void AddTrianglePoint(float x, float y)
    {
        if (_scene == null || CurrentCollisionGeometry() is not { } geometry)
        {
            return;
        }

        var camera = _scene.Camera.GetFrameCamera();
        var ray = camera.ScreenRay(new vec2(x, y));
        var origin = ToSystem(ray.Origin);
        var direction = ToSystem(ray.Direction);
        float? distance = CollisionEdits.Pick(geometry, origin, direction, out var collisionDistance) != null ? collisionDistance : null;
        foreach (var placement in _viewportObjects.Where(viewportObject => viewportObject.Render.IsVisible && viewportObject.DuplicatedElement?.GetValue() is SceneryPlacement)
                     .Where(viewportObject => GizmoMath.IntersectBox(ray, viewportObject.Render.GetBoundsTransform()) != null)
                     .Select(viewportObject => (SceneryPlacement)viewportObject.DuplicatedElement!.GetValue()!))
        {
            foreach (var (a, b, c) in PlacementTriangles(placement))
            {
                if (CollisionEdits.RayTriangle(origin, direction, a, b, c) is { } hit && (distance == null || hit < distance))
                {
                    distance = hit;
                }
            }
        }

        vec3 point;
        if (distance != null)
        {
            point = ray.Origin + ray.Direction * distance.Value;
        }
        else
        {
            var level = _trianglePoints.Length > 0 ? _trianglePoints[^1].y : _editingContext?.IsCursorPlaced == true ? _editingContext.GetCursorCoordinates().y : 0.0f;
            if (MathF.Abs(ray.Direction.y) < 1e-6f || (level - ray.Origin.y) / ray.Direction.y <= 0)
            {
                return;
            }

            point = ray.Origin + ray.Direction * ((level - ray.Origin.y) / ray.Direction.y);
        }

        if (CollisionEdits.NearestCorner(geometry, ToSystem(point), camera.WorldUnitsPerPixel(point) * CornerSnapPixels) is { } corner)
        {
            point = ToGlm(geometry.Position(corner));
        }

        _trianglePoints = [.. _trianglePoints, point];
        if (_trianglePoints.Length < 3)
        {
            return;
        }

        var points = _trianglePoints.Select(ToSystem).ToArray();
        _trianglePoints = [];
        // Counter-clockwise seen from the camera, the collision gets it turned so its right-handed normal points away from it
        var facesCamera = NVector3.Dot(NVector3.Cross(points[1] - points[0], points[2] - points[0]), ToSystem(camera.Position) - (points[0] + points[1] + points[2]) / 3.0f) >= 0;
        (NVector3, NVector3, NVector3)[] sources = [facesCamera ? (points[0], points[1], points[2]) : (points[0], points[2], points[1])];
        if (SurfaceForNewCollision() is not { } surface)
        {
            return;
        }

        if (!EditCollision("Drew a collision triangle", current =>
            {
                var (edited, added, _) = CollisionEdits.Add(current, sources, surface);
                return (edited, added);
            }))
        {
            Log.WriteLine("The triangle's points were on a line or it was there already", Log.LogType.Warning);
        }
    }

    // The selected triangles' corners move with the gizmo, the collision's mesh only changes once the drag ends
    private sealed class TriangleDragTarget(ViewportViewModel viewport, CollisionGeometry start, int[] triangles) : IViewportTransformTarget
    {
        private System.Numerics.Matrix4x4 _transform = System.Numerics.Matrix4x4.Identity;

        public void Preview(mat4 transform)
        {
            _transform = transform.ToTwin().ToSystem();
            viewport._triangleDragPreview = new TriangleDragPreview(start, triangles, _transform);
        }

        public void Commit()
        {
            viewport._triangleDragPreview = null;
            if (_transform.IsIdentity)
            {
                return;
            }

            var transform = _transform;
            _transform = System.Numerics.Matrix4x4.Identity;
            // After the drag is done with the gizmo's object, which gets made again around the moved triangles
            Dispatcher.UIThread.Post(() => viewport.EditCollision(triangles.Length > 1 ? $"Moved {triangles.Length} collision triangles" : "Moved a collision triangle",
                geometry => ReferenceEquals(geometry, start) ? (CollisionEdits.Transform(geometry, triangles, transform), triangles) : (geometry, null)));
        }

        public void Cancel()
        {
            _transform = System.Numerics.Matrix4x4.Identity;
            viewport._triangleDragPreview = null;
        }
    }

    // The selected triangles' edges over everything, where a drag has got them to, and the points of a triangle being drawn
    private void DrawSceneryEditing(PrimitiveRenderer renderer, FrameCamera camera)
    {
        if (!_isChunkViewport || !_renderInit)
        {
            return;
        }

        var points = _trianglePoints;
        for (var i = 0; i < points.Length; i++)
        {
            renderer.DrawSphere(points[i], camera.WorldUnitsPerPixel(points[i]) * 5.0f, TrianglePointColor, PrimitiveLayer.Overlay);
            if (i > 0)
            {
                renderer.DrawLine(points[i - 1], points[i], TrianglePointColor, 2.0f, PrimitiveLayer.Overlay);
            }
        }

        var selection = _selectedTriangles;
        var preview = _triangleDragPreview;
        var geometry = preview?.Start ?? _shownGeometry;
        if (selection.Length == 0 || geometry == null)
        {
            return;
        }

        Span<vec3> corners = stackalloc vec3[3];
        foreach (var index in selection)
        {
            if (index >= geometry.Triangles.Count)
            {
                continue;
            }

            var (a, b, c) = geometry.Corners(index);
            if (preview != null)
            {
                a = NVector3.Transform(a, preview.Transform);
                b = NVector3.Transform(b, preview.Transform);
                c = NVector3.Transform(c, preview.Transform);
            }

            corners[0] = ToGlm(a);
            corners[1] = ToGlm(b);
            corners[2] = ToGlm(c);
            renderer.DrawPolyline(corners, TriangleSelectionColor, 2.0f, PrimitiveLayer.WorldXRay, true);
        }
    }

    #endregion

    #region Menu

    private static readonly PlaceholderShape[] Shapes = Enum.GetValues<PlaceholderShape>();

    private IReadOnlyList<ViewportMenuEntry> GetSceneryMenu(bool canCreate)
    {
        var hasScenery = FindScenery() != null;
        var hasCollision = CurrentCollisionGeometry() != null;
        var entries = new List<ViewportMenuEntry>();
        if (IsEditingTriangles)
        {
            entries.AddRange(Shapes.Select(shape => new ViewportMenuEntry($"{shape} of collision", () => AddCollisionPrimitive(shape), canCreate && hasCollision)));
            entries.Add(ViewportMenuEntry.Separator);
            entries.Add(new ViewportMenuEntry("Triangle lying at the cursor", AddFlatTriangle, canCreate && hasCollision));
            entries.Add(new ViewportMenuEntry(IsDrawingTriangle ? "Stop drawing triangles (Esc)" : "Draw triangles by clicking three points", () => SetTriangleDrawing(!IsDrawingTriangle), hasCollision));
            entries.Add(ViewportMenuEntry.Separator);
            entries.Add(new ViewportMenuEntry("Select every triangle (Ctrl+A)", SelectAllTriangles, hasCollision));
            return entries;
        }

        var selected = SelectedPlacements().Count;
        entries.AddRange(Shapes.Select(shape => new ViewportMenuEntry($"{shape} placeholder", () => AddPlaceholder(shape), canCreate && hasScenery)));
        entries.Add(ViewportMenuEntry.Separator);
        entries.Add(new ViewportMenuEntry(selected > 1 ? $"Copies of the {selected} selected meshes here" : "A copy of the selected mesh here", PlaceCopiesOfSelection, canCreate && selected > 0));
        entries.Add(new ViewportMenuEntry(selected > 1 ? $"Collision of the {selected} selected meshes" : "Collision of the selected mesh", GenerateCollisionFromSelection, selected > 0 && hasCollision));
        entries.Add(ViewportMenuEntry.Separator);
        entries.Add(new ViewportMenuEntry("Select every mesh (Ctrl+A)", SelectAllPlacements, hasScenery));
        return entries;
    }

    #endregion

    private static NVector3 ToSystem(vec3 vector) => new(vector.x, vector.y, vector.z);

    private static vec3 ToGlm(NVector3 vector) => new(vector.X, vector.Y, vector.Z);
}
