using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Assets
{
    public class Package : Folder
    {
        private string _iconPath1;

        [JsonProperty(Required = Required.Always)]
        public Boolean Enabled { get; set; }
        [JsonProperty(Required = Required.Always)]
        public List<LabURI> Dependencies { get; private set; } = new();
        [JsonProperty(Required = Required.Always)]
        public String Variant { get; set; } = "";

        public override string IconPath => "Package.png";

        public Package() : base()
        {
            Enabled = true;
            SkipExport = false;
        }

        public Package(String name, Folder? parent = null) : base((LabURI)$"res://{name}", name, null, parent)
        {
            Enabled = true;
            SkipExport = false;
        }

        public void AddDependency(LabURI uri)
        {
            Dependencies.Add(uri);
        }

        public void RemoveDependency(LabURI uri)
        {
            Dependencies.Remove(uri);
        }

        public override AbstractAssetData GetData()
        {
            return base.GetData();
        }

        public override void RegenerateURI(bool needVariant)
        {
            URI = new LabURI($"res://{Name}{Variant}");
        }

        public override Type GetEditorType()
        {
            throw new NotImplementedException();
        }

        protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
        {
            return new PackageElementViewModel(URI, parent);
        }
    }
}
