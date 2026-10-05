using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.VisualTree;
using TT_Lab.Project.Prefabs;

namespace TT_Lab.Views;

/// <summary>
/// What a prefab dragged from the Prefabs panel can be dropped onto, in any of TT Lab's windows
/// </summary>
public interface IPrefabDropTarget
{
    /// <summary>
    /// Whether the prefab can go onto the part of it under the pointer
    /// </summary>
    bool CanDrop(Prefab prefab, Visual hit);

    void Drop(Prefab prefab, Visual hit, PixelPoint screen);

    /// <summary>
    /// The prefab is dragged over the part of it under the pointer, which can take it
    /// </summary>
    void DragOver(Prefab prefab, Visual hit, PixelPoint screen)
    {
    }

    /// <summary>
    /// The prefab left it, or the drag ended
    /// </summary>
    void DragLeave()
    {
    }
}

/// <summary>
/// Finds where a prefab dragged from the Prefabs panel lands by the screen point it's let go at, in whichever of TT Lab's windows is there.
/// Avalonia's drag and drop on Linux only tells the window the drag started in (the window manager hands that window every pointer event
/// while the button is down), so a prefab dragged out of a floating Prefabs panel never reached the viewport
/// </summary>
public static class PrefabDropTargets
{
    /// <summary>
    /// TT Lab's windows, the main one and the floating docks', which the tests give themselves
    /// </summary>
    internal static Func<IEnumerable<TopLevel>> Windows { get; set; } = () =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows ?? [];

    /// <summary>
    /// The drop target under the screen point and the control of it there, the window the drag started in first, then TT Lab's others
    /// </summary>
    public static (IPrefabDropTarget Target, Visual Hit)? Find(PixelPoint screen, TopLevel? source, IEnumerable<TopLevel>? windows = null)
    {
        windows ??= Windows();
        var candidates = source == null ? windows : windows.Where(window => window != source).Prepend(source);
        foreach (var window in candidates.Where(window => window.IsVisible))
        {
            var point = window.PointToClient(screen);
            if (point.X < 0 || point.Y < 0 || point.X >= window.Bounds.Width || point.Y >= window.Bounds.Height)
            {
                continue;
            }

            if (window.InputHitTest(point) is not Visual hit)
            {
                return null;
            }

            for (var current = hit; current != null; current = current.GetVisualParent())
            {
                if (current is IPrefabDropTarget target)
                {
                    return (target, hit);
                }
            }

            // The topmost window there has nothing to drop onto
            return null;
        }

        return null;
    }
}
