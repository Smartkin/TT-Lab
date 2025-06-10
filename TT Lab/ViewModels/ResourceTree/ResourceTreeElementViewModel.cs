using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Caliburn.Micro;
using Microsoft.Xaml.Behaviors.Core;
using Newtonsoft.Json;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Util;
using Action = System.Action;

namespace TT_Lab.ViewModels.ResourceTree;

public delegate void DeleteHandler(ResourceTreeElementViewModel deletedViewModel);

/// <summary>
/// Default representation of the resource in the ProjectTree
/// </summary>
public class ResourceTreeElementViewModel : PropertyChangedBase
{
    public event DeleteHandler? OnDeleted;
    
    protected struct MenuItemSettings
    {
        public MenuItemSettings()
        {
            Header = string.Empty;
            Action = null;
            IsCheckable = false;
            IsChecked = null;
        }

        public string Header { get; set; } = "";
        public Action? Action { get; set; } = null;
        public bool IsCheckable { get; set; } = false;
        public Binding? IsChecked { get; set; } = null;
    }
    
    private readonly IAsset _asset;
    private readonly ResourceTreeElementViewModel? _parent;
    private readonly BindableCollection<MenuItem> _menuOptions = new();
    private BindableCollection<ResourceTreeElementViewModel>? _children;
    private List<ResourceTreeElementViewModel>? _internalChildren;
    private string _newAlias;
    private Boolean _isTargetItem;
    private Boolean _isSelected;
    private Boolean _isExpanded;
    private Visibility _isVisible;
    private Boolean _isRenaming;
    private Boolean _contextMenuCreated;

    public ResourceTreeElementViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null)
    {
        _asset = AssetManager.Get().GetAsset(asset);
        _parent = parent;
        _newAlias = Alias;
    }

    public BindableCollection<ResourceTreeElementViewModel>? Children => _asset.Type == typeof(ChunkFolder) ? null : _children;

    public virtual void Init()
    {
    }

    protected virtual void Deleted()
    {
        OnDeleted?.Invoke(this);
    }

    protected void BuildChildren(Folder folder)
    {
        // Build the tree
        var myChildren = folder.GetData().To<FolderData>().Children;
        var children = (from child in myChildren
            orderby _asset.Order
            let c = AssetManager.Get().GetAsset(child)
            select c.GetResourceTreeElement(this));
        _children = new BindableCollection<ResourceTreeElementViewModel>(children);
        _internalChildren = new List<ResourceTreeElementViewModel>(_children);
    }

    public void ClearChildren()
    {
        // Only folders should be capable of this
        if (_internalChildren != null)
        {
            _children = new BindableCollection<ResourceTreeElementViewModel>();
            _internalChildren.ForEach(a => a.ClearChildren());
        }
    }

    public void LoadChildrenBack()
    {
        if (_internalChildren != null)
        {
            _children = new BindableCollection<ResourceTreeElementViewModel>(_internalChildren);
            _internalChildren.ForEach(a => a.LoadChildrenBack());
        }
    }

    public void AddNewChild(ResourceTreeElementViewModel child)
    {
        if (_internalChildren == null || _children == null)
        {
            return;
        }
        
        if (_internalChildren.Contains(child))
        {
            return;
        }
        
        _internalChildren.Add(child);
        AddChild(child);
    }

    public void AddChild(ResourceTreeElementViewModel a)
    {
        if (_internalChildren == null || _children == null)
        {
            return;
        }

        if (!_internalChildren.Contains(a))
        {
            return;
        }
        
        _children.Add(a);
    }

    private void RemoveChild(ResourceTreeElementViewModel child)
    {
        if (_internalChildren == null || _children == null)
        {
            return;
        }
        
        _children.Remove(child);
        _internalChildren.Remove(child);
    }

    protected MenuItem RegisterMenuItem(MenuItemSettings settings = default)
    {
        var newItem = new MenuItem
        {
            Header = settings.Header,
            IsCheckable = settings.IsCheckable,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        
        if (settings.Action != null)
        {
            newItem.Command = new ActionCommand(settings.Action);
        }

        if (settings is { IsCheckable: true, IsChecked: not null })
        {
            newItem.SetBinding(MenuItem.IsCheckedProperty, settings.IsChecked);
        }
        
        _menuOptions.Add(newItem);

        return newItem;
    }

    protected virtual void CreateContextMenu()
    {
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Rename",
            Action = PerformAssetRename,
        });
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Delete",
            Action = StartDeletingAsset,
        });
    }

    public async Task CreateContextMenuAction()
    {
        // TODO: Rework this so menu gets recreated because if you click on many resources the memory consumption is gonna be pretty high
        if (_contextMenuCreated)
        {
            return;
        }
        
        _contextMenuCreated = true;
        await Application.Current.Dispatcher.BeginInvoke(CreateContextMenu, DispatcherPriority.Background);
    }

    public void StopRenaming()
    {
        if (!_isRenaming)
        {
            return;
        }
        
        _isRenaming = false;
        NotifyOfPropertyChange(nameof(IsRenaming));
        NotifyOfPropertyChange(nameof(IsNotRenaming));
    }

    public void SaveRenaming(KeyEventArgs key)
    {
        if (key.Key != Key.Enter)
        {
            return;
        }
        
        _isRenaming = false;
        NotifyOfPropertyChange(nameof(IsRenaming));
        NotifyOfPropertyChange(nameof(IsNotRenaming));
        if (string.IsNullOrEmpty(NewAlias))
        {
            NewAlias = Alias;
            return;
        }
        
        Alias = NewAlias;
        Asset.Serialize(SerializationFlags.SetDirectoryToAssets);
    }
 
    private void PerformAssetRename()
    {
        _isRenaming = true;
        NotifyOfPropertyChange(nameof(IsRenaming));
        NotifyOfPropertyChange(nameof(IsNotRenaming));
    }

    private void StartDeletingAsset()
    {
        var result = new OpenDialogueCommand.DialogueResult();
        var showCommandDialogue = new OpenDialogueCommand(() => new DeleteAssetDialogue(result, this));
        showCommandDialogue.Execute();
        if (result.Result == null)
        {
            return;
        }
        var receivedAnswer = MiscUtils.ConvertEnum<DeleteAssetDialogue.DeleteAnswerResult>(result.Result);
        if (receivedAnswer == DeleteAssetDialogue.DeleteAnswerResult.No)
        {
            return;
        }
        
        DeleteAsset();
        _parent?.RemoveChild(this);
        _parent?.ClearChildren();
        _parent?.LoadChildrenBack();
        _parent?.NotifyOfPropertyChange(nameof(Children));
        
        Deleted();
   }

    private void DeleteAsset()
    {
        _internalChildren?.ForEach(a => a.DeleteAsset());
        Log.WriteLine($"Deleting asset {Alias}");
        Asset.Delete(true);
        _internalChildren?.Clear();
        _children?.Clear();
    }

    public List<ResourceTreeElementViewModel>? GetInternalChildren()
    {
        return Children != null ? _internalChildren : null;
    }

    public Boolean IsTargetItem
    {
        get => _isTargetItem;
        set
        {
            if (value != _isTargetItem)
            {
                _isTargetItem = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public Visibility IsRenaming => _isRenaming ? Visibility.Visible : Visibility.Collapsed;

    public Visibility IsNotRenaming => !_isRenaming ? Visibility.Visible : Visibility.Collapsed;

    public String IconPath => $"/Media/LabIcons/{Asset.IconPath}";

    public Boolean IsSelected
    {
        get => _isSelected;
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public Boolean IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (value != _isExpanded)
            {
                _isExpanded = value;
                NotifyOfPropertyChange();
            }

            if (_isExpanded && _parent != null)
            {
                _parent.IsExpanded = true;
            }
        }
    }
    
    public BindableCollection<MenuItem> MenuOptions => _menuOptions;
    
    public ResourceTreeElementViewModel? Parent => _parent;

    public Visibility IsVisible
    {
        get => _isVisible;
        set
        {
            if (value != _isVisible)
            {
                _isVisible = value;
                NotifyOfPropertyChange();
            }

            if (_isVisible == Visibility.Visible && _parent != null)
            {
                _parent._isVisible = Visibility.Visible;
            }
        }
    }

    public String Alias
    {
        get => _asset.Alias;
        set
        {
            if (value != _asset.Alias)
            {
                _asset.Alias = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public String NewAlias
    {
        get => _newAlias;
        set
        {
            if (value != _newAlias)
            {
                _newAlias = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    public virtual Boolean IsEnabled => true;

    public IAsset Asset => _asset;

    public T GetAsset<T>() where T : IAsset
    {
        return (T)_asset;
    }
}