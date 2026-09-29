using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using SharpHash.Checksum;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Attributes;
using TT_Lab.Project;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.Assets;

public abstract class SerializableAsset : IAsset
{
    private UInt32 _id;
    private AbstractAssetData? _assetData;
    
    public virtual String SavePath => $"{Package.GetPackageName()}/{SavePathInPackage}";
    protected virtual String SavePathInPackage => string.IsNullOrEmpty(AdditionalPath) ? $"{Type.Name}" : $"{AdditionalPath}/{Type.Name}";
    protected virtual String DataExt => ".data";
    protected virtual String TwinDataExt => "bin";
    protected virtual Boolean SetIdFromDataHash => false;

    protected virtual String LoadPath => Path.Combine("assets", Package.GetPackageName(), URI.GetFilePathInPackage().Replace('/', Path.DirectorySeparatorChar));
    protected String DataLoadPath => Path.Combine(LoadPath, Data);
    protected ResourceTreeElementViewModel? ViewModel;
    
    public abstract UInt32 Section { get; }

    public Type Type { get; set; }
    public String InvariantName { get; set; }
    public String Name => string.IsNullOrEmpty(Variation) ? InvariantName : $"{InvariantName}_{Variation}";

    public string DocumentName => Alias;
    public virtual void Save()
    {
        // Open editors keep working on this data after saving, it gets released once no editor uses it anymore
        Serialize(SerializationFlags.SaveData | SerializationFlags.SetDirectoryToAssets | SerializationFlags.PreserveData | SerializationFlags.FixReferences);
    }

    public Boolean Raw { get; set; }
    public virtual String IconPath => "Common_Node.png";
    public virtual Boolean SupportsViewport => GetType().GetCustomAttribute<SupportsViewportAttribute>() != null;
    public String Data => $"{Name}{DataExt}";
    public bool MarkedForDeletion { get; private set; }
    public String? AdditionalPath { get; set; }
    public String FullDataPath => $"{Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath}/{DataLoadPath}";
    public String FullPath => $"{Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath}/{LoadPath}";
    public UInt32 ID { get; set; }
    public string HashSalt { get; set; } = string.Empty;
    // Assets identified by their data's hash have the ID of the chunk's view when the chunk being built has values of its own
    public UInt32 ExportTwinID => SetIdFromDataHash && OverriddenAsset == null && Factory.ChunkOverrides.Current?.GetView(this) is { } view
        ? view.ExportTwinID
        : SetIdFromDataHash ? GetDataHash() : ID;
    
    [Editable]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, -5)]
    public String Alias { get; set; }
    
    [Editable]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, Int32.MaxValue)]
    [EditorParam(DocumentCompositeViewModel.EditorInline, true)]
    public AbstractAssetData? AssetData
    {
        // Builds work on their scope's data, or on the asset's own when an editor has it loaded
        get => (IsInternal ? null : AssetDataScope.Current?.GetData(this)) ?? _assetData;
        set => SetData(value);
    }

    public String Chunk { get; set; }
    [Editable(EditorDescType = typeof(LayoutEditorDesc))]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, -4)]
    public Int32? LayoutID { get; set; }
    public Boolean IsLoaded => AssetData is { Disposed: false };
    public bool IsInternal { get; set; } = false;
    public IAsset? InternalOwner { get; set; }
    public Boolean SkipExport { get; set; } = false;

    public Dictionary<String, Object?> Parameters { get; set; } = new();

    /// <summary>
    /// The asset a chunk's view is of, views have the asset's URI and data file and are never saved as themselves (<see cref="AssetOverrides.CreateView"/>)
    /// </summary>
    public IAsset? OverriddenAsset { get; internal set; }

    public LabURI URI { get; set; }
    public LabURI Package { get; set; }

    // Made in TT Lab and not written yet, like an instance placed in a chunk before the chunk gets saved: the project tree keeps it
    // while it has no file
    public bool IsUnsaved { get; internal set; }
    public List<LabURI> References { get; set; } = [];
    public String Variation { get; set; }

    private bool _hasHashCache = false;
    private UInt32 _hashCache = 0U;

    protected SerializableAsset()
    {
        Raw = true;
        Type = GetType();
    }

    private SerializableAsset(UInt32 id, String name)
    {
        ID = id;
        InvariantName = name;
        Alias = Name;
        Raw = true;
        Type = GetType();
    }

    protected SerializableAsset(UInt32 id, String name, LabURI package, Boolean needVariant, String variant) : this(id, name)
    {
        Package = package;
        Variation = needVariant ? variant.Replace('\\', '_').Replace('/', '_') : string.Empty;
        Alias = Name;
    }

    public virtual void RegenerateUri()
    {
        URI = new LabURI($"{Package}/{SavePathInPackage.Replace('\\', '/')}/{Name}");
    }

    public void RegenerateLinks()
    {
        RegenerateUri();
    }

    public UInt32 GetDataHash()
    {
        if (_hasHashCache)
        {
            return _hashCache;
        }
        
        var hashResult = 0U;
        var crcHasher = SharpHash.Base.HashFactory.Checksum.CreateCRC(CRCStandard.CRC32);
        if (IsLoaded && AssetData != null)
        {
            hashResult = crcHasher.ComputeString(AssetData.GetStringified() + HashSalt, new UTF8Encoding()).GetUInt32();
        }
        else
        {
            using var fs = new FileStream(FullDataPath, FileMode.Open, FileAccess.Read);
            hashResult = crcHasher.ComputeStream(fs).GetUInt32();
        }

        _hashCache = hashResult;
        _hasHashCache = true;

        return hashResult;
    }

    public virtual void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
    {
        // Views share the file of the asset they're of
        if (OverriddenAsset != null)
        {
            return;
        }

        var path = FullPath;
        Directory.CreateDirectory(path);
        
        // Created or loaded data needs to be saved on disk but then disposed of since we are not going to need it
        // unless user wishes to edit the exact asset
        if (IsLoaded && serializationFlags.HasFlag(SerializationFlags.SaveData))
        {
            AssetData!.Save(Path.Combine(path, Data));
            if (serializationFlags.HasFlag(SerializationFlags.FixReferences))
            {
                References.Clear();
                ExtractReferences(GetData());
            }

            if (!serializationFlags.HasFlag(SerializationFlags.PreserveData))
            {
                DisposeData();
            }
        }
        
        if (!serializationFlags.HasFlag(SerializationFlags.SaveData) && serializationFlags.HasFlag(SerializationFlags.FixReferences))
        {
            References.Clear();
            ExtractReferences(GetData());
            DisposeData();
        }

        if (MarkedForDeletion)
        {
            return;
        }
        
        using (FileStream fs = new(Path.Combine(path, $"{Name}.json"), FileMode.Create, FileAccess.Write))
        using (BinaryWriter writer = new(fs))
        {
            writer.Write(JsonConvert.SerializeObject(this, Formatting.Indented).ToCharArray());
        }

        IsUnsaved = false;
    }

    public virtual void Deserialize(String json)
    {
        JsonConvert.PopulateObject(json, this);
    }

    public virtual void PostDeserialize() { }
        
    public virtual void Delete()
    {
        if (MarkedForDeletion)
        {
            return;
        }
        
        MarkedForDeletion = true;
        DisposeData(true);
        AssetManager.Get().RemoveAsset(this);

        // Internal assets are stored within their owner's data
        if (IsInternal)
        {
            return;
        }

        var jsonPath = Path.Combine(FullPath, $"{Name}.json");
        if (File.Exists(jsonPath))
        {
            File.Delete(jsonPath);
        }

        if (File.Exists(FullDataPath))
        {
            File.Delete(FullDataPath);
        }
    }

    public virtual List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var result = new List<ViewportObject>();
        if (_assetData != null)
        {
            result.AddRange(_assetData.GetViewportObjects(viewportContext, property));
        }

        return result;
    }

    public abstract Type GetEditorType();
    public abstract AbstractAssetData GetData();

    public virtual void SetData(AbstractAssetData data)
    {
        if (data is null || data == AssetData)
        {
            return;
        }

        if (!IsInternal && AssetDataScope.Current is { } scope)
        {
            scope.SetData(this, data);
            return;
        }
        
        DisposeData(true);
        _assetData = data;
        if (IsInternal)
        {
            InvariantName += $"_{GetDataHash():X}";
        }
    }

    public void UnloadData()
    {
        // Internal assets and ones never saved have no data file of their own to load it back from
        if (IsInternal || IsUnsaved)
        {
            return;
        }

        _assetData = null;
    }

    public virtual void Import()
    {
        AssetData!.Import(Package, Variation, LayoutID);
        ExtractReferences(AssetData);
        AssetData.NullifyReference();
    }

    public virtual ITwinItem Export(Factory.ITwinItemFactory factory)
    {
        if (!IsLoaded)
        {
            AssetData = GetData();
        }
        var item = AssetData!.Export(factory);
        DisposeData();
        return item;
    }

    public virtual void ExportToFile(Factory.ITwinItemFactory factory)
    {
        PreResolveResources();
        var item = Export(factory);
        using var itemFile = new FileStream(ExportFileName, FileMode.Create, FileAccess.Write);
        using var binaryWriter = new BinaryWriter(itemFile);
        item.Write(binaryWriter);
        binaryWriter.Flush();
        binaryWriter.Close();
    }

    public string ExportFileName => $"{InvariantName}.{TwinDataExt}";

    public virtual void PreResolveResources() { }

    public virtual void PostResolveResources(Factory.ITwinItemFactory factory, ITwinSection section, ITwinItem? item)
    {
        var exportId = ExportTwinID;
        // HACK: Default.rm2 Meshes have hardcoded IDs >:(
        if (section.GetParent() == null && item is ITwinMesh)
        {
            exportId = ID;
        }
        item?.SetID(exportId);
        item?.Compile();
    }

    public virtual void Dispose()
    {
        DisposeData();
    }

    // Creating a project keeps internal assets' data in a file once no write uses it and loads it back from there when a write needs it
    // again (CreationWriter). Setting an internal asset's data the usual way names it after the data, which would move its file
    internal void KeepInternalData(string path)
    {
        AssetData!.Save(path);
        DisposeData(true);
    }

    internal void DisposeInternalData()
    {
        DisposeData(true);
    }

    internal void LoadKeptData(Type dataType, string path)
    {
        var data = (AbstractAssetData)Activator.CreateInstance(dataType, this)!;
        data.Load(path);
        _assetData = data;
    }

    protected void DisposeData(bool force = false)
    {
        if (IsInternal && !force)
        {
            return;
        }

        // A build only releases what its scope loaded, the asset's own data is an editor's
        if (!IsInternal && AssetDataScope.Current is { } scope)
        {
            scope.ReleaseData(this);
            return;
        }
        
        // The field is cleared directly because the AssetData setter ignores nulls, which left every disposed data referenced by its asset
        if (!IsLoaded)
        {
            _assetData = null;
            return;
        }

        AssetData?.Dispose();
        _assetData = null;
    }

    public virtual void ResolveChunkResources(Factory.ITwinItemFactory factory, ITwinSection section)
    {
        if (OverrideViewOf(factory) is { } view)
        {
            view.ResolveChunkResources(factory, section);
            return;
        }

        if (ChunkVersionOf(factory) is { } version)
        {
            version.ResolveChunkResources(factory, section);
            return;
        }

        if (!factory.Resolution.Begin(this, section))
        {
            return;
        }

        try
        {
            var data = GetData();
            PreResolveResources();
            var item = data.ResolveChunkResources(factory, section, ExportTwinID, LayoutID);
            PostResolveResources(factory, section, item);
            section.RemoveDuplicates(ExportTwinID);
            DisposeData();
        }
        finally
        {
            factory.Resolution.End(this, section);
        }
    }

    /// <summary>
    /// The asset with the chunk being built's own values when it has some
    /// </summary>
    protected IAsset? OverrideViewOf(Factory.ITwinItemFactory factory)
    {
        return OverriddenAsset == null ? factory.Overrides?.GetView(this) : null;
    }

    internal void SetViewData(AbstractAssetData data)
    {
        _assetData = data;
    }

    /// <summary>
    /// The chunk being built's own version of the item when it differs from this one
    /// </summary>
    protected IAsset? ChunkVersionOf(Factory.ITwinItemFactory factory)
    {
        if (factory.ChunkVersions == null || !factory.ChunkVersions.TryGetValue((GetType(), ID), out var version) || version == URI)
        {
            return null;
        }

        var assetManager = AssetManager.Get();
        return assetManager.DoesAssetExist(version) ? assetManager.GetAsset(version) : null;
    }

    public virtual bool IsReferencingAny(IReadOnlySet<LabURI> assets)
    {
        return References.Any(assets.Contains);
    }

    public virtual void FixDeletedReferences(DeletedReferenceFixer fixer)
    {
        fixer.FixObject(GetData());
    }

    // Only the row the tree has, none gets made for it
    internal void RemoveFromTree()
    {
        if (ViewModel?.Parent is not { } parent)
        {
            return;
        }

        parent.RemoveChild(ViewModel);
        parent.ClearChildren();
        parent.LoadChildrenBack();
        parent.NotifyOfPropertyChange(nameof(parent.Children));
    }

    public ResourceTreeElementViewModel GetResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        if (ViewModel != null)
        {
            return ViewModel;
        }
            
        ViewModel = CreateResourceTreeElement(parent);
        ViewModel.Init();

        return ViewModel;
    }

    protected virtual ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new ResourceTreeElementViewModel(URI, parent);
    }

    private void ExtractReferences(object? data)
    {
        if (data?.GetType().GetCustomAttribute<ReferencesAssetsAttribute>() is null)
        {
            return;
        }
            
        var props = data.GetType().GetProperties();
        foreach (var prop in props)
        {
            if (prop.PropertyType.IsAssignableTo(typeof(LabURI)))
            {
                var uri = prop.GetValue(data) as LabURI;
                if (uri != null)
                {
                    ((IAsset)this).AddReference(uri);
                }
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(IEnumerable<LabURI>)))
            {
                if (prop.GetValue(data) is not IEnumerable<LabURI> refList)
                {
                    continue;
                }
                    
                foreach (var @ref in refList)
                {
                    ((IAsset)this).AddReference(@ref);
                }
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(IEnumerable)))
            {
                if (prop.GetValue(data) is not IEnumerable list)
                {
                    continue;
                }

                foreach (var item in list)
                {
                    ExtractReferences(item);
                }
            }
            else
            {
                ExtractReferences(prop.GetValue(data));
            }
        }
    }
}