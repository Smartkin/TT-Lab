using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TT_Lab.AssetData;
using TT_Lab.Assets.Factory;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets;

[Flags]
public enum FolderMark
{
    Normal = 0x0,
    InChunk = 0x1,
    Locked = 0x2,
    ChunksOnly = 0x4,
    DefaultOnly = 0x8,
    IsPackage =  0x10,
}
    
public class Folder : SerializableAsset
{
    public override UInt32 Section => uint.MaxValue;
    public FolderMark Mark { get; set; } = FolderMark.Normal;
    public List<LabURI> Children { get; set; } = [];
    public LabURI Parent { get; set; } = LabURI.Empty;

    public override string IconPath => Mark.HasFlag(FolderMark.IsPackage) ? "Package.png" : "Folder.png";

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

    public string GetPath()
    {
        var result = "";
        if (Parent == LabURI.Empty)
        {
            return $"{result}/{Name}";
        }
        
        var parentFolder = AssetManager.Get().GetAsset<Folder>(Parent);
        result += parentFolder.GetPath();

        return $"{result}/{Name}";
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
        throw new NotSupportedException();
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
}