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

namespace TT_Lab.Assets
{
    public abstract class SerializableAsset : IAsset
    {
        protected virtual String SavePath => Type.Name;
        protected virtual String DataExt => ".data";
        protected virtual String TwinDataExt => "bin";
        public abstract UInt32 Section { get; }

        protected AbstractAssetData assetData;
        protected ResourceTreeElementViewModel viewModel;

        public Type Type { get; set; }

        public String Name { get; set; }
        public Boolean Raw { get; set; }
        public virtual String IconPath => "Common_Node.png";
        public String Data { get; set; }
        public String FullDataPath => $"{IoC.Get<ProjectManager>().OpenedProject!.ProjectPath}\\assets\\{SavePath}\\{Data}";
        public UInt32 ID { get; set; }
        public String Alias { get; set; }
        public String Chunk { get; set; }
        public Int32? LayoutID { get; set; }
        public Boolean IsLoaded { get; protected set; }
        public UInt32 Order { get; set; }
        public Boolean SkipExport { get; set; } = false;

        public Dictionary<String, Object?> Parameters { get; set; } = new();
        public LabURI URI { get; set; }
        public LabURI Package { get; set; }
        public List<LabURI> References { get; set; } = new();
        public String Variation { get; set; }

        private bool resolveTraversed = false;

        protected SerializableAsset()
        {
            IsLoaded = false;
            Raw = true;
            Type = GetType();
        }

        private SerializableAsset(UInt32 id, String name)
        {
            ID = id;
            Name = name;
            Alias = Name;
            Raw = true;
            IsLoaded = true;
            Type = GetType();
        }

        protected SerializableAsset(UInt32 id, String name, LabURI package, Boolean needVariant, String variant) : this(id, name)
        {
            Package = package;
            Variation = variant;
            RegenerateLinks(needVariant);
        }

        public virtual void RegenerateURI(Boolean needVariant)
        {
            var variantAddition = needVariant ? $"/{Variation}" : "";
            var layoutId = LayoutID == null ? "" : $"/{LayoutID}";
            URI = new LabURI($"{Package}/{Type.Name}/{ID}{variantAddition}{layoutId}");
        }

        public void RegenerateDataName(Boolean needVariant)
        {
            var variantPath = needVariant ? Variation.Replace("\\", "_").Replace("/", "_") : "";
            Data = $"{Name.Replace("/", "_").Replace("\\", "_")}_{variantPath}{DataExt}";
        }

        public void RegenerateLinks(Boolean needVariant)
        {
            RegenerateDataName(needVariant);
            RegenerateURI(needVariant);
        }

        public virtual void Serialize(SerializationFlags serializationFlags = SerializationFlags.None)
        {
            if (serializationFlags.HasFlag(SerializationFlags.SetDirectoryToAssets))
            {
                Directory.SetCurrentDirectory($"{IoC.Get<ProjectManager>().OpenedProject!.ProjectPath}\\assets");
            }

            var path = SavePath;
            Directory.CreateDirectory(path);
            var variantPath = Variation == null ? "" : Variation.Replace("\\", "_").Replace("/", "_");
            var name = Name.Replace("/", "_").Replace("\\", "_");

            // Created or loaded data needs to be saved on disk but then disposed of since we are not going to need it
            // unless user wishes to edit the exact asset
            if (assetData != null && serializationFlags.HasFlag(SerializationFlags.SaveData))
            {
                assetData.Save(Path.Combine(path, Data));
                if (serializationFlags.HasFlag(SerializationFlags.FixReferences))
                {
                    References.Clear();
                    ExtractReferences(GetData());
                }
                assetData.Dispose();
                IsLoaded = false;
            }
            
            if (!serializationFlags.HasFlag(SerializationFlags.SaveData) && serializationFlags.HasFlag(SerializationFlags.FixReferences))
            {
                References.Clear();
                ExtractReferences(GetData());
                assetData!.Dispose();
                IsLoaded = false;
            }
            
            using FileStream fs = new(Path.Combine(path, $"{name}_{variantPath}.json"), FileMode.Create, FileAccess.Write);
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
            var variantPath = Variation == null ? "" : Variation.Replace("\\", "_").Replace("/", "_");
            var name = Name.Replace("/", "_").Replace("\\", "_");
            File.Delete(Path.Combine(path, $"{name}_{variantPath}.json"));
            File.Delete(Path.Combine(path, Data));
        }

        public abstract Type GetEditorType();
        public abstract AbstractAssetData GetData();
        
        public void SetData(AbstractAssetData data)
        {
            if (IsLoaded && assetData != null && !assetData.Disposed)
            {
                assetData.Dispose();
            }
            
            assetData = data;
            IsLoaded = true;
        }

        public virtual void Import()
        {
            assetData.Import(Package, Variation, LayoutID);
            ExtractReferences(assetData);
            assetData.NullifyReference();
        }

        public virtual ITwinItem Export(Factory.ITwinItemFactory factory)
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = GetData();
            }
            var item = assetData.Export(factory);
            assetData.Dispose();
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

        private void DisposeData()
        {
            assetData.Dispose();
            IsLoaded = false;
        }

        public virtual void ResolveChunkResources(Factory.ITwinItemFactory factory, ITwinSection section)
        {
            if (resolveTraversed) return;

            resolveTraversed = true;
            assetData = GetData();
            PreResolveResources();
            var item = assetData.ResolveChunkResources(factory, section, ID, LayoutID);
            PostResolveResources(factory, section, item);

            DisposeData();
            resolveTraversed = false;
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
            if (viewModel == null)
            {
                viewModel = CreateResourceTreeElement(parent);
                viewModel.Init();
            }

            return viewModel;
        }

        protected virtual ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new ResourceTreeElementViewModel(URI, parent);
        }

        private void RemoveReferencesFromData(object? data, LabURI reference)
        {
            if (data == null)
            {
                return;
            }
            
            var referencesData = data.GetType().GetCustomAttribute(typeof(ReferencesAssetsAttribute)) as ReferencesAssetsAttribute;
            if (referencesData == null)
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
                        prop.SetValue(data, LabURI.Empty);
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
                        
                        list.RemoveAt(i--);
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
            if (data == null)
            {
                return;
            }
            
            var referencesData = data.GetType().GetCustomAttribute(typeof(ReferencesAssetsAttribute)) as ReferencesAssetsAttribute;
            if (referencesData == null)
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
}
