using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
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
    [Editable(IsExcludedFromPropertyGraph = true)]
    [EditorParam(DocumentModelViewModel.EditorExplicitOrder, -1)]
    public LabURI Path { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorLinkedField(typeof(WhenVisibleChanged), nameof(IsVisibleInCameraFrustum))]
    public Boolean IsAlwaysVisible { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorLinkedField(typeof(WhenVisibleChanged), nameof(IsAlwaysVisible))]
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
    [Editable(IncludeAllProperties = true)]
    [EditorLinkedField(typeof(InverseDependentMatrix), nameof(ChunkMatrix))]
    [EditorReadOnly]
    public Matrix4 ObjectMatrix { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(IncludeAllProperties = true)]
    public Matrix4 ChunkMatrix { get; set; }
    
    [JsonProperty(Required = Required.AllowNull, NullValueHandling = NullValueHandling.Ignore)]
    [Editable(IncludeAllProperties = true)]
    [EditorLinkedField(typeof(CanEditLoadWall), nameof(IsLoadWallActive))]
    public Matrix4 LoadingWall { get; set; }
    
    [JsonProperty(Required = Required.AllowNull)]
    [Editable(IncludeAllProperties = true)]
    public List<TwinChunkLinkBoundingBoxBuilder> ChunkLinksCollisionData { get; set; }

    public ChunkLink()
    {
        Path = LabURI.Empty;
        IsAlwaysVisible = false;
        IsVisibleInCameraFrustum = true;
        IsLoadWallActive = true;
        LoadingWall = mat4.Zero.ToTwin();
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

    private class WhenVisibleChanged : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            if (linkedViewModel.GetValue<bool>() && listener.GetValue<bool>())
            {
                listener.SetValue(false);
            }
        }
    }

    private class CanEditLoadWall : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.IsReadOnly = !linkedViewModel.GetValue<bool>();
        }
    }

    private class InverseDependentMatrix : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.SetValue(linkedViewModel.GetValue<Matrix4>()?.ToGlm().Inverse.ToTwin());
        }
    }
}