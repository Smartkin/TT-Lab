using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.VisualTree;
using TT_Lab.Assets;
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
/// What takes assets dragged from the project tree: the scene takes game objects, the project tree's folders the assets they can have
/// </summary>
public interface IAssetDropTarget
{
    /// <summary>
    /// Whether the asset can go onto the part of it under the pointer
    /// </summary>
    bool CanDropAsset(IAsset asset, Visual hit);

    void DropAsset(IAsset asset, Visual hit, PixelPoint screen);

    /// <summary>
    /// The asset is dragged over the part of it under the pointer, which can take it
    /// </summary>
    void DragAssetOver(IAsset asset, Visual hit, PixelPoint screen)
    {
    }

    /// <summary>
    /// The asset left it, or the drag ended
    /// </summary>
    void DragAssetLeave()
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
    // While a control keeps the pointer the window only takes the cursor of what's under the pointer when that's the control itself, never
    // one of its rows or another control: the control's cursor, which its rows inherit, reached the window once, as the drag started over a
    // row, and the drag showed "can't drop" (X11's X) all the way into the scene. Avalonia's own drag sets the window's cursor override,
    // which is internal (recheck it after updating Avalonia)
    private static readonly MethodInfo? SetCursorOverride = typeof(TopLevel).GetMethod("SetCursorOverride", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Cursor)]);

    /// <summary>
    /// Shows a drag's cursor over the window it started in, none to put the window's own back
    /// </summary>
    public static void ShowDragCursor(TopLevel? window, Control dragging, Cursor? cursor)
    {
        if (SetCursorOverride != null && window != null)
        {
            SetCursorOverride.Invoke(window, [cursor]);
            return;
        }

        dragging.Cursor = cursor;
    }

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
        return Find<IPrefabDropTarget>(screen, source, windows);
    }

    /// <summary>
    /// The target of the kind under the screen point and the control of it there, an asset dragged from the project tree finds its own
    /// </summary>
    public static (T Target, Visual Hit)? Find<T>(PixelPoint screen, TopLevel? source, IEnumerable<TopLevel>? windows = null) where T : class
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
                if (current is T target)
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
