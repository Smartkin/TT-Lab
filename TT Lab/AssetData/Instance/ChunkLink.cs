using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance;

[JsonObject]
[ReferencesAssets]
public class ChunkLink
{
    [JsonProperty(Required = Required.Always)]
    public Boolean UnkFlag { get; set; }
    [JsonProperty(Required = Required.Always)]
    public LabURI Path { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Boolean IsRendered { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Byte UnkNum { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Boolean IsLoadWallActive { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Boolean KeepLoaded { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Matrix4 ObjectMatrix { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Matrix4 ChunkMatrix { get; set; }
    [JsonProperty(Required = Required.AllowNull)]
    public Matrix4? LoadingWall { get; set; }
    [JsonProperty(Required = Required.AllowNull)]
    public List<TwinChunkLinkBoundingBoxBuilder> ChunkLinksCollisionData { get; set; }

    public ChunkLink()
    {
        var assetManager = AssetManager.Get();
        Path = assetManager.GetAllAssetsOf<ChunkFolder>().First(c => c.Variation[..^4].Equals("levels\\earth\\hub\\beach", StringComparison.InvariantCultureIgnoreCase)).URI;
        IsRendered = true;
        IsLoadWallActive = true;
        ObjectMatrix = mat4.Identity.ToTwin();
        ChunkMatrix = mat4.Identity.ToTwin();
        ChunkLinksCollisionData = [];
    }

    public ChunkLink(TwinChunkLink link)
    {
        var assetManager = AssetManager.Get();
        UnkFlag = link.UnkFlag;
        Path = assetManager.GetAllAssetsOf<ChunkFolder>().First(c => c.Variation[..^4].Equals(link.Path[..], StringComparison.InvariantCultureIgnoreCase)).URI;
        IsRendered = link.IsRendered;
        UnkNum = link.UnkNum;
        IsLoadWallActive = link.IsLoadWallActive;
        KeepLoaded = link.KeepLoaded;
        ObjectMatrix = CloneUtils.DeepClone(link.ObjectMatrix);
        ChunkMatrix = CloneUtils.DeepClone(link.ChunkMatrix);
        LoadingWall = CloneUtils.DeepClone(link.LoadingWall);
        ChunkLinksCollisionData = CloneUtils.DeepClone(link.ChunkLinksCollisionData);
    }
}