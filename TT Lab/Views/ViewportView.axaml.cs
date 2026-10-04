using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReactiveUI;
using ReactiveUI.Avalonia;
using TT_Lab.Project.Prefabs;
using TT_Lab.ViewModels;

namespace TT_Lab.Views;

public partial class ViewportView : ReactiveUserControl<ViewportViewModel>, IPrefabDropTarget
{
    private const double ClickDistance = 4.0;
    private Point? _rightPress;

    public ViewportView()
    {
        InitializeComponent();
        // A right click that doesn't turn into a look around opens the menu of what to make at the cursor
        ViewportControl.AddHandler(PointerPressedEvent, (_, e) =>
        {
            var point = e.GetCurrentPoint(ViewportControl);
            _rightPress = point.Properties.IsRightButtonPressed ? point.Position : null;
        }, RoutingStrategies.Tunnel, true);
        ViewportControl.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (e.InitialPressMouseButton != MouseButton.Right || _rightPress is not { } press)
            {
                return;
            }

            _rightPress = null;
            var position = e.GetPosition(ViewportControl);
            if (Math.Abs(position.X - press.X) > ClickDistance || Math.Abs(position.Y - press.Y) > ClickDistance)
            {
                return;
            }

            ShowCreateMenu(position);
        }, RoutingStrategies.Tunnel, true);
    }

    // Prefabs dragged from the Prefabs panel, whichever window it's in, land where they're dropped
    public bool CanDrop(Prefab prefab, Visual hit) => (DataContext as ViewportViewModel)?.IsChunkViewport == true;

    public void Drop(Prefab prefab, Visual hit, PixelPoint screen)
    {
        if (DataContext is not ViewportViewModel viewport)
        {
            return;
        }

        var position = ViewportControl.PointToClient(screen);
        viewport.PlacePrefabAt(prefab, (float)position.X, (float)position.Y);
    }

    private void ShowCreateMenu(Point position)
    {
        if (DataContext is not ViewportViewModel viewport)
        {
            return;
        }

        var entries = viewport.GetCreateMenu((float)position.X, (float)position.Y);
        if (entries.Count == 0)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var entry in entries)
        {
            if (entry.IsSeparator)
            {
                flyout.Items.Add(new Separator());
                continue;
            }

            flyout.Items.Add(new MenuItem
            {
                Header = entry.Header,
                IsEnabled = entry.IsEnabled && entry.Action != null,
                Command = entry.Action == null ? null : ReactiveCommand.Create(entry.Action),
            });
        }

        flyout.ShowAt(ViewportControl, true);
    }
}
