using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.Rendering.Objects;
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
 * which keep them in their TT Lab model files
 */

[Flags]
public enum SerializationFlags
{
    None = 0,
    /// <summary>
    /// Does nothing anymore: every asset is written to its absolute path and the current directory stays as it is. Saving a chunk set it
    /// to the project's assets, and a build running at the same time went on from there
    /// </summary>
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
    /// The folder of its package the asset was made in, where its files are instead of the folder of its type
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    String? FolderInPackage { get; set; }
    
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
    
    String HashSalt { get; set; }

    /// <summary>
    /// What the disc's item of the asset was made of (its fingerprint, and each of its chunks' own versions'), recorded when the disc's
    /// assets were made: the graphics items whose IDs are made of their data are built with <see cref="ID"/>, the game's, while they're
    /// still made of that. None for what TT Lab made
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    List<UInt32>? RetailFingerprints { get; set; }

    /// <summary>
    /// The disc's IDs of the parts the asset's model file holds (an OGI's rigid models, skin and blend skin, a scenery's meshes and LODs, a
    /// sky's meshes, the models they're made of), by the kind of part and what it's made of: reading the file makes the parts again
    /// without their IDs, and a part is built with the ID of what it's still made of
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    Dictionary<String, List<UInt32>>? RetailPartIds { get; set; }

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
    /// Full system path to asset
    /// </summary>
    String FullPath { get; }

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
    /// For internal assets the asset whose data created them
    /// </summary>
    /// <remarks>Internal assets get recreated whenever their owner's data is loaded so they're dropped once the owner isn't loaded anymore</remarks>
    IAsset? InternalOwner { get; set; }

    /// <summary>
    /// If asset shouldn't be exported during game's build stage
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    Boolean SkipExport { get; set; }

    /// <summary>
    /// Whether the asset's editor should display a viewport
    /// </summary>
    Boolean SupportsViewport { get; }

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
    /// Stops holding on to the loaded data so it can be collected, the next access loads it from disk again
    /// </summary>
    /// <remarks>The data isn't disposed because renderers may still hold on to it</remarks>
    void UnloadData();

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
    /// Whether the asset references any of the given assets
    /// </summary>
    /// <param name="assets">Assets to check for</param>
    bool IsReferencingAny(IReadOnlySet<LabURI> assets);

    /// <summary>
    /// Fixes up the references to deleted assets, whether anything gets changed depends on the fixer
    /// </summary>
    /// <param name="fixer">Fixer holding the deleted assets and their replacements</param>
    void FixDeletedReferences(DeletedReferenceFixer fixer);

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
    /// Removes the asset from the project and deletes its files from the disk drive
    /// </summary>
    /// <remarks>References other assets have to it are left as is, use <see cref="AssetDeletion"/> to fix them up as well</remarks>
    void Delete();

    /// <summary>
    /// Finishes import on Project Creation stage
    /// </summary>
    void Import();

    /// <summary>
    /// Exports the item to Twinsanity format during Project Compilation stage
    /// </summary>
    Twinsanity.TwinsanityInterchange.Interfaces.ITwinItem Export(Factory.ITwinItemFactory factory);

    /// <summary>
    /// Exports the item to a file in Twinsanity's format, <see cref="ExportFileName"/> in the directory
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="directory">Where the file goes, never the current directory: saving an asset while a build runs could change that</param>
    void ExportToFile(Factory.ITwinItemFactory factory, string directory);

    /// <summary>
    /// Name of the file <see cref="ExportToFile"/> writes into its directory
    /// </summary>
    string ExportFileName { get; }

    /// <summary>
    /// Traverses the chunk sections to fill it with all the referenced data
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="section"></param>
    void ResolveChunkResources(Factory.ITwinItemFactory factory, Twinsanity.TwinsanityInterchange.Interfaces.ITwinSection section);
}