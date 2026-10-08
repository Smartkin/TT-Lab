using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI.Avalonia;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Views;

public partial class ProjectTreeView : ReactiveUserControl<ProjectTreeViewModel>, IAssetDropTarget
{
    private const double DragDistance = 6.0;
    private static readonly Cursor DropCursor = new(StandardCursorType.DragCopy);
    private static readonly Cursor MoveCursor = new(StandardCursorType.DragMove);
    private static readonly Cursor NoDropCursor = new(StandardCursorType.No);
    private IAsset? _pressed;
    private Point _pressedAt;
    // The asset being dragged: the tree keeps the pointer while it is and finds where it's let go of itself, in whichever of TT Lab's
    // windows that is, like the Prefabs panel's prefabs (PrefabDropTargets)
    private IAsset? _dragged;
    // The window the drag started in, which shows its cursor, and the target it's over, told when the asset leaves it
    private TopLevel? _dragWindow;
    private IAssetDropTarget? _dragTarget;
    // Where what's dragged can go in the tree, worked out once a drag, and the folder's row it would go into
    private AssetRelocation.Destinations? _destinations;
    private ResourceTreeElementViewModel? _targetRow;

    public ProjectTreeView()
    {
        InitializeComponent();
        // Dragging a game object's row a little starts dragging it into a scene, a plain click keeps selecting the row
        ProjectTree.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        ProjectTree.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        ProjectTree.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
        ProjectTree.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(null));
        ProjectTree.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (_dragged != null && e.Key == Key.Escape)
            {
                EndDrag(null);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    // What can be dragged: what can move into another folder of the tree, and game objects into a chunk's scene
    private static bool IsDraggable(IAsset asset) => asset is GameObject || AssetRelocation.WhyNotMovable(asset) == null;

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        _pressed = null;
        if (!e.GetCurrentPoint(ProjectTree).Properties.IsLeftButtonPressed || e.Source is not Visual source || IsInTextBox(source))
        {
            return;
        }

        if (RowUnder(source)?.Asset is { } asset)
        {
            _pressed = asset;
            _pressedAt = e.GetPosition(ProjectTree);
        }
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged != null)
        {
            var screen = ScreenPoint(e);
            var found = PrefabDropTargets.Find<IAssetDropTarget>(screen, TopLevel.GetTopLevel(this));
            var target = found is { } over && over.Target.CanDropAsset(_dragged, over.Hit) ? over.Target : null;
            if (target != _dragTarget)
            {
                _dragTarget?.DragAssetLeave();
                _dragTarget = target;
            }

            target?.DragAssetOver(_dragged, found!.Value.Hit, screen);
            PrefabDropTargets.ShowDragCursor(_dragWindow, ProjectTree, target == null ? NoDropCursor : target == this ? MoveCursor : DropCursor);
            e.Handled = true;
            return;
        }

        if (_pressed == null || !e.GetCurrentPoint(ProjectTree).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var moved = e.GetPosition(ProjectTree) - _pressedAt;
        if (Math.Abs(moved.X) < DragDistance && Math.Abs(moved.Y) < DragDistance)
        {
            return;
        }

        // Asked once the row is dragged, a click doesn't look at what's in a folder
        var pressed = _pressed;
        _pressed = null;
        if (!IsDraggable(pressed))
        {
            return;
        }

        _dragged = pressed;
        _destinations = AssetRelocation.WhyNotMovable(pressed) == null ? new AssetRelocation.Destinations(pressed) : null;
        _dragWindow = TopLevel.GetTopLevel(this);
        e.Pointer.Capture(ProjectTree);
        PrefabDropTargets.ShowDragCursor(_dragWindow, ProjectTree, NoDropCursor);
        e.Handled = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressed = null;
        if (_dragged == null)
        {
            return;
        }

        var asset = _dragged;
        var screen = ScreenPoint(e);
        // Asked while the drag is on, the tree's folders go by what it worked out for the drag
        var found = PrefabDropTargets.Find<IAssetDropTarget>(screen, TopLevel.GetTopLevel(this));
        var takes = found is { } over && over.Target.CanDropAsset(asset, over.Hit);
        EndDrag(e.Pointer);
        if (takes)
        {
            found!.Value.Target.DropAsset(asset, found.Value.Hit, screen);
        }

        e.Handled = true;
    }

    private PixelPoint ScreenPoint(PointerEventArgs e) => ProjectTree.PointToScreen(e.GetPosition(ProjectTree));

    private void EndDrag(IPointer? pointer)
    {
        if (_dragged == null)
        {
            return;
        }

        _dragged = null;
        _destinations = null;
        _dragTarget?.DragAssetLeave();
        _dragTarget = null;
        PrefabDropTargets.ShowDragCursor(_dragWindow, ProjectTree, null);
        _dragWindow = null;
        if (pointer?.Captured == ProjectTree)
        {
            pointer.Capture(null);
        }
    }

    // The folder row under the pointer that takes what's dragged
    private ResourceTreeElementViewModel? FolderRowTaking(IAsset asset, Visual hit)
    {
        if (_destinations == null || _destinations.Item != RowItemOf(asset) || RowUnder(hit) is not { Asset: Folder folder } row)
        {
            return null;
        }

        return _destinations.WhyNot(folder) == null ? row : null;
    }

    private static IAsset RowItemOf(IAsset asset) => asset is LevelChunk chunk && AssetManager.Get().DoesAssetExist(chunk.URI) ? SafeChunkFolder(chunk) : asset;

    private static IAsset SafeChunkFolder(LevelChunk chunk)
    {
        try
        {
            return chunk.GetChunkFolder();
        }
        catch (Exception)
        {
            return chunk;
        }
    }

    public bool CanDropAsset(IAsset asset, Visual hit) => FolderRowTaking(asset, hit) != null;

    public void DropAsset(IAsset asset, Visual hit, PixelPoint screen)
    {
        Highlight(null);
        if (RowUnder(hit) is { Asset: Folder folder })
        {
            _ = MoveDropped(asset, folder);
        }
    }

    private static async Task MoveDropped(IAsset asset, Folder folder)
    {
        try
        {
            await AssetRelocation.MoveAsync(asset, folder);
        }
        catch (Exception exception)
        {
            Log.WriteLine($"Moving {asset.Alias} failed: {exception.Message}", Log.LogType.Error);
        }
    }

    public void DragAssetOver(IAsset asset, Visual hit, PixelPoint screen) => Highlight(FolderRowTaking(asset, hit));

    public void DragAssetLeave() => Highlight(null);

    private void Highlight(ResourceTreeElementViewModel? row)
    {
        if (row == _targetRow)
        {
            return;
        }

        if (_targetRow != null)
        {
            _targetRow.IsTargetItem = false;
        }

        _targetRow = row;
        if (row != null)
        {
            row.IsTargetItem = true;
        }
    }

    private static bool IsInTextBox(Visual visual)
    {
        for (var current = visual; current != null; current = current.GetVisualParent())
        {
            if (current is TextBox)
            {
                return true;
            }
        }

        return false;
    }

    private static ResourceTreeElementViewModel? RowUnder(Visual visual)
    {
        for (var current = visual; current != null; current = current.GetVisualParent())
        {
            if (current is Control { DataContext: ResourceTreeElementViewModel row })
            {
                return row;
            }
        }

        return null;
    }
}
