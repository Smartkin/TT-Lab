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
    public LabURI? Parent { get; set; }

    public override string IconPath => Mark.HasFlag(FolderMark.IsPackage) ? "Package.png" : "Folder.png";

    public Folder()
    {
        SkipExport = true;
    }

    public Folder(string name)
    {
        InvariantName = name;
    }

    public void AddChild(IAsset asset)
    {
        Children.Add(asset.URI);
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