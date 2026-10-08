using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Caliburn.Micro;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets;
using TT_Lab.Command;
using TT_Lab.Controls;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
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
    // The row it's listed under, which the tree moves an asset's row to when its file turns up in another folder
    private ResourceTreeElementViewModel? _parent;
    private readonly BindableCollection<MenuItem> _menuOptions = new();
    private BindableCollection<ResourceTreeElementViewModel>? _children;
    private List<ResourceTreeElementViewModel>? _internalChildren;
    private string _newAlias;
    private Boolean _isTargetItem;
    private Boolean _isSelected;
    private Boolean _isExpanded;
    private Boolean _isVisible;
    private Boolean _isRenaming;
    private Boolean _contextMenuCreated;

    public ResourceTreeElementViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null)
    {
        _asset = AssetManager.Get().GetAsset(asset);
        _parent = parent;
        _newAlias = Alias;
    }

    public BindableCollection<ResourceTreeElementViewModel>? Children => _asset.Type == typeof(LevelChunk) ? null : _children;

    public virtual void Init()
    {
    }

    protected virtual void Deleted()
    {
        OnDeleted?.Invoke(this);
    }

    protected void BuildChildren(Folder folder)
    {
        // Folders before the rest, each in the order of their names the folder keeps them in
        var myChildren = folder.Children;
        var children = (from child in myChildren
            let c = AssetManager.Get().GetAsset(child)
            select c.GetResourceTreeElement(this)).OrderBy(element => element.Asset is Folder ? 0 : 1);
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

    // Folders before assets, each in the order of their names, like the tree gets built
    private static readonly StringComparer NameOrder = StringComparer.OrdinalIgnoreCase;

    private static int InsertionIndex(IReadOnlyList<ResourceTreeElementViewModel> siblings, ResourceTreeElementViewModel child)
    {
        var childIsFolder = child.Asset is Folder;
        for (var i = 0; i < siblings.Count; i++)
        {
            var siblingIsFolder = siblings[i].Asset is Folder;
            if (childIsFolder != siblingIsFolder)
            {
                if (childIsFolder)
                {
                    return i;
                }

                continue;
            }

            if (NameOrder.Compare(siblings[i].Alias, child.Alias) > 0)
            {
                return i;
            }
        }

        return siblings.Count;
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
        
        _internalChildren.Insert(InsertionIndex(_internalChildren, child), child);
        child._parent = this;
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
        
        _children.Insert(InsertionIndex(_children, a), a);
    }

    internal void RemoveChild(ResourceTreeElementViewModel child)
    {
        if (_internalChildren == null || _children == null)
        {
            return;
        }
        
        _children.Remove(child);
        _internalChildren.Remove(child);
        if (child._parent == this)
        {
            child._parent = null;
        }
    }

    protected MenuItem RegisterMenuItem(MenuItemSettings settings = default)
    {
        var newItem = new MenuItem
        {
            Header = settings.Header,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center
        };
        
        if (settings.Action != null)
        {
            newItem.Command = new GenerateCommand(settings.Action);
        }

        if (settings is { IsCheckable: true, IsChecked: not null })
        {
            newItem.ToggleType = MenuItemToggleType.CheckBox;
            newItem.Bind(MenuItem.IsCheckedProperty, settings.IsChecked);
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
        RegisterRelocationItems();
    }

    // Duplicate and Move To..., for what can be copied or moved
    protected void RegisterRelocationItems(bool canMove = true)
    {
        if (AssetRelocation.WhyNotDuplicable(Asset) == null)
        {
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Duplicate",
                Action = Duplicate,
            });
        }

        if (canMove && AssetRelocation.WhyNotMovable(Asset) == null)
        {
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Move To...",
                Action = MoveTo,
            });
        }
    }

    private async void Duplicate()
    {
        try
        {
            await AssetRelocation.DuplicateAsync(Asset);
        }
        catch (Exception exception)
        {
            Log.WriteLine($"Duplicating {Alias} failed: {exception.Message}", Log.LogType.Error);
        }
    }

    private async void MoveTo()
    {
        try
        {
            await AssetRelocation.MoveToAsync(Asset);
        }
        catch (Exception exception)
        {
            Log.WriteLine($"Moving {Alias} failed: {exception.Message}", Log.LogType.Error);
        }
    }

    public virtual void CreateEditor()
    {
        var mainShell = Locator.Current.GetService<ILabManager>()!;
        mainShell.OpenEditor(Asset);
    }

    public async Task CreateContextMenuAction()
    {
        _menuOptions.Clear();
        
        await Dispatcher.UIThread.InvokeAsync(CreateContextMenu, DispatcherPriority.Background);
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

        if (!NameRules.IsAscii(NewAlias))
        {
            Log.WriteLine($"{Alias} isn't renamed to {NewAlias}: a name {NameRules.AsciiOnly}", Log.LogType.Warning);
            NewAlias = Alias;
            return;
        }

        // A folder is its directory, renaming it moves what's in it and gives the links to that the new place. Only its alias changed
        // before, the next look at the disk took it out and read the directory of the old name back
        if (Asset is Folder folder)
        {
            RenameFolder(folder, NewAlias);
            return;
        }

        Alias = NewAlias;
        Asset.Serialize(SerializationFlags.SetDirectoryToAssets);
    }

    private async void RenameFolder(Folder folder, string name)
    {
        try
        {
            if (!await AssetRelocation.RenameAsync(folder, name))
            {
                NewAlias = Alias;
            }
        }
        catch (Exception exception)
        {
            NewAlias = Alias;
            Log.WriteLine($"Renaming {Alias} failed: {exception.Message}", Log.LogType.Error);
        }
    }
 
    private void PerformAssetRename()
    {
        _isRenaming = true;
        NotifyOfPropertyChange(nameof(IsRenaming));
        NotifyOfPropertyChange(nameof(IsNotRenaming));
    }

    protected async void StartDeletingAsset()
    {
        var result = new OpenDialogueCommand.DialogueResult();
        var showCommandDialogue = new DeleteAssetDialogue(result, this);
        await showCommandDialogue.ShowDialog(MiscUtils.GetMainWindow());
        if (result.Result == null)
        {
            return;
        }
        var receivedAnswer = MiscUtils.ConvertEnum<DeleteAssetDialogue.DeleteAnswerResult>(result.Result);
        if (receivedAnswer == DeleteAssetDialogue.DeleteAnswerResult.No)
        {
            return;
        }
        
        await DeleteAsync();
    }

    // The rows change in place: making the folder's rows again gave every folder under it new lists its rows in the tree didn't show,
    // and a row moved to the folder its file is in got taken out of the one it was made in, it stayed in the tree
    internal async Task<bool> DeleteAsync()
    {
        Log.WriteLine($"Deleting asset {Alias}...");
        if (!await AssetDeletion.DeleteAsync(Asset))
        {
            return false;
        }

        _internalChildren?.Clear();
        _children?.Clear();
        _parent?.RemoveChild(this);
        Deleted();
        return true;
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

    public Boolean IsRenaming => _isRenaming;

    public Boolean IsNotRenaming => !_isRenaming;

    public Bitmap IconPath => MiscUtils.GetLabIcon(System.IO.Path.GetFileNameWithoutExtension(Asset.IconPath));

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

    public Boolean IsVisible
    {
        get => _isVisible;
        set
        {
            if (value != _isVisible)
            {
                _isVisible = value;
                NotifyOfPropertyChange();
            }

            if (_isVisible && _parent != null)
            {
                _parent._isVisible = true;
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