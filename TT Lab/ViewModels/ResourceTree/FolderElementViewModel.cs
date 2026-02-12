using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Models;
using TT_Lab.Util;
using TT_Lab.Views;
using Twinsanity.Libraries;
using Path = TT_Lab.Assets.Instance.Path;

namespace TT_Lab.ViewModels.ResourceTree;

// TODO: Currently we can't determine if the folder belongs to a chunk.
// TODO: Need to fix that for proper context menus
public class FolderElementViewModel : ResourceTreeElementViewModel
{
    public FolderElementViewModel(LabURI asset, ResourceTreeElementViewModel? parent = null) : base(asset, parent)
    {
    }

    public override void Init()
    {
        base.Init();
        
        BuildChildren((Folder)Asset);
    }

    public override bool IsEnabled => !GetMark().HasFlag(FolderMark.IsPackage) || IsPackageEnabled;

    public bool IsPackageEnabled
    {
        get => AssetManager.Get().GetAsset<Package>(Asset.Package).Enabled;
        set
        {
            var package = AssetManager.Get().GetAsset<Package>(Asset.Package);
            if (value != package.Enabled)
            {
                package.Enabled = value;
                package.Serialize(SerializationFlags.SetDirectoryToAssets);
                NotifyOfPropertyChange(nameof(IsEnabled));
            }
        }
    }

    private FolderMark GetMark()
    {
        return ((Folder)Asset).Mark;
    }
    
    protected override void CreateContextMenu()
    {
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Create Asset",
            Action = CreateItem
        });
        
        var mark = ((Folder)Asset).Mark;

        if (mark.HasFlag(FolderMark.IsPackage))
        {
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Open Settings"
            });
            
            var binding = new Binding
            {
                Mode = BindingMode.TwoWay,
                Source = this,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Path = nameof(IsPackageEnabled),
                // NotifyOnSourceUpdated = true,
                // NotifyOnTargetUpdated = true
            };
        
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Is Enabled",
                IsCheckable = true,
                IsChecked = binding
            });
        }
        
        if (!mark.HasFlag(FolderMark.Locked))
        {
            base.CreateContextMenu();
        }
    }

    private void DefaultCreatableAssets(CreateAssetViewModel createAssetViewModel)
    {
        createAssetViewModel.RegisterAssetToCreate<Folder>("Folder", asset => AssetDataFactory.CreateFolderData(Asset, asset));
    }

    private void ListNormalFolderCreatableAssets(CreateAssetViewModel createAssetViewModel)
    {
        createAssetViewModel.RegisterAssetToCreate<LevelChunk>("Chunk", AssetDataFactory.CreateChunkData);
        createAssetViewModel.RegisterAssetToCreate<GameObject>("Game Object", AssetDataFactory.CreateGameObjectData);
        createAssetViewModel.RegisterAssetToCreate<BehaviourGraph>("Behaviour", AssetDataFactory.CreateBehaviourData);
        createAssetViewModel.RegisterAssetToCreate<SoundEffect>("Sound Effect", AssetDataFactory.CreateSoundEffectData);
        createAssetViewModel.RegisterAssetToCreate<Skydome>("Skydome", AssetDataFactory.CreateSkydomeData);
    }

    private void ListInstanceCreatableAssets(CreateAssetViewModel createAssetViewModel)
    {
        var mark = ((Folder)Asset).Mark;
        if (mark.HasFlag(FolderMark.DefaultOnly))
        {
            createAssetViewModel.RegisterAssetToCreate<CollisionSurface>("Collision Surface", AssetDataFactory.CreateCollisionSurfaceData);
            createAssetViewModel.RegisterAssetToCreate<InstanceTemplate>("Instance Template", AssetDataFactory.CreateInstanceTemplateData);
            return;
        }
        
        createAssetViewModel.RegisterAssetToCreate<AiPath>("AI Path", AssetDataFactory.CreateAiPathData);
        createAssetViewModel.RegisterAssetToCreate<AiPosition>("AI Position", AssetDataFactory.CreateAiPositionData);
        createAssetViewModel.RegisterAssetToCreate<Camera>("Camera", AssetDataFactory.CreateCameraData);
        createAssetViewModel.RegisterAssetToCreate<ObjectInstance>("Object Instance", AssetDataFactory.CreateObjectInstanceData);
        createAssetViewModel.RegisterAssetToCreate<Path>("Path", AssetDataFactory.CreatePathData);
        createAssetViewModel.RegisterAssetToCreate<Position>("Position", AssetDataFactory.CreatePositionData);
        createAssetViewModel.RegisterAssetToCreate<Trigger>("Trigger", AssetDataFactory.CreateTriggerData);
    }

    protected virtual void ListCreatableAssets(CreateAssetViewModel createAssetViewModel)
    {
        var mark = ((Folder)Asset).Mark;
        if (mark.HasFlag(FolderMark.ChunksOnly))
        {
            createAssetViewModel.RegisterAssetToCreate<LevelChunk>("Chunk");
            return;
        }

        if (mark.HasFlag(FolderMark.InChunk))
        {
            ListInstanceCreatableAssets(createAssetViewModel);
            return;
        }
        
        if (mark.HasFlag(FolderMark.Normal))
        {
            ListNormalFolderCreatableAssets(createAssetViewModel);
        }
    }

    private async void CreateItem()
    {
        var assetCreatorDialogue = Locator.Current.GetService<CreateAssetViewModel>()!;
        DefaultCreatableAssets(assetCreatorDialogue);
        ListCreatableAssets(assetCreatorDialogue);
        assetCreatorDialogue.AssignFolder(this);
        var dialogue = new CreateAssetView
        {
            DataContext = assetCreatorDialogue
        };
        await dialogue.ShowDialog(MiscUtils.GetMainWindow());
    }
}