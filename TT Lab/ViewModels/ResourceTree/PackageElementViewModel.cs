using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TT_Lab.Assets;

namespace TT_Lab.ViewModels.ResourceTree;

public class PackageElementViewModel : ResourceTreeElementViewModel
{
    private bool _isEnabled;
    private MenuItem _isEnabledItem;

    public PackageElementViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null) : base(asset, parent)
    {
    }

    protected override void Deleted()
    {
        BindingOperations.ClearBinding(_isEnabledItem, MenuItem.IsCheckedProperty);
        
        base.Deleted();
    }

    protected override void CreateContextMenu()
    {
        base.CreateContextMenu();

        var binding = new Binding
        {
            Mode = BindingMode.TwoWay,
            Source = this,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            Path = new PropertyPath(nameof(IsPackageEnabled)),
            NotifyOnSourceUpdated = true,
            NotifyOnTargetUpdated = true
        };
        
        _isEnabledItem = RegisterMenuItem(new MenuItemSettings
        {
            Header = "Is Enabled",
            IsCheckable = true,
            IsChecked = binding
        });
    }

    public override bool IsEnabled => IsPackageEnabled;

    public bool IsPackageEnabled
    {
        get => ((Package)Asset).Enabled;
        set
        {
            if (value != ((Package)Asset).Enabled)
            {
                ((Package)Asset).Enabled = value;
                Asset.Serialize(SerializationFlags.SetDirectoryToAssets);
                NotifyOfPropertyChange(nameof(IsEnabled));
            }
        }
    }
}