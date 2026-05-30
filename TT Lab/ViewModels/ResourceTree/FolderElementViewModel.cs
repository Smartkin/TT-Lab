using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Data;
using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Graphics;
using TT_Lab.Assets.Instance;
using TT_Lab.Models;
using TT_Lab.Project;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
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

        if (GetMark().HasFlag(FolderMark.IsChunk))
        {
            return;
        }
        
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
        var mark = GetMark();

        if (mark.HasFlag(FolderMark.IsChunk))
        {
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Build chunk",
                Action = async () => await RebuildChunk()
            });
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Build with neighbouring chunks",
                Action = RebuildChunkAndLinks
            });
            return;
        }
        
        RegisterMenuItem(new MenuItemSettings
        {
            Header = "Create Asset",
            Action = CreateItem
        });

        if (mark.HasFlag(FolderMark.IsPackage))
        {
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Open Settings",
                Action = OpenPackageSettings
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
        else
        {
            RegisterMenuItem(new MenuItemSettings
            {
                Header = "Build contained chunks",
                Action = BuildContainedChunks
            });
        }
        
        if (!mark.HasFlag(FolderMark.Locked))
        {
            base.CreateContextMenu();
        }
    }
    
    private async void BuildContainedChunks()
    {
        if (Children == null)
        {
            return;
        }
        
        Log.WriteLine("Started building contained chunks...");
        foreach (var child in Children)
        {
            if (child is not FolderElementViewModel folder)
            {
                continue;
            }

            if (folder.GetMark().HasFlag(FolderMark.IsChunk))
            {
                await folder.RebuildChunk();
            }
        }
        Log.WriteLine("Finished building contained chunks...");
    }

    private void OpenPackageSettings()
    {
        Locator.Current.GetService<ILabManager>()!.OpenEditor(AssetManager.Get().GetAsset<Package>(Asset.Package));
    }

    private void DefaultCreatableAssets(CreateAssetViewModel createAssetViewModel)
    {
        createAssetViewModel.RegisterAssetToCreate<Folder>("Folder", asset => AssetDataFactory.CreateFolderData(Asset, asset));
    }

    private void ListNormalFolderCreatableAssets(CreateAssetViewModel createAssetViewModel)
    {
        createAssetViewModel.RegisterAssetToCreate<LevelChunk>("Chunk", AssetDataFactory.CreateChunkData);
        createAssetViewModel.RegisterAssetToCreate<GameObject>("Game Object", AssetDataFactory.CreateGameObjectData);
        createAssetViewModel.RegisterAssetToCreate<OGI>("Game Model", AssetDataFactory.CreateOgiData);
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

    private IAsset GetFirstChild() => AssetManager.Get().GetAsset(((Folder)Asset).Children[0]);
    
    private async void RebuildChunkAndLinks()
    {
        try
        {
            var projectManager = Locator.Current.GetService<ProjectManager>()!;
            projectManager.WorkableProject = false;
            using var buildTask = Task.Factory.StartNew(() =>
            {
                Locator.Current.GetService<ProjectManager>()!.OpenedProject!.PackChunk(GetFirstChild().URI);
            });
            await buildTask;

            var assetManager = AssetManager.Get();
            var links = ((LevelChunk)GetFirstChild()).ChunkResources.FirstOrDefault(uri => assetManager.GetAsset(uri) is ChunkLinks, LabURI.Empty);
            if (links != LabURI.Empty)
            {
                var assetLinks = assetManager.GetAsset(links);
                var linksData = assetLinks.GetData<ChunkLinksData>();
                foreach (var link in linksData.Links)
                {
                    using var linkTask = Task.Factory.StartNew(() =>
                    {
                        Locator.Current.GetService<ProjectManager>()!.OpenedProject!.PackChunk(link.Path);
                    });
                    await linkTask;
                }
            }
            projectManager.WorkableProject = true;
        }
        catch (Exception e)
        {
            Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
            Log.WriteLine($"Error when building chunk: {e.Message}");
        }
    }

    private async Task RebuildChunk()
    {
        try
        {
            var projectManager = Locator.Current.GetService<ProjectManager>()!;
            projectManager.WorkableProject = false;
            var buildTask = Task.Factory.StartNew(() =>
            {
                Locator.Current.GetService<ProjectManager>()!.OpenedProject!.PackChunk(GetFirstChild().URI);
            });
            await buildTask;
            projectManager.WorkableProject = true;
        }
        catch (Exception e)
        {
            Locator.Current.GetService<ProjectManager>()!.WorkableProject = true;
            Log.WriteLine($"Error when building chunk: {e.Message}");
        }
    }
}