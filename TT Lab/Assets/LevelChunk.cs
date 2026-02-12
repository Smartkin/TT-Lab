using System;
using System.Collections.Generic;
using System.IO;
using Caliburn.Micro;
using Newtonsoft.Json;
using Splat;
using TT_Lab.AssetData;
using TT_Lab.Project;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Assets;

public class LevelChunk : SerializableAsset
{
    protected override String SavePathInPackage => string.IsNullOrEmpty(AdditionalPath) ? "levels" : $"{AdditionalPath}";
    
    public override string IconPath => "Scene.png";

    [JsonProperty(Required = Required.Always)]
    public List<LabURI> ChunkResources { get; set; } = [];

    [JsonProperty(Required = Required.Always)]
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
        throw new NotImplementedException();
    }

    public override AbstractAssetData GetData()
    {
        throw new NotSupportedException();
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