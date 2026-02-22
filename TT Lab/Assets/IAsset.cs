using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Assets;

/*
 * Some notes here on how some assets are used.
 * Certain assets are created temporarily during project creation and then discarded completely.
 * When finalizing the project all assets are merged together based on how editable they are in
 * external software such as Blender or any other stuff that's more competent than having to write
 * those tools completely ourselves.
 * Temporary assets include:
 * -    Model: Raw models which only contain vertex data
 * -    RigidModel: Models with materials
 * -    Mesh: Same as rigid model
 * -    Skin: Skinned models with materials
 * -    BlendSkins: Skinned models with facial poses
 * All the above-mentioned assets are merged into higher level assets such as Scenery, DynamicScenery, Skydome and OGI
 * which consequently get imported into GLTF and metadata files with all the necessary data
 */

[Flags]
public enum SerializationFlags
{
    None = 0,
    SetDirectoryToAssets = 0x1,
    SaveData = 0x2,
    FixReferences = 0x4,
    PreserveData = 0x8,
}
    
/// <summary>
/// Interface for all the assets TT Lab manages
/// </summary>
[JsonObject(MemberSerialization.OptIn)]
public interface IAsset : IDocumentModel
{
    /// <summary>
    /// Where the asset is saved in the assets folder of the project root
    /// </summary>
    String SavePath { get; }
    
    /// <summary>
    /// Additional saving path
    /// </summary>
    [JsonProperty(Required = Required.AllowNull)]
    String? AdditionalPath { get; set; }
    
    /// <summary>
    /// Asset's string type
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    Type Type { get; }

    /// <summary>
    /// In-Game's ID number
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    UInt32 ID { get; set; }
    
    /// <summary>
    /// In-Game's ID when exporting
    /// </summary>
    UInt32 ExportTwinID { get; }

    /// <summary>
    /// The main package the asset belongs to
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    LabURI Package { get; set; }
        
    /// <summary>
    /// All the assets that are referenced by this one
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    List<LabURI> References { get; set; }

    /// <summary>
    /// In case of Twinsanity ID collisions the distinct category asset belongs to
    /// </summary>
    [JsonProperty(Required = Required.AllowNull)]
    String Variation { get; set; }
    
    /// <summary>
    /// Asset's name without variation
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    String InvariantName { get; set; }

    /// <summary>
    /// Asset's name with variation if present
    /// </summary>
    String Name { get; }

    /// <summary>
    /// Path to the icon of the asset in the project tree
    /// </summary>
    String IconPath { get; }

    /// <summary>
    /// Whether asset's data is in raw form
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    Boolean Raw { get; set; }

    /// <summary>
    /// Path to asset's data
    /// </summary>
    String Data { get; }
    
    public bool MarkedForDeletion { get; }
    
    /// <summary>
    /// Full system path to asset's data
    /// </summary>
    String FullDataPath { get; }

    /// <summary>
    /// Name for the asset to display in project tree
    /// </summary>
    [JsonProperty(Required = Required.AllowNull)]
    String Alias { get; set; }

    /// <summary>
    /// Chunk path where this asset belongs to
    /// </summary>
    [JsonProperty(Required = Required.AllowNull)]
    String Chunk { get; }

    /// <summary>
    /// Resources unique URI to be accessed from around the project
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    LabURI URI { get; set; }

    /// <summary>
    /// TT Lab WPF specific data
    /// </summary>
    [JsonProperty(Required = Required.AllowNull)]
    Dictionary<String, Object?> Parameters { get; }

    /// <summary>
    /// For instances their Layout ID
    /// </summary>
    /// <remarks>Ranges from 0 to 7</remarks>
    [JsonProperty(Required = Required.AllowNull)]
    Int32? LayoutID { get; set; }

    /// <summary>
    /// What section of RM2/SM2 file the asset belongs to
    /// </summary>
    UInt32 Section { get; }

    /// <summary>
    /// Whether the data for this asset is currently in memory
    /// </summary>
    Boolean IsLoaded { get; }
    
    /// <summary>
    /// Whether the data was loaded internally from other asset, and it holds the ownership of that resource
    /// </summary>
    Boolean IsInternal { get; set; }

    /// <summary>
    /// If asset shouldn't be exported during game's build stage
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    Boolean SkipExport { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>Asset's editor type</returns>
    Type GetEditorType();

    /// <summary>
    /// Loads in asset's data if it's not loaded
    /// </summary>
    /// <returns>Asset's data</returns>
    protected AbstractAssetData GetData();
        
    /// <summary>
    /// Sets new data for the asset to use
    /// </summary>
    /// <param name="data"></param>
    void SetData(AbstractAssetData data);

    /// <summary>
    /// Adds new resource reference
    /// </summary>
    /// <param name="reference">Resource to reference</param>
    void AddReference(LabURI reference)
    {
        if (reference == LabURI.Empty || References.Contains(reference))
        {
            return;
        }
            
        References.Add(reference);
    }

    /// <summary>
    /// Removes reference to a resource
    /// </summary>
    /// <param name="reference">Resource to remove reference from</param>
    void RemoveReference(LabURI reference);

    /// <summary>
    /// Loads in asset's data if it's not loaded
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns>Asset's data as a specific type</returns>
    T GetData<T>() where T : AbstractAssetData
    {
        return (T)GetData();
    }

    /// <summary>
    /// Returns asset's viewmodel for editing
    /// </summary>
    ResourceTreeElementViewModel GetResourceTreeElement(ResourceTreeElementViewModel? parent = null);

    /// <summary>
    /// Sets or creates the parameter
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <param name="value"></param>
    void SetParameter<T>(String key, T value)
    {
        Parameters[key] = value;
    }

    /// <summary>
    /// Gets the parameter as a specific type
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    T? GetParameter<T>(String key)
    {
        var retT = typeof(T);
        if (retT.IsEnum)
        {
            return MiscUtils.ConvertEnum<T?>(Parameters[key]);
        }
        return (T?)Parameters[key];
    }

    /// <summary>
    /// Regenerates the URI if package, subpackage or variation was changed
    /// </summary>
    void RegenerateLinks();

    /// <summary>
    /// Gets the asset's data hash as CRC32 checksum
    /// </summary>
    /// <returns>The resulting hash value</returns>
    UInt32 GetDataHash();

    /// <summary>
    /// Save the data to disk
    /// </summary>
    void Serialize(SerializationFlags serializationFlags = SerializationFlags.None);

    /// <summary>
    /// Read data from the disk
    /// </summary>
    void Deserialize(String json);

    /// <summary>
    /// Called if asset needs to do anything else after being deserialized
    /// </summary>
    void PostDeserialize();

    /// <summary>
    /// Deletes the asset from disk drive and all references to it
    /// </summary>
    /// <param name="setDirectoryToAssets"></param>
    /// <param name="deleteAllReferencedData"></param>
    void Delete(bool setDirectoryToAssets = false, bool deleteAllReferencedData = false);

    /// <summary>
    /// Finishes import on Project Creation stage
    /// </summary>
    void Import();

    /// <summary>
    /// Exports the item to Twinsanity format during Project Compilation stage
    /// </summary>
    Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem Export(Factory.ITwinItemFactory factory);

    /// <summary>
    /// Exports the item to a file in Twinsanity's format
    /// </summary>
    /// <param name="factory"></param>
    void ExportToFile(Factory.ITwinItemFactory factory);

    /// <summary>
    /// Traverses the chunk sections to fill it with all the referenced data
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="section"></param>
    void ResolveChunkResources(Factory.ITwinItemFactory factory, Twinsanity.TwinsanityInterchange.Interfaces.ITwinSection section);
}