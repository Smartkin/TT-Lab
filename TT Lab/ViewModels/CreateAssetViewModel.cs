using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Caliburn.Micro;
using Splat;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Models;
using TT_Lab.Project;
using TT_Lab.ServiceProviders;
using TT_Lab.Services;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.ViewModels;

public class CreateAssetViewModel : Screen, INotifyDataErrorInfo
{
    private string _assetName = "NewAsset";
    private Enums.Layouts _layId = Enums.Layouts.LAYER_1;
    private AssetCreationPreviewModel _selectedCreationModel;
    private FolderElementViewModel _selectedFolder;
    private readonly IDataValidatorService _dataValidatorService;
    private readonly IActiveChunkService _activeChunkService;
    
    const Int32 ASSET_NAME_LIMIT = 64;
    const String ASSET_NAME_INVALID_CHARS_ERROR = "Asset name must not contain invalid characters";
    const String ASSET_NAME_EMPTY_ERROR = "Asset name must not be empty";
    const String ASSET_NAME_TOO_LONG_ERROR = "Asset name must be less than 64 characters long";
    const String ASSET_NAME_ALREADY_EXISTS = "Asset with the same name already exists";

    public CreateAssetViewModel(IDataValidatorService dataValidatorService, IActiveChunkService activeChunkService)
    {
        _dataValidatorService = dataValidatorService;
        _activeChunkService = activeChunkService;
        _dataValidatorService.RegisterProperty<string>(nameof(AssetName), IsAssetNameValid);
    }

    public void RegisterAssetToCreate<T>(string displayName, Func<IAsset, AssetCreationStatus>? createCallback = null) where T : IAsset, new()
    {
        CreatableAssets.Add(AssetCreationPreviewModelFactory.CreatePreview<T>(displayName, createCallback));
    }
    
    public void RegisterAssetToCreate<T>(string displayName, Func<IAsset, Task<AssetCreationStatus>> createCallback) where T : IAsset, new()
    {
        CreatableAssets.Add(AssetCreationPreviewModelFactory.CreatePreview<T>(displayName, createCallback));
    }

    public void AssignFolder(FolderElementViewModel folder)
    {
        _selectedFolder = folder;
    }

    public async Task CreateAssetButton()
    {
        ITwinIdGeneratorService idGenerator;
        if (SelectedCreationModel.IsInstance)
        {
            Debug.Assert(_activeChunkService.CurrentChunkEditor != null, "A chunk must be active for a new instance to be created");
            idGenerator = TwinIdGeneratorServiceProvider.GetGeneratorForChunk(SelectedCreationModel.AssetType,
                AssetManager.Get().GetAsset(_activeChunkService.CurrentChunkEditor.EditableResource).Variation, LayoutID);
        }
        else
        {
            idGenerator = TwinIdGeneratorServiceProvider.GetGenerator(SelectedCreationModel.AssetType);
        }

        IAsset? newAsset = null;
        if (SelectedCreationModel.DataCreator != null)
        {
            newAsset = AssetFactory.CreateAsset(SelectedCreationModel.AssetType, _selectedFolder.GetAsset<Folder>(),
                _assetName.Trim(), Locator.Current.GetService<ProjectManager>()!.OpenedProject!.BasePackage.ID.ToString(), idGenerator,
                SelectedCreationModel.DataCreator, SelectedCreationModel.IsInstance ? LayoutID : null);
        }
        else if (SelectedCreationModel.DataCreatorAsync != null)
        {
            newAsset = await AssetFactory.CreateAsset(SelectedCreationModel.AssetType,
                _selectedFolder.GetAsset<Folder>(), _assetName.Trim(),
                Locator.Current.GetService<ProjectManager>()!.OpenedProject!.BasePackage.ID.ToString(), idGenerator,
                SelectedCreationModel.DataCreatorAsync, SelectedCreationModel.IsInstance ? LayoutID : null);
        }
        if (newAsset != null)
        {
            await TryCloseAsync();
        }
        
        Log.WriteLine("Error: Failed to create asset");
    }

    private Boolean IsAssetNameValid(string name)
    {
        var isValid = true;

        if (String.IsNullOrEmpty(name.Trim()))
        {
            _dataValidatorService.AddError(nameof(AssetName), ASSET_NAME_EMPTY_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(AssetName), ASSET_NAME_EMPTY_ERROR);
        }

        if (name.Length > ASSET_NAME_LIMIT)
        {
            _dataValidatorService.AddError(nameof(AssetName), ASSET_NAME_TOO_LONG_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(AssetName), ASSET_NAME_TOO_LONG_ERROR);
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
        {
            _dataValidatorService.AddError(nameof(AssetName), ASSET_NAME_INVALID_CHARS_ERROR);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(AssetName), ASSET_NAME_INVALID_CHARS_ERROR);
        }
        
        var assetsOfType = AssetManager.Get().GetAllAssetsOf(SelectedCreationModel.AssetType);
        if (assetsOfType.Any(asset => asset.Name == name))
        {
            _dataValidatorService.AddError(nameof(AssetName), ASSET_NAME_ALREADY_EXISTS);
            isValid = false;
        }
        else
        {
            _dataValidatorService.RemoveError(nameof(AssetName), ASSET_NAME_ALREADY_EXISTS);
        }

        return isValid;
    }

    protected override void OnViewReady(object view)
    {
        base.OnViewReady(view);

        SelectedCreationModel = CreatableAssets[0];
        _dataValidatorService.ValidateProperty(AssetName, nameof(AssetName));
    }

    public BindableCollection<AssetCreationPreviewModel> CreatableAssets { get; set; } = new();

    public string AssetName
    {
        get => _assetName;
        set
        {
            _dataValidatorService.ValidateProperty(value);
            _assetName = value;
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(CanCreate));
        }
    }

    public AssetCreationPreviewModel SelectedCreationModel
    {
        get => _selectedCreationModel;
        set
        {
            _selectedCreationModel = value;
            _assetName = $"New {_selectedCreationModel.DisplayName}";
            NotifyOfPropertyChange();
            NotifyOfPropertyChange(nameof(AssetName));
            NotifyOfPropertyChange(nameof(IsInstance));
            NotifyOfPropertyChange(nameof(LayoutRowHeight));
        }
    }

    public IEnumerable GetErrors(string? propertyName)
    {
        return _dataValidatorService.GetErrors(propertyName);
    }
    
    public Enums.Layouts LayoutID
    {
        get => _layId;
        set
        {
            if (_layId != value)
            {
                _layId = value;
                    
                NotifyOfPropertyChange();
            }
        }
    }

    public bool CanCreate => !HasErrors;

    public bool HasErrors => _dataValidatorService.HasErrors;

    public Boolean IsInstance => SelectedCreationModel.IsInstance;
    
    public string LayoutRowHeight => IsInstance ? "2*" : "0";

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged
    {
        add => _dataValidatorService.ErrorsChanged += value;
        remove => _dataValidatorService.ErrorsChanged -= value;
    }
}