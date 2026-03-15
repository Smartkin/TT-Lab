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

    protected String LoadPath => Path.Combine("assets", Package.GetPackageName(), URI.GetFilePathInPackage().Replace('/', Path.DirectorySeparatorChar));
    protected String DataLoadPath => Path.Combine(LoadPath, Data);
    protected ResourceTreeElementViewModel? ViewModel;
    
    public abstract UInt32 Section { get; }

    public Type Type { get; set; }
    public String InvariantName { get; set; }
    public String Name => string.IsNullOrEmpty(Variation) ? InvariantName : $"{InvariantName}_{Variation}";

    public string DocumentName => Alias;
    public void Save()
    {
        Serialize(SerializationFlags.SaveData | SerializationFlags.SetDirectoryToAssets);
    }

    public Boolean Raw { get; set; }
    public virtual String IconPath => "Common_Node.png";
    public String Data => $"{Name}{DataExt}";
    public bool MarkedForDeletion { get; private set; }
    public String? AdditionalPath { get; set; }
    public String FullDataPath => $"{Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath}/{DataLoadPath}";
    public String FullPath => $"{Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath}/{LoadPath}";
    public UInt32 ID { get; set; }
    public string HashSalt { get; set; } = string.Empty;
    public UInt32 ExportTwinID => SetIdFromDataHash ? GetDataHash() : ID;
    
    [Editable]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, -5)]
    public String Alias { get; set; }
    
    [Editable]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, Int32.MaxValue)]
    public AbstractAssetData? AssetData
    {
        get => _assetData;
        set => SetData(value);
    }
    
    public String Chunk { get; set; }
    public Int32? LayoutID { get; set; }
    public Boolean IsLoaded => AssetData is { Disposed: false };
    public bool IsInternal { get; set; } = false;
    public Boolean SkipExport { get; set; } = false;

    public Dictionary<String, Object?> Parameters { get; set; } = new();
    public LabURI URI { get; set; }
    public LabURI Package { get; set; }
    public List<LabURI> References { get; set; } = [];
    public String Variation { get; set; }

    private bool _resolveTraversed = false;
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
        
        using FileStream fs = new(Path.Combine(path, $"{Name}.json"), FileMode.Create, FileAccess.Write);
        using BinaryWriter writer = new(fs);
        writer.Write(JsonConvert.SerializeObject(this, Formatting.Indented).ToCharArray());
    }

    public virtual void Deserialize(String json)
    {
        JsonConvert.PopulateObject(json, this);
    }

    public virtual void PostDeserialize() { }
        
    public virtual void Delete(bool setDirectoryToAssets = false, bool deleteAllReferencedData = false)
    {
        if (MarkedForDeletion)
        {
            return;
        }
        
        MarkedForDeletion = true;

        var assetManager = AssetManager.Get();
        if (deleteAllReferencedData)
        {
            var refsCopy = References.ToList();
            foreach (var reference in refsCopy)
            {
                if (reference == LabURI.Empty)
                {
                    continue;
                }
                
                assetManager.GetAsset(reference).Delete(setDirectoryToAssets, deleteAllReferencedData);
            }
        }
        
        DisposeData(true);
        assetManager.RemoveAsset(this);
        
        if (setDirectoryToAssets)
        {
            Directory.SetCurrentDirectory($"{Locator.Current.GetService<ProjectManager>()!.OpenedProject!.ProjectPath}/assets");
        }

        var path = SavePath;
        if (File.Exists(Path.Combine(path, $"{Name}.json")))
        {
            File.Delete(Path.Combine(path, $"{Name}.json"));
        }

        if (File.Exists(Path.Combine(path, Data)))
        {
            File.Delete(Path.Combine(path, Data));
        }
    }

    public virtual List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property) => [];

    public abstract Type GetEditorType();
    public abstract AbstractAssetData GetData();

    public virtual void SetData(AbstractAssetData data)
    {
        if (data == _assetData)
        {
            return;
        }
        
        DisposeData(true);
        _assetData = data;
        if (IsInternal)
        {
            InvariantName += $"_{GetDataHash():X}";
        }
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
        using var itemFile = new FileStream($"{InvariantName}.{TwinDataExt}", FileMode.Create, FileAccess.Write);
        using var binaryWriter = new BinaryWriter(itemFile);
        item.Write(binaryWriter);
        binaryWriter.Flush();
        binaryWriter.Close();
    }

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

    protected void DisposeData(bool force = false)
    {
        if (IsInternal && !force)
        {
            return;
        }
        
        if (!IsLoaded)
        {
            AssetData = null;
            return;
        }
            
        AssetData?.Dispose();
        AssetData = null;
    }

    public virtual void ResolveChunkResources(Factory.ITwinItemFactory factory, ITwinSection section)
    {
        if (_resolveTraversed) return;

        _resolveTraversed = true;
        AssetData = GetData();
        PreResolveResources();
        var item = AssetData.ResolveChunkResources(factory, section, ExportTwinID, LayoutID);
        PostResolveResources(factory, section, item);
        section.RemoveDuplicates(ExportTwinID);

        DisposeData();
        _resolveTraversed = false;
    }

    public void RemoveReference(LabURI reference)
    {
        if (!References.Remove(reference))
        {
            return;
        }

        var serializationFlags = SerializationFlags.SetDirectoryToAssets | SerializationFlags.FixReferences;
        if (!IsInternal)
        {
            RemoveReferencesFromData(GetData(), reference);
            serializationFlags |= SerializationFlags.SaveData;
        }

        Serialize(serializationFlags);
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

    protected virtual LabURI GetDefaultReference() => LabURI.Empty;

    protected virtual ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new ResourceTreeElementViewModel(URI, parent);
    }

    private void RemoveReferencesFromData(object? data, LabURI reference)
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
                if (uri != null && uri == reference)
                {
                    prop.SetValue(data, GetDefaultReference());
                }
            }
            else if (prop.PropertyType.IsAssignableTo(typeof(IEnumerable<LabURI>)))
            {
                if (prop.GetValue(data) is not IEnumerable<LabURI> refList)
                {
                    continue;
                }
                    
                var list = refList.ToList();
                var referenceRemoved = false;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] != reference)
                    {
                        continue;
                    }
                    
                    list[i] = GetDefaultReference();
                    referenceRemoved = true;
                }

                if (referenceRemoved)
                {
                    prop.SetValue(data, list);
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
                    RemoveReferencesFromData(item, reference);
                }
            }
            else
            {
                RemoveReferencesFromData(prop.GetValue(data), reference);
            }
        }
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