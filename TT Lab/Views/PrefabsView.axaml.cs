using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.Project.Prefabs;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class PrefabsView : ReactiveUserControl<PrefabsViewModel>, IPrefabDropTarget
{
    private const double DragDistance = 6.0;
    private static readonly Cursor DropCursor = new(StandardCursorType.DragCopy);
    private static readonly Cursor MoveCursor = new(StandardCursorType.DragMove);
    private static readonly Cursor NoDropCursor = new(StandardCursorType.No);
    // While the list keeps the pointer the window only takes the cursor of what's under the pointer when that's the list itself, never one
    // of its tiles or another control: the list's cursor, which its tiles inherit, reached the window once, as the drag started over a
    // tile, and the drag showed "can't drop" (X11's X) all the way into the scene. Avalonia's own drag sets the window's cursor override,
    // which is internal (recheck it after updating Avalonia)
    private static readonly MethodInfo? SetCursorOverride = typeof(TopLevel).GetMethod("SetCursorOverride", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Cursor)]);
    private PrefabEntry? _pressed;
    private Point _pressedAt;
    // The prefab being dragged: the list keeps the pointer while it is and finds where it's let go of itself, in whichever of TT Lab's
    // windows that is (PrefabDropTargets)
    private PrefabEntry? _dragged;
    // The window the drag started in, which shows its cursor, and the target it's over, told when the prefab leaves it
    private TopLevel? _dragWindow;
    private IPrefabDropTarget? _dragTarget;

    static PrefabsView()
    {
        // A folder being renamed has its name typed right away
        IsVisibleProperty.Changed.AddClassHandler<TextBox>((box, _) =>
        {
            if (box is { Name: "RenameBox", IsVisible: true })
            {
                Dispatcher.UIThread.Post(() =>
                {
                    box.Focus();
                    box.SelectAll();
                });
            }
        });
    }

    public PrefabsView()
    {
        InitializeComponent();
        PrefabList.AddHandler(LostFocusEvent, (_, e) =>
        {
            if (e.Source is TextBox { Name: "RenameBox", DataContext: PrefabFolderEntry entry })
            {
                entry.CommitRename();
            }
        });

        // A panel shown after the project opened lists what the project has by then
        this.WhenActivated(_ => ViewModel?.Refresh());
        // Dragging a prefab's tile a little starts dragging it, a plain click keeps selecting the tile
        PrefabList.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        PrefabList.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        PrefabList.AddHandler(PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
        PrefabList.AddHandler(PointerCaptureLostEvent, (_, _) => EndDrag(null));
        PrefabList.DoubleTapped += (_, e) =>
        {
            if (e.Source is Visual source && !IsInTextBox(source) && FolderEntryUnder(source) is { } folder)
            {
                ViewModel?.OpenFolder(folder.Path);
                e.Handled = true;
            }
        };
        PrefabList.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        // The mouse's back button goes up a folder like a file manager's
        AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.XButton1Pressed)
            {
                ViewModel?.OpenParentFolder();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
    }

    // A prefab dropped onto a folder's tile or a step of the way to the folder shown goes there
    public bool CanDrop(Prefab prefab, Visual hit) => FolderUnder(hit) != null;

    public void Drop(Prefab prefab, Visual hit, PixelPoint screen)
    {
        if (FolderUnder(hit) is { } folder)
        {
            ViewModel?.Move(prefab, folder);
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (_dragged != null && e.Key == Key.Escape)
        {
            EndDrag(null);
            e.Handled = true;
            return;
        }

        // Typing a folder's name keeps its keys
        if (ViewModel == null || e.Source is Visual source && IsInTextBox(source))
        {
            return;
        }

        var folder = PrefabList.SelectedItem as PrefabFolderEntry;
        switch (e.Key)
        {
            case Key.Enter when folder != null:
                ViewModel.OpenFolder(folder.Path);
                e.Handled = true;
                break;
            case Key.F2 when folder != null:
                folder.StartRenameCommand.Execute().Subscribe();
                e.Handled = true;
                break;
            case Key.Back:
                ViewModel.OpenParentFolder();
                e.Handled = true;
                break;
        }
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(PrefabList).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = EntryUnder(e.Source as Visual);
        _pressedAt = e.GetPosition(PrefabList);
    }

    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged != null)
        {
            var screen = ScreenPoint(e);
            var found = PrefabDropTargets.Find(screen, TopLevel.GetTopLevel(this));
            var target = found is { } over && over.Target.CanDrop(_dragged.Prefab, over.Hit) ? over.Target : null;
            if (target != _dragTarget)
            {
                _dragTarget?.DragLeave();
                _dragTarget = target;
            }

            target?.DragOver(_dragged.Prefab, found!.Value.Hit, screen);
            ShowDragCursor(target == null ? NoDropCursor : target is PrefabsView ? MoveCursor : DropCursor);
            e.Handled = true;
            return;
        }

        if (_pressed == null || !e.GetCurrentPoint(PrefabList).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var moved = e.GetPosition(PrefabList) - _pressedAt;
        if (Math.Abs(moved.X) < DragDistance && Math.Abs(moved.Y) < DragDistance)
        {
            return;
        }

        _dragged = _pressed;
        _pressed = null;
        _dragWindow = TopLevel.GetTopLevel(this);
        e.Pointer.Capture(PrefabList);
        ShowDragCursor(NoDropCursor);
        e.Handled = true;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        _pressed = null;
        if (_dragged == null)
        {
            return;
        }

        var prefab = _dragged.Prefab;
        var screen = ScreenPoint(e);
        EndDrag(e.Pointer);
        if (PrefabDropTargets.Find(screen, TopLevel.GetTopLevel(this)) is { } found && found.Target.CanDrop(prefab, found.Hit))
        {
            found.Target.Drop(prefab, found.Hit, screen);
        }

        e.Handled = true;
    }

    private PixelPoint ScreenPoint(PointerEventArgs e) => PrefabList.PointToScreen(e.GetPosition(PrefabList));

    private void EndDrag(IPointer? pointer)
    {
        if (_dragged == null)
        {
            return;
        }

        _dragged = null;
        _dragTarget?.DragLeave();
        _dragTarget = null;
        ShowDragCursor(null);
        _dragWindow = null;
        if (pointer?.Captured == PrefabList)
        {
            pointer.Capture(null);
        }
    }

    private void ShowDragCursor(Cursor? cursor)
    {
        if (SetCursorOverride != null && _dragWindow != null)
        {
            SetCursorOverride.Invoke(_dragWindow, [cursor]);
            return;
        }

        PrefabList.Cursor = cursor;
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

    private static string? FolderUnder(Visual? visual)
    {
        for (var current = visual; current != null; current = current.GetVisualParent())
        {
            switch (current)
            {
                case Control { DataContext: PrefabFolderEntry entry }:
                    return entry.Path;
                case Control { DataContext: PrefabCrumb crumb }:
                    return crumb.Path;
            }
        }

        return null;
    }

    private static PrefabFolderEntry? FolderEntryUnder(Visual? visual)
    {
        for (var current = visual; current != null; current = current.GetVisualParent())
        {
            if (current is Control { DataContext: PrefabFolderEntry entry })
            {
                return entry;
            }
        }

        return null;
    }

    private static PrefabEntry? EntryUnder(Visual? visual)
    {
        for (var current = visual; current != null; current = current.GetVisualParent())
        {
            if (current is Control { DataContext: PrefabEntry entry })
            {
                return entry;
            }
        }

        return null;
    }
}
