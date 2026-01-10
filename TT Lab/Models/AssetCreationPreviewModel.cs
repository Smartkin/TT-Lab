using System;
using System.Threading.Tasks;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;

namespace TT_Lab.Models;

public class AssetCreationPreviewModel
{
    private readonly IAsset _asset;

    public AssetCreationPreviewModel(IAsset assetToPreview, string displayName, Func<IAsset, AssetCreationStatus>? dataCreator = null)
    {
        _asset = assetToPreview;
        DisplayName = displayName;
        DataCreator = dataCreator;
    }
    
    public AssetCreationPreviewModel(IAsset assetToPreview, string displayName, Func<IAsset, Task<AssetCreationStatus>>? dataCreator = null)
    {
        _asset = assetToPreview;
        DisplayName = displayName;
        DataCreatorAsync = dataCreator;
    }
    
    public string IconPath => $"/Media/LabIcons/{_asset.IconPath}";
    public string DisplayName { get; }
    public Func<IAsset, AssetCreationStatus>? DataCreator { get; }
    public Func<IAsset, Task<AssetCreationStatus>>? DataCreatorAsync { get; }
    public Type AssetType => _asset.GetType();
    public Boolean IsInstance => AssetType.IsAssignableTo(typeof(SerializableInstance));
}