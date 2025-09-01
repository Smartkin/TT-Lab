using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Caliburn.Micro;
using TT_Lab.AssetData;
using TT_Lab.Attributes;
using TT_Lab.Project;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets;

public abstract class SerializableAsset : IAsset
{
    protected virtual String SavePath => $"{Package.GetPackageName()}\\{SavePathInPackage}";
    protected virtual String SavePathInPackage => string.IsNullOrEmpty(AdditionalPath) ? $"{Type.Name}" : $"{AdditionalPath}\\{Type.Name}";
    protected virtual String DataExt => ".data";
    protected virtual String TwinDataExt => "bin";
    protected AbstractAssetData? AssetData;
    protected ResourceTreeElementViewModel? ViewModel;
        
    public abstract UInt32 Section { get; }

    public Type Type { get; set; }
    public String InvariantName { get; set; }
    public String Name => string.IsNullOrEmpty(Variation) ? InvariantName : $"{InvariantName}_{Variation}";
    public Boolean Raw { get; set; }
    public virtual String IconPath => "Common_Node.png";
    public String Data => $"{Name}{DataExt}";
    public String? AdditionalPath { get; set; }
    public String FullDataPath => $"{IoC.Get<ProjectManager>().OpenedProject!.ProjectPath}\\assets\\{SavePath}\\{Data}";
    public UInt32 ID { get; set; }
    public String Alias { get; set; }
    public String Chunk { get; set; }
    public Int32? LayoutID { get; set; }
    public Boolean IsLoaded => AssetData is { Disposed: false };
    public UInt32 Order { get; set; }
    public Boolean SkipExport { get; set; } = false;

    public Dictionary<String, Object?> Parameters { get; set; } = new();
    public LabURI URI { get; set; }
    public LabURI Package { get; set; }
    public List<LabURI> References { get; set; } = [];
    public String Variation { get; set; }

    private bool _resolveTraversed = false;

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

    public virtual void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
    {
        if (serializationFlags.HasFlag(SerializationFlags.SetDirectoryToAssets))
        {
            Directory.SetCurrentDirectory($"{IoC.Get<ProjectManager>().OpenedProject!.ProjectPath}\\assets");
        }

        var path = SavePath;
        Directory.CreateDirectory(path);
            
        // Created or loaded data needs to be saved on disk but then disposed of since we are not going to need it
        // unless user wishes to edit the exact asset
        if (IsLoaded && serializationFlags.HasFlag(SerializationFlags.SaveData))
        {
            AssetData.Save(Path.Combine(path, Data));
            if (serializationFlags.HasFlag(SerializationFlags.FixReferences))
            {
                References.Clear();
                ExtractReferences(GetData());
            }
            AssetData.Dispose();
        }
            
        if (!serializationFlags.HasFlag(SerializationFlags.SaveData) && serializationFlags.HasFlag(SerializationFlags.FixReferences))
        {
            References.Clear();
            ExtractReferences(GetData());
            AssetData!.Dispose();
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
        
    public void Delete(bool setDirectoryToAssets = false)
    {
        AssetManager.Get().RemoveAsset(URI);
            
        if (setDirectoryToAssets)
        {
            Directory.SetCurrentDirectory($"{IoC.Get<ProjectManager>().OpenedProject!.ProjectPath}\\assets");
        }

        var path = SavePath;
        File.Delete(Path.Combine(path, $"{Name}.json"));
        File.Delete(Path.Combine(path, Data));
    }

    public abstract Type GetEditorType();
    public abstract AbstractAssetData GetData();
        
    public void SetData(AbstractAssetData data)
    {
        DisposeData();
        AssetData = data;
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
        AssetData.Dispose();
        return item;
    }

    public virtual void ExportToFile(Factory.ITwinItemFactory factory)
    {
        PreResolveResources();
        var item = Export(factory);
        using var itemFile = new FileStream($"{Name}.{TwinDataExt}", FileMode.Create, FileAccess.Write);
        using var binaryWriter = new BinaryWriter(itemFile);
        item.Write(binaryWriter);
        binaryWriter.Flush();
        binaryWriter.Close();
    }

    public virtual void PreResolveResources()
    {

    }

    public virtual void PostResolveResources(Factory.ITwinItemFactory factory, ITwinSection section, ITwinItem? item)
    {
        item?.SetID(ID);
        item?.Compile();
    }

    protected void DisposeData()
    {
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
        var item = AssetData.ResolveChunkResources(factory, section, ID, LayoutID);
        PostResolveResources(factory, section, item);

        DisposeData();
        _resolveTraversed = false;
    }

    public void RemoveReference(LabURI reference)
    {
        if (!References.Remove(reference))
        {
            return;
        }
            
        RemoveReferencesFromData(GetData(), reference);
        Serialize(SerializationFlags.SetDirectoryToAssets | SerializationFlags.SaveData | SerializationFlags.FixReferences);
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