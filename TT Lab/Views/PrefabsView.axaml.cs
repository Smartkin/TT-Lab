using System;
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
    private PrefabEntry? _pressed;
    private Point _pressedAt;
    // The prefab being dragged: the list keeps the pointer while it is and finds where it's let go of itself, in whichever of TT Lab's
    // windows that is (PrefabDropTargets)
    private PrefabEntry? _dragged;

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
            var found = PrefabDropTargets.Find(ScreenPoint(e), TopLevel.GetTopLevel(this));
            PrefabList.Cursor = found is { } target && target.Target.CanDrop(_dragged.Prefab, target.Hit) ? target.Target is PrefabsView ? MoveCursor : DropCursor : NoDropCursor;
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
        e.Pointer.Capture(PrefabList);
        PrefabList.Cursor = NoDropCursor;
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
        PrefabList.Cursor = null;
        if (pointer?.Captured == PrefabList)
        {
            pointer.Capture(null);
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
