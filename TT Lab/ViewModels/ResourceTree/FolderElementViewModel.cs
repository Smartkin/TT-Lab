using System;
using System.IO;
using System.Threading.Tasks;
using Caliburn.Micro;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Models;
using TT_Lab.Util;
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

    protected override void CreateContextMenu()
    {
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Create Asset",
            Action = CreateItem
        });
        
        var mark = ((Folder)Asset).Mark;
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
        createAssetViewModel.RegisterAssetToCreate<SoundEffect>("Sound Effect", AssetDataFactory.CreateSoundEffectData);
        createAssetViewModel.RegisterAssetToCreate<Texture>("Texture", AssetDataFactory.CreateTextureData);
        createAssetViewModel.RegisterAssetToCreate<Material>("Material", AssetDataFactory.CreateMaterialData);
        createAssetViewModel.RegisterAssetToCreate<Skydome>("Skydome", AssetDataFactory.CreateSkydomeData);
        createAssetViewModel.RegisterAssetToCreate<GameObject>("Game Object", AssetDataFactory.CreateGameObjectData);
        createAssetViewModel.RegisterAssetToCreate<BehaviourGraph>("Behaviour", AssetDataFactory.CreateBehaviourData);
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
            createAssetViewModel.RegisterAssetToCreate<ChunkFolder>("Chunk");
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

    private void CreateItem()
    {
        var assetCreatorDialogue = IoC.Get<CreateAssetViewModel>();
        DefaultCreatableAssets(assetCreatorDialogue);
        ListCreatableAssets(assetCreatorDialogue);
        assetCreatorDialogue.AssignFolder(this);
        IoC.Get<IWindowManager>().ShowDialogAsync(assetCreatorDialogue);
    }
}