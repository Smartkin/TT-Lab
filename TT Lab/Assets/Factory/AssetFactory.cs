using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TT_Lab.Services;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Assets.Factory;

public static class AssetFactory
{
    public static IAsset? CreateAsset(Type type, Folder folder, string name, string variation, ITwinIdGeneratorService idGenerator, Func<IAsset, AssetCreationStatus>? dataCreator = null, Enums.Layouts? layout = null)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"The type {type.Name} must implement IAsset");
        var newAsset = (IAsset)Activator.CreateInstance(type)!;
        newAsset.InvariantName = name;
        newAsset.Alias = name;
        newAsset.Package = folder.Package;
        newAsset.Variation = variation;
        newAsset.ID = idGenerator.GenerateTwinId();
        if (layout.HasValue)
        {
            newAsset.LayoutID = (int)layout.Value;
        }
        
        newAsset.RegenerateLinks();
        
        var dataCreationResult = dataCreator?.Invoke(newAsset);
        if (dataCreationResult is AssetCreationStatus.Failed)
        {
            return null;
        }
        
        AssetManager.Get().AddAsset(newAsset);
        var containingFolder = GetContainingFolder(folder, newAsset);
        containingFolder.AddChild(newAsset);
        if (type != typeof(Folder) && layout is null)
        {
            newAsset.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        }

        var parent = folder.GetResourceTreeElement();
        IAsset treeAsset = containingFolder == folder ? newAsset : containingFolder;
        parent.AddNewChild(treeAsset.GetResourceTreeElement(parent));
        parent.ClearChildren();
        parent.LoadChildrenBack();
        parent.NotifyOfPropertyChange(nameof(parent.Children));
        
        return newAsset;
    }
    
    // Chunks live in their own folder named after them, the same way the project tree gets built from the disk
    private static Folder GetContainingFolder(Folder folder, IAsset asset)
    {
        if (asset is not LevelChunk)
        {
            return folder;
        }

        var chunkFolder = new Folder(asset.Name)
        {
            Parent = folder.URI,
            Package = folder.Package,
            Mark = FolderMark.Normal | FolderMark.IsChunk
        };
        AssetManager.Get().AddAsset(chunkFolder);
        folder.AddChild(chunkFolder);
        return chunkFolder;
    }
    
    public static async Task<IAsset?> CreateAsset(Type type, Folder folder, string name, string variation, ITwinIdGeneratorService idGenerator, Func<IAsset, Task<AssetCreationStatus>>? dataCreator = null, Enums.Layouts? layout = null)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"The type {type.Name} must implement IAsset");
        var newAsset = (IAsset)Activator.CreateInstance(type)!;
        newAsset.InvariantName = name;
        newAsset.Alias = name;
        newAsset.Package = folder.Package;
        newAsset.Variation = variation;
        newAsset.ID = idGenerator.GenerateTwinId();
        if (layout.HasValue)
        {
            newAsset.LayoutID = (int)layout.Value;
        }
        
        newAsset.RegenerateLinks();

        if (dataCreator != null)
        {
            var dataCreationResult = await dataCreator.Invoke(newAsset);
            if (dataCreationResult is AssetCreationStatus.Failed)
            {
                return null;
            }
        }

        folder.AddChild(newAsset);
        AssetManager.Get().AddAsset(newAsset);
        newAsset.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        folder.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData | SerializationFlags.FixReferences);
        
        var parent = folder.GetResourceTreeElement();
        parent.AddNewChild(newAsset.GetResourceTreeElement(parent));
        parent.ClearChildren();
        parent.LoadChildrenBack();
        parent.NotifyOfPropertyChange(nameof(parent.Children));
        
        return newAsset;
    }
}