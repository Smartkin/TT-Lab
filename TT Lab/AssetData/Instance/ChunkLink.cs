using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.AssetData.Instance;

[JsonObject]
[ReferencesAssets]
public class ChunkLink : IDocumentModel
{
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Boolean UnkFlag { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentViewModel.EditorExplicitOrder, -1)]
    public LabURI Path { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Boolean IsAlwaysVisible { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Boolean IsVisibleInCameraFrustum { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Byte UnkNum { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Boolean IsLoadWallActive { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Boolean KeepLoaded { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorReadOnly]
    public Matrix4 ObjectMatrix { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Matrix4 ChunkMatrix { get; set; }
    
    [JsonProperty(Required = Required.AllowNull)]
    [Editable]
    public Matrix4? LoadingWall { get; set; }
    
    [JsonProperty(Required = Required.AllowNull)]
    public List<TwinChunkLinkBoundingBoxBuilder> ChunkLinksCollisionData { get; set; }

    public ChunkLink()
    {
        Path = LabURI.Empty;
        IsAlwaysVisible = false;
        IsVisibleInCameraFrustum = true;
        IsLoadWallActive = true;
        LoadingWall = mat4.Identity.ToTwin();
        ObjectMatrix = mat4.Identity.ToTwin();
        ChunkMatrix = mat4.Identity.ToTwin();
        ChunkLinksCollisionData = [];
    }

    public ChunkLink(TwinChunkLink link)
    {
        var assetManager = AssetManager.Get();
        UnkFlag = link.UnkFlag;
        Path = assetManager.GetAllAssetsOf<LevelChunk>().First(c => c.GetChunkPath().Equals(link.Path.Replace('\\', System.IO.Path.DirectorySeparatorChar), StringComparison.InvariantCultureIgnoreCase)).URI;
        IsAlwaysVisible = link.IsAlwaysVisible;
        IsVisibleInCameraFrustum = link.IsVisibleInCameraFrustum;
        UnkNum = link.UnkNum;
        IsLoadWallActive = link.IsLoadWallActive;
        KeepLoaded = link.KeepLoaded;
        ObjectMatrix = CloneUtils.DeepClone(link.ObjectMatrix);
        ChunkMatrix = CloneUtils.DeepClone(link.ChunkMatrix);
        LoadingWall = CloneUtils.DeepClone(link.LoadingWall);
        ChunkLinksCollisionData = CloneUtils.DeepClone(link.ChunkLinksCollisionData);
    }

    public string DocumentName => "Chunk Link";
}