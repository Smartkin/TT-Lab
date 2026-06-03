using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Attributes.Viewport;
using TT_Lab.Project;
using TT_Lab.Rendering;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Assets;

[SupportsViewport]
public class LevelChunk : SerializableAsset
{
    protected override String SavePathInPackage => string.IsNullOrEmpty(AdditionalPath) ? "levels" : $"{AdditionalPath}";
    
    public override string IconPath => "Scene.png";

    [JsonProperty(Required = Required.Always, ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Document)]
    [EditorParam(UriLinkViewModel.OpenInInspector, true)]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<LabURI> ChunkResources { get; set; } = [];

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, -4)]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Skydome))]
    [EditorHiddenIn("default", false)]
    public LabURI Skydome { get; set; } = LabURI.Empty;

    public LevelChunk()
    {
        SkipExport = false;
    }

    public LevelChunk(LabURI package, String name) : base((uint)Guid.NewGuid().GetHashCode(), name, package, false, "")
    {
        SkipExport = false;
    }

    public string GetChunkPath() => SavePathInPackage;

    public override void Dispose()
    {
        var assetManager = AssetManager.Get();
        foreach (var chunkResource in ChunkResources)
        {
            assetManager.GetAsset(chunkResource).Dispose();
        }
        
        base.Dispose();
    }

    public override void Save()
    {
        var assetManager = AssetManager.Get();
        var allCurrentAssets = ChunkResources.Select(assetManager.GetAsset).ToList();
        var chunkFolder = GetChunkFolder();
        foreach (var asset in chunkFolder.Children.Select(assetManager.GetAsset))
        {
            if (asset == this)
            {
                continue;
            }
            
            asset.Delete();
        }
        
        base.Save();
        foreach (var asset in allCurrentAssets)
        {
            if (!assetManager.DoesAssetExist(asset.URI))
            {
                assetManager.AddAsset(asset);
            }
            asset.Save();
        }
    }

    public override void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
    {
        if (serializationFlags.HasFlag(SerializationFlags.SetDirectoryToAssets))
        {
            Directory.SetCurrentDirectory($"{Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath}/assets");
        }
        
        var path = SavePath;
        Directory.CreateDirectory(path);
        
        using FileStream fs = new(Path.Combine(path, $"{Name}.json"), FileMode.Create, FileAccess.Write);
        using BinaryWriter writer = new(fs);
        writer.Write(JsonConvert.SerializeObject(this, Formatting.Indented).ToCharArray());
        writer.Flush();
    }

    public Folder GetChunkFolder()
    {
        var assetManager = AssetManager.Get();
        var packageFolderUri = assetManager.GetAsset<Package>(Package).GetFolderUri();
        return AssetManager.Get().GetAsset<Folder>(new LabURI($"{packageFolderUri}/{GetChunkPath().Replace('\\', '/')}"));
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var assetManager = AssetManager.Get();
        var result = new List<ViewportObject>();
        if (Skydome != LabURI.Empty)
        {
            var skydomeData = assetManager.GetAssetData(Skydome);
            var skydomeProp = property.Find(nameof(Skydome))!;
            result.AddRange(skydomeData.GetViewportObjects(viewportContext, skydomeProp));
        }

        var chunkResourceList = property.Find(nameof(ChunkResources))!;
        var idx = 0;
        foreach (var data in ChunkResources.Select(assetManager.GetAssetData))
        {
            var dataDoc = chunkResourceList.Find($"[{idx}]");
            if (dataDoc is not null)
            {
                result.AddRange(data.GetViewportObjects(viewportContext, dataDoc));
            }

            idx++;
        }
        
        return result;
    }

    public override AbstractAssetData GetData()
    {
        return new DummyData(this);
    }

    public override void Import()
    {
    }

    public override uint Section { get; }

    public override Type GetEditorType()
    {
        return typeof(ChunkEditorViewModel);
    }

    protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new ChunkElementViewModel(URI, parent);
    }
}