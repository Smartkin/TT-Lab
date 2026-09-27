using System.Collections.Generic;
using Dock.Model.Controls;
using Dock.Model.Core;

namespace TT_Lab.Extensions;

public static class DockExtensions
{
    public static IEnumerable<(IDock Owner, IDockable Dockable)> EnumerateDockables(this IDock dock)
    {
        if (dock.VisibleDockables != null)
        {
            foreach (var dockable in dock.VisibleDockables)
            {
                if (dockable is IDock childDock)
                {
                    foreach (var child in childDock.EnumerateDockables())
                    {
                        yield return child;
                    }

                    continue;
                }

                yield return (dock, dockable);
            }
        }

        foreach (var windowLayout in dock.EnumerateWindowLayouts())
        {
            foreach (var child in windowLayout.EnumerateDockables())
            {
                yield return child;
            }
        }
    }

    public static IEnumerable<IDock> EnumerateDocks(this IDock dock)
    {
        yield return dock;

        if (dock.VisibleDockables != null)
        {
            foreach (var childDock in dock.VisibleDockables)
            {
                if (childDock is not IDock child)
                {
                    continue;
                }

                foreach (var descendant in child.EnumerateDocks())
                {
                    yield return descendant;
                }
            }
        }

        foreach (var windowLayout in dock.EnumerateWindowLayouts())
        {
            foreach (var descendant in windowLayout.EnumerateDocks())
            {
                yield return descendant;
            }
        }
    }

    public static bool IsDescendantOf(this IDockable dockable, IDockable ancestor)
    {
        for (var owner = dockable.Owner; owner != null; owner = owner.Owner)
        {
            if (owner == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<IDock> EnumerateWindowLayouts(this IDock dock)
    {
        if (dock is not IRootDock { Windows: not null } root)
        {
            yield break;
        }

        foreach (var window in root.Windows)
        {
            if (window.Layout != null)
            {
                yield return window.Layout;
            }
        }
    }
}
