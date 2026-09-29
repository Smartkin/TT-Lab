using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace TT_Lab.Project.Prefabs;

public enum PrefabKind
{
    /// <summary>
    /// An instance of one of a chunk's layouts: an object instance, a trigger, a camera, a position, a path, ...
    /// </summary>
    Instance,

    /// <summary>
    /// An element of a list of a resource a chunk has once: a chunk link, a particle emitter, ...
    /// </summary>
    Element,

    /// <summary>
    /// Several instances saved together, placed around the cursor the way they stood to each other
    /// </summary>
    Group
}

/// <summary>
/// An instance of a group prefab, where it stands from the group's first instance
/// </summary>
public sealed class PrefabItem
{
    [JsonProperty(Required = Required.Always)]
    public string AssetType { get; set; } = string.Empty;

    [JsonProperty(Required = Required.Always)]
    public string DataType { get; set; } = string.Empty;

    [JsonProperty(Required = Required.Always)]
    public int LayoutID { get; set; }

    [JsonProperty(Required = Required.Always)]
    public float[] Offset { get; set; } = [0.0f, 0.0f, 0.0f];

    [JsonProperty(Required = Required.Always)]
    public JObject Data { get; set; } = new();
}

/// <summary>
/// Something of a chunk saved to be placed in any chunk, a JSON file in the project's prefabs folder
/// </summary>
public sealed class Prefab
{
    [JsonProperty(Required = Required.Always)]
    public string Name { get; set; } = string.Empty;

    [JsonProperty(Required = Required.Always)]
    [JsonConverter(typeof(StringEnumConverter))]
    public PrefabKind Kind { get; set; }

    /// <summary>
    /// Version of the game it was made from, its assets are that version's
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string Platform { get; set; } = string.Empty;

    [JsonProperty(Required = Required.Always)]
    public string Package { get; set; } = string.Empty;

    /// <summary>
    /// What it was saved from, for the list
    /// </summary>
    [JsonProperty]
    public string MadeFrom { get; set; } = string.Empty;

    /// <summary>
    /// The instance's asset type, or the type of the resource the element is a part of
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public string AssetType { get; set; } = string.Empty;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? DataType { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int? LayoutID { get; set; }

    /// <summary>
    /// The list the element is put into, a path within the resource's data node
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ListPath { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? ElementType { get; set; }

    /// <summary>
    /// The instance's data or the element, without its links to the chunk's instances. A group's is empty, its instances are its items
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    public JObject Data { get; set; } = new();

    /// <summary>
    /// A group's instances, the first is the one the others are placed around
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<PrefabItem>? Items { get; set; }

    [JsonIgnore]
    public string? FilePath { get; set; }

    /// <summary>
    /// The picture of it taken when it got saved, next to its file
    /// </summary>
    [JsonIgnore]
    public string? PreviewPath { get; set; }
}
