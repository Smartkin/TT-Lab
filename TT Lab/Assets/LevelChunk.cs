using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caliburn.Micro;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Assets.Graphics;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Attributes.Viewport;
using TT_Lab.Project;
using TT_Lab.Rendering;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Assets;

[SupportsViewport]
public class LevelChunk : SerializableAsset
{
    protected override String SavePathInPackage => string.IsNullOrEmpty(AdditionalPath) ? "levels" : $"{AdditionalPath}";
    
    public override string IconPath => "Scene.png";


    // Shown like the project tree, a folder for each kind of resource
    [JsonProperty(Required = Required.Always, ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [Editable(Caption = "Resources", EditorDescType = typeof(ChunkResourcesEditorDesc), EditorOrientation = Avalonia.Controls.Dock.Top)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> ChunkResources { get; set; } = [];

    // The chunk's versions of the objects and sounds that differ between chunks, the build puts them in whatever references them
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> ItemVersions { get; set; } = [];

    // The chunk's own values of assets it shares with other chunks, applied when it gets built
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<AssetOverride> Overrides { get; set; } = [];

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, -4)]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Skydome))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
    [EditorHiddenWhen(nameof(IsGlobalDefaultChunk))]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
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

    public bool IsGlobalDefaultChunk
    {
        get
        {
            var project = Locator.Current.GetService<ProjectManager>()?.OpenedProject;
            if (project == null || !Name.Equals("default", StringComparison.InvariantCultureIgnoreCase))
            {
                return false;
            }

            return Package == project.GlobalPackagePS2.URI || Package == project.GlobalPackageXbox.URI;
        }
    }

    public override void Dispose()
    {
        var assetManager = AssetManager.Get();
        foreach (var chunkResource in ChunkResources)
        {
            // Closing the chunk's tab disposes what it lists, one that isn't in the project anymore has nothing to release
            if (!assetManager.DoesAssetExist(chunkResource))
            {
                Log.WriteLine($"{Name} lists {chunkResource}, which isn't in the project", Log.LogType.Warning);
                continue;
            }

            assetManager.GetAsset(chunkResource).Dispose();
        }
        
        base.Dispose();
    }

    // Chunk's resources are kept by the chunk itself rather than its data
    public override bool IsReferencingAny(IReadOnlySet<LabURI> assets)
    {
        return base.IsReferencingAny(assets) || assets.Contains(Skydome) || ChunkResources.Any(assets.Contains) || ItemVersions.Any(assets.Contains)
               || Overrides.Any(@override => assets.Contains(@override.Asset) || @override.GetLinkedAssets().Any(assets.Contains));
    }

    public override void FixDeletedReferences(DeletedReferenceFixer fixer)
    {
        base.FixDeletedReferences(fixer);
        fixer.FixProperties(this, nameof(ChunkResources), nameof(Skydome), nameof(ItemVersions), nameof(Overrides));
        Overrides.RemoveAll(@override => @override.Asset == LabURI.Empty);
        FixOverrideValues(fixer);
    }

    // The chunk's own values are fixed the way the asset's are, each member of its data the way it's marked, and they stay where they
    // still differ from the asset's. Fixing a copy of the asset as well makes it not matter whether the asset got fixed already
    private void FixOverrideValues(DeletedReferenceFixer fixer)
    {
        var assetManager = AssetManager.Get();
        foreach (var @override in Overrides.Where(@override => @override.GetLinkedAssets().Any(fixer.IsDeleted)).ToList())
        {
            var asset = assetManager.GetAsset(@override.Asset);
            var view = AssetOverrides.CreateView(asset, @override.Values);
            var shared = AssetOverrides.CreateView(asset, new Dictionary<string, JToken>());
            view.FixDeletedReferences(fixer);
            shared.FixDeletedReferences(fixer);
            if (!fixer.IsDryRun)
            {
                @override.Values = AssetOverrides.Diff(AssetOverrides.GetDocument(shared, shared.GetData()), AssetOverrides.GetDocument(view, view.GetData()));
            }
        }

        Overrides.RemoveAll(@override => @override.Values.Count == 0);
    }

    public AssetOverride? GetOverride(LabURI asset) => Overrides.FirstOrDefault(@override => @override.Asset == asset);

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
            
            //asset.Delete();
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
        // By its URI like every asset: the chunk's path has the separators of the system the project was made on, a Windows path is the
        // name of one folder on Linux, and its folder can be named in another case (the default chunk's in a project made on Windows)
        var path = DirectoryCase.Create(FullPath);

        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        using FileStream fs = new(Path.Combine(path, $"{Name}.json"), FileMode.Create, FileAccess.Write);
        using BinaryWriter writer = new(fs);
        writer.Write(json.ToCharArray());
        writer.Flush();
    }

    public Folder GetChunkFolder()
    {
        var assetManager = AssetManager.Get();
        var packageFolder = assetManager.GetAsset<Package>(Package).GetPackageFolder();
        var path = GetChunkPath().Replace('\\', '/');
        var uri = new LabURI($"{packageFolder.URI}/{path}");
        if (assetManager.DoesAssetExist(uri))
        {
            return assetManager.GetAsset<Folder>(uri);
        }

        // Folders are named after their directories, which can be named in another case than the chunk's path (DirectoryCase)
        var folder = packageFolder;
        foreach (var name in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var children = folder.Children.Where(assetManager.DoesAssetExist).Select(assetManager.GetAsset).OfType<Folder>().ToList();
            folder = children.FirstOrDefault(child => child.Alias == name) ?? children.FirstOrDefault(child => child.Alias.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"The folder of the chunk {path} isn't in the project");
        }

        return folder;
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var assetManager = AssetManager.Get();
        var result = new List<ViewportObject>();
        var total = ChunkResources.Count + (Skydome != LabURI.Empty ? 1 : 0);
        var done = 0;
        if (Skydome != LabURI.Empty)
        {
            viewportContext.Progress?.Invoke("Skydome", done++, total);
            var skydomeData = assetManager.GetAssetData(Skydome);
            var skydomeProp = property.Find(nameof(Skydome))!;
            result.AddRange(skydomeData.GetViewportObjects(viewportContext, skydomeProp));
        }

        var chunkResourceList = property.Find(nameof(ChunkResources))!;
        var idx = 0;
        foreach (var uri in ChunkResources)
        {
            var asset = assetManager.GetAsset(uri);
            // Named like the Chunk Resources panel's folders
            viewportContext.Progress?.Invoke($"{ChunkResourcesTreeViewModel.KindName(asset.GetType())}: {asset.Alias}", done++, total);
            var data = assetManager.GetAssetData(uri);
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