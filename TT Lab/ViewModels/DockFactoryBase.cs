using Dock.Model.Core;
using Dock.Model.ReactiveUI;

namespace TT_Lab.ViewModels;

public abstract class DockFactoryBase : Factory
{
    public override void InitDockable(IDockable dockable, IDockable? owner)
    {
        // Dock's tabs bind through every dockable's overrides and its dock's policy, and logged a binding error for each one without them.
        // Values left null inherit, the same as no object at all
        dockable.DockCapabilityOverrides ??= new DockCapabilityOverrides();
        if (dockable is IDock dock)
        {
            dock.DockCapabilityPolicy ??= new DockCapabilityPolicy();
        }

        base.InitDockable(dockable, owner);
    }
}
