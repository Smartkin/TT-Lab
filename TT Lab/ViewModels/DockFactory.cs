using Dock.Model.Core;
using Dock.Model.ReactiveUI;
using TT_Lab.ViewModels.Composite;

namespace TT_Lab.ViewModels;

public class DockFactory : Factory
{
    public override async void CloseDockable(IDockable dockable)
    {
        if (dockable.Context is not TabbedEditorViewModel editorTab)
        {
            base.CloseDockable(dockable);
            return;
        }
        
        var canClose = await editorTab.CloseTab();
        if (canClose)
        {
            base.CloseDockable(dockable);
        }
    }
}