using System;
using TT_Lab.Assets;

namespace TT_Lab.ViewModels.ResourceTree;

public delegate void DuplicateEventHandler(InstanceElementViewModel sender, ResourceTreeElementViewModel duplicate);

public abstract class InstanceElementViewModel : ResourceTreeElementViewModel
{
    public event DuplicateEventHandler? OnDuplicate;
    protected ResourceTreeElementViewModel? Duplicate;
    
    protected InstanceElementViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null) : base(asset, parent)
    {
    }

    protected override void CreateContextMenu()
    {
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Duplicate",
            Action = DuplicateInstance
        });
        base.CreateContextMenu();
    }

    protected virtual void DuplicateInstance()
    {
        OnDuplicate?.Invoke(this, Duplicate!);
        Duplicate = null;
    }
}