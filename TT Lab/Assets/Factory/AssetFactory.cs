using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Splat;
using TT_Lab.Project;
using TT_Lab.Services;
using TT_Lab.Util;
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
        newAsset.FolderInPackage = FolderInPackageOf(newAsset, folder);
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
        var (containingFolder, createdFolder, createdIn) = GetContainingFolder(folder, newAsset);
        // A package's file marks its folder, the folder doesn't list it
        if (newAsset is not Package)
        {
            containingFolder.AddChild(newAsset);
        }

        if (type != typeof(Folder) && layout is null)
        {
            newAsset.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        }
        else if (type != typeof(Folder) && newAsset is SerializableAsset unsaved)
        {
            // A layout's instance is written when the chunk it was made for gets saved
            unsaved.IsUnsaved = true;
        }

        // The tree shows the new asset, or the first folder made for it
        var parent = (createdIn ?? containingFolder).GetResourceTreeElement();
        IAsset treeAsset = createdFolder ?? newAsset;
        parent.AddNewChild(treeAsset.GetResourceTreeElement(parent));
        
        return newAsset;
    }

    // An asset made in a folder of its package has its files in it, unless its type decides where they go (chunks, instances, packages,
    // global files) or the folder is where they'd go anyway: the package's own folder or its type's. Every asset went into its type's
    // folder, the tree then found it there
    private static string? FolderInPackageOf(IAsset asset, Folder folder)
    {
        var savePath = asset.GetType().GetProperty("SavePathInPackage", BindingFlags.Instance | BindingFlags.NonPublic);
        if (asset is Folder || savePath?.DeclaringType != typeof(SerializableAsset))
        {
            return null;
        }

        var assetManager = AssetManager.Get();
        var names = new List<string>();
        for (var current = folder; !current.Mark.HasFlag(FolderMark.IsPackage); current = assetManager.GetAsset<Folder>(current.Parent))
        {
            if (current.Parent == LabURI.Empty || !assetManager.DoesAssetExist(current.Parent))
            {
                return null;
            }

            names.Insert(0, current.Alias);
        }

        var path = string.Join('/', names);
        return path.Length == 0 || path == asset.GetType().Name ? null : path;
    }
    
    // A new asset is listed in the folder of the directory its file is written to, the same way the project tree gets built from the
    // disk: chunks get a folder of their own, a layout's instances the folders of their type and layout in their chunk's. Instances
    // used to be listed in the chunk's folder, and the tree following the file system took them out of the project as missing files
    private static (Folder Containing, Folder? Created, Folder? CreatedIn) GetContainingFolder(Folder folder, IAsset asset)
    {
        if (asset is Package package)
        {
            var packageFolder = new Folder(package.Name)
            {
                Parent = folder.URI,
                Package = package.URI,
                Mark = FolderMark.IsPackage | FolderMark.Locked
            };
            AssetManager.Get().AddAsset(packageFolder);
            folder.AddChild(packageFolder);
            return (packageFolder, packageFolder, folder);
        }

        if (asset is LevelChunk)
        {
            var chunkFolder = new Folder(asset.Name)
            {
                Parent = folder.URI,
                Package = folder.Package,
                Mark = FolderMark.Normal | FolderMark.IsChunk
            };
            AssetManager.Get().AddAsset(chunkFolder);
            folder.AddChild(chunkFolder);
            return (chunkFolder, chunkFolder, folder);
        }

        var projectPath = Locator.Current.GetService<ProjectManager>()?.OpenedProject?.ProjectPath;
        if (asset is Folder || asset is not SerializableAsset serializable || projectPath == null)
        {
            return (folder, null, null);
        }

        var relative = Path.GetRelativePath(Path.Combine(projectPath, folder.GetPath().TrimStart('/')), serializable.FullPath);
        if (relative == "." || relative.StartsWith("..") || Path.IsPathRooted(relative))
        {
            return (folder, null, null);
        }

        // The directories are made right away, the tree drops folders whose directory it can't find
        DirectoryCase.Create(serializable.FullPath);
        var assetManager = AssetManager.Get();
        var current = folder;
        Folder? created = null;
        Folder? createdIn = null;
        foreach (var name in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            var existing = current.Children.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset).OfType<Folder>().FirstOrDefault(child => child.Alias == name);
            if (existing == null)
            {
                existing = new Folder(name)
                {
                    Parent = current.URI,
                    Package = current.Package,
                    Mark = FolderMark.Normal
                };
                assetManager.AddAsset(existing);
                current.AddChild(existing);
                if (created == null)
                {
                    created = existing;
                    createdIn = current;
                }
            }

            current = existing;
        }

        return (current, created, createdIn);
    }
    
    public static async Task<IAsset?> CreateAsset(Type type, Folder folder, string name, string variation, ITwinIdGeneratorService idGenerator, Func<IAsset, Task<AssetCreationStatus>>? dataCreator = null, Enums.Layouts? layout = null)
    {
        Debug.Assert(type.IsAssignableTo(typeof(IAsset)), $"The type {type.Name} must implement IAsset");
        var newAsset = (IAsset)Activator.CreateInstance(type)!;
        newAsset.InvariantName = name;
        newAsset.Alias = name;
        newAsset.Package = folder.Package;
        newAsset.Variation = variation;
        newAsset.FolderInPackage = FolderInPackageOf(newAsset, folder);
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

        AssetManager.Get().AddAsset(newAsset);
        var (containingFolder, createdFolder, createdIn) = GetContainingFolder(folder, newAsset);
        // A package's file marks its folder, the folder doesn't list it
        if (newAsset is not Package)
        {
            containingFolder.AddChild(newAsset);
        }

        newAsset.Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData);
        
        var parent = (createdIn ?? containingFolder).GetResourceTreeElement();
        IAsset treeAsset = createdFolder ?? newAsset;
        parent.AddNewChild(treeAsset.GetResourceTreeElement(parent));
        
        return newAsset;
    }
}