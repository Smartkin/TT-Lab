using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets.Factory;
using TT_Lab.Project;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets;

[Flags]
public enum FolderMark
{
    Normal = 0x1,
    InChunk = 0x2,
    Locked = 0x4,
    ChunksOnly = 0x8,
    DefaultOnly = 0x10,
    IsPackage =  0x20,
    IsChunk = 0x40,
    // The disc's files, shown for the twinstudio tools to open
    Disc = 0x80,
}
    
public class Folder : SerializableAsset
{
    public override UInt32 Section => uint.MaxValue;
    public FolderMark Mark { get; set; } = FolderMark.Normal;
    public List<LabURI> Children { get; set; } = [];
    public LabURI Parent { get; set; } = LabURI.Empty;

    public override string IconPath => GetIconPath();

    public Folder()
    {
        SkipExport = true;
    }

    public Folder(string name)
    {
        InvariantName = name;
        Alias = InvariantName;
    }

    public void AddChild(LabURI uri)
    {
        Children.Add(uri);
    }

    public void AddChild(IAsset asset)
    {
        Children.Add(asset.URI);
    }

    // Folders are the project's directories, never files of their own
    public override void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
    {
    }

    public override void Delete()
    {
        AssetManager.Get().RemoveAsset(this);
        var projectPath = Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath;
        var directory = Path.Combine(projectPath, GetPath().TrimStart('/'));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }

    public string GetPath()
    {
        var result = "";
        if (Parent == LabURI.Empty)
        {
            return $"{result}/{Alias}";
        }
        
        var parentFolder = AssetManager.Get().GetAsset<Folder>(Parent);
        result += parentFolder.GetPath();

        return $"{result}/{Alias}";
    }

    public const string LevelsFolderName = "levels";

    // Chunks only go in the levels folder at the root of a package and the folders under it, the build writes that tree as the game's
    // Levels folder. The folders from the levels folder down to this one, null when it isn't in one
    public List<string>? GetPathInLevels()
    {
        var assetManager = AssetManager.Get();
        var path = new List<string>();
        var folder = this;
        while (!folder.Mark.HasFlag(FolderMark.IsPackage))
        {
            if (folder.Mark.HasFlag(FolderMark.IsChunk) || folder.Parent == LabURI.Empty || !assetManager.DoesAssetExist(folder.Parent))
            {
                return null;
            }

            path.Insert(0, folder.Alias);
            folder = assetManager.GetAsset<Folder>(folder.Parent);
        }

        return path.Count > 0 && path[0] == LevelsFolderName ? path : null;
    }

    public T FindAndGetChild<T>(string name) where T : IAsset
    {
        return AssetManager.Get().GetAsset<T>(FindChild<T>(name));
    }

    public LabURI FindChild<T>(string name) where T : IAsset
    {
        var result = LabURI.Empty;
        var assetManager = AssetManager.Get();
        foreach (var child in Children)
        {
            var asset = assetManager.GetAsset(child);
            if (asset.Name != name || asset is not T)
            {
                continue;
            }
            
            result = child;
            break;
        }

        if (result != LabURI.Empty)
        {
            return result;
        }
        
        foreach (var child in Children)
        {
            if (assetManager.GetAsset(child) is not Folder childFolder)
            {
                continue;
            }

            result = childFolder.FindChild<T>(name);
            if (result != LabURI.Empty)
            {
                break;
            }
        }

        return result;
        
    }

    public LabURI FindChild(string name)
    {
        return FindChild<IAsset>(name);
    }

    public override void RegenerateUri()
    {
        URI = new LabURI($"res://__GLOBAL_FOLDER__{GetPath()}");
    }

    public override Type GetEditorType()
    {
        throw new NotSupportedException();
    }

    public override AbstractAssetData GetData()
    {
        return new DummyData(this);
    }

    public override void ResolveChunkResources(ITwinItemFactory factory, ITwinSection section)
    {
        var assetManager = AssetManager.Get();
        foreach (var item in Children)
        {
            assetManager.GetAsset(item).ResolveChunkResources(factory, section);
        }
    }

    protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new FolderElementViewModel(URI, parent);
    }

    private string GetIconPath()
    {
        if (Mark.HasFlag(FolderMark.IsChunk))
        {
            return "Scene.png";
        }

        if (Mark.HasFlag(FolderMark.IsPackage))
        {
            return "Package.png";
        }

        return "Folder.png";
    }
}