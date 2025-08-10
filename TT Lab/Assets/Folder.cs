using System;
using Newtonsoft.Json;
using TT_Lab.AssetData;
using TT_Lab.Assets.Factory;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Interfaces;

namespace TT_Lab.Assets
{
    [Flags]
    public enum FolderMark
    {
        Normal = 0x0,
        InChunk = 0x1,
        Locked = 0x2,
        ChunksOnly = 0x4,
        DefaultOnly = 0x8,
    }
    
    public class Folder : SerializableAsset
    {
        public override UInt32 Section => UInt32.MaxValue;
        
        [JsonProperty(Required = Required.Always)]
        public FolderMark Mark { get; set; } = FolderMark.Normal;

        public override string IconPath => "Folder.png";

        private static UInt32 _rootOrder = 0;
        private UInt32 order = 0;
        private string _iconPath;

        public Folder()
        {
            IsLoaded = true;
            SkipExport = true;
            assetData = new FolderData();
        }

        public static Folder CreatePackageFolder(Package package, String name, String? variant = null)
        {
            return new Folder((LabURI)$"res://{package.Name}", name, variant, package);
        }

        public static Folder CreatePackageFolder(Package package, String name, Folder parent, String? variant = null)
        {
            return new Folder((LabURI)$"res://{package.Name}", name, variant, parent);
        }

        public static Folder CreatePS2Folder(String name, String? variant = null)
        {
            return new Folder((LabURI)"res://PS2", name, variant);
        }

        public static Folder CreatePS2Folder(String name, Folder parent, String? variant = null)
        {
            return new Folder((LabURI)"res://PS2", name, variant, parent);
        }

        public static Folder CreateXboxFolder(String name, String? variant = null)
        {
            return new Folder((LabURI)"res://XBOX", name, variant);
        }

        public static Folder CreateXboxFolder(String name, Folder parent, String? variant = null)
        {
            return new Folder((LabURI)"res://XBOX", name, variant, parent);
        }

        public Folder(LabURI package, String name, String? variant = null) : this(package, name, variant, null)
        {
        }

        public Folder(LabURI package, String name, String? variant = null, Folder? parent = null) : this(package, variant, (UInt32)Guid.NewGuid().GetHashCode(), name)
        {
            if (parent != null)
            {
                GetData().To<FolderData>().Parent = parent.URI;
                parent.AddChild(this);
            }
            else
            {
                Order = _rootOrder++;
            }
        }

        protected Folder(LabURI package, String? variant, UInt32 id, String name) : base(id, name, package, variant != null, variant ?? "")
        {
            IsLoaded = true;
            SkipExport = true;
            assetData = new FolderData();
        }

        public void AddChild(IAsset asset)
        {
            GetData().To<FolderData>().Children.Add(asset.URI);
            asset.Order = GetOrder();
        }

        public override void Deserialize(String json)
        {
            base.Deserialize(json);
            assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
        }

        public override Type GetEditorType()
        {
            throw new NotImplementedException();
        }

        public override AbstractAssetData GetData()
        {
            if (!IsLoaded || assetData.Disposed)
            {
                assetData = new FolderData();
                assetData.Load(System.IO.Path.Combine("assets", SavePath, Data));
                IsLoaded = true;
            }
            return assetData;
        }

        public override void ResolveChunkResources(ITwinItemFactory factory, ITwinSection section)
        {
            var assetManager = AssetManager.Get();
            foreach (var item in assetData.To<FolderData>().Children)
            {
                assetManager.GetAsset(item).ResolveChunkResources(factory, section);
            }
        }

        internal UInt32 GetOrder()
        {
            return order++;
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new FolderElementViewModel(URI, parent);
        }
    }
}
