using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class PrefabsView : ReactiveUserControl<PrefabsViewModel>
{
    private const double DragDistance = 6.0;
    private PrefabEntry? _pressed;
    private Point _pressedAt;

    public PrefabsView()
    {
        InitializeComponent();
        // A panel shown after the project opened lists what the project has by then
        this.WhenActivated(_ => ViewModel?.Refresh());
        // Dragging a row a little starts a drag of its prefab, a plain click keeps selecting the row
        PrefabList.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        PrefabList.AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        PrefabList.AddHandler(PointerReleasedEvent, (_, _) => _pressed = null, RoutingStrategies.Tunnel);
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

    private async void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_pressed == null || !e.GetCurrentPoint(PrefabList).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var moved = e.GetPosition(PrefabList) - _pressedAt;
        if (Math.Abs(moved.X) < DragDistance && Math.Abs(moved.Y) < DragDistance)
        {
            return;
        }

        var entry = _pressed;
        _pressed = null;
        var data = new DataObject();
        data.Set(PrefabsViewModel.DragFormat, entry.Prefab);
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy);
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
