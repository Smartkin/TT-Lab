using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Project;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Assets;

public class Package : SerializableAsset
{
    public override string SavePath => Name;
    protected override string SavePathInPackage => string.Empty;

    [JsonProperty(Required = Required.Always)]
    public Boolean Enabled { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<LabURI> Dependencies { get; private set; } = [];
    [JsonProperty(Required = Required.Always)]
    public String Variant { get; set; } = "";

    public override string IconPath => "Package.png";

    public Package() : base()
    {
        Enabled = true;
        SkipExport = false;
    }

    public Package(String name, String? variant = null) : base((uint)Guid.NewGuid().GetHashCode(), name, (LabURI)$"res://{name}", !string.IsNullOrEmpty(variant), variant ?? string.Empty)
    {
        Enabled = true;
        SkipExport = false;
    }

    public Folder GetPackageFolder()
    {
        throw new NotImplementedException();
    }

    public void AddDependency(LabURI uri)
    {
        Dependencies.Add(uri);
    }

    public void RemoveDependency(LabURI uri)
    {
        Dependencies.Remove(uri);
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

    public override AbstractAssetData GetData()
    {
        throw new NotSupportedException();
    }

    public override void RegenerateUri()
    {
        URI = new LabURI($"res://{Name}");
    }

    public override void Import()
    {
    }

    public override uint Section { get; }

    public override Type GetEditorType()
    {
        throw new NotImplementedException();
    }

    protected override ResourceTreeElementViewModel CreateResourceTreeElement(ResourceTreeElementViewModel? parent = null)
    {
        return new PackageElementViewModel(URI, parent);
    }
}