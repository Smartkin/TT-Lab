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
    [Editable(Hint = "A link with loading hulls only loads its chunk while the player is inside one of them, this loads it while there's no player yet as well")]
    public Boolean LoadsWithoutPlayer { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(MaxLinkGraphDepth = 4)]
    [EditorParam(DocumentCompositeViewModel.EditorExplicitOrder, -1)]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(LevelChunk))]
    [EditorParam(UriLinkViewModel.BrowseExcludeWhen, nameof(LevelChunk.IsGlobalDefaultChunk))]
    [EditorParam(UriLinkViewModel.BrowseExcludeOwnerChunk, true)]
    public LabURI Path { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Whether the linked chunk's scenery is drawn from this one: always, or culled by the load wall like a portal")]
    public ChunkLinkVisibility Visibility { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Whether the load wall can be crossed. With this or Keep Loaded the linked chunk's own links load a level deeper")]
    public Boolean IsLoadWallActive { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Keeps the linked chunk in memory. With this or an active load wall the linked chunk's own links load a level deeper")]
    public Boolean KeepLoaded { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorLinkedField(typeof(InverseMatrixChange), nameof(ChunkMatrix))]
    [EditorReadOnly]
    public Matrix4 ObjectMatrix { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Matrix4 ChunkMatrix { get; set; }
    
    // A zero matrix is no wall, the way the build writes it
    [JsonProperty(Required = Required.Default, NullValueHandling = NullValueHandling.Ignore)]
    [Editable]
    [EditorLinkedField(typeof(CanEditLoadWall), nameof(IsLoadWallActive))]
    public Matrix4 LoadingWall { get; set; }

    // Links without a wall are stored without one, a link read back got a zero wall that every save then wrote into the file
    public bool ShouldSerializeLoadingWall() => LoadingWall != null && LoadingWall.ToGlm() != mat4.Zero;

    [JsonProperty("ChunkLinksCollisionData", Required = Required.AllowNull)]
    [Editable(Caption = "Loading hulls", Hint = "The linked chunk only loads while the player is inside one of them, a link without any always loads it")]
    [EditorCollectionItemPrefix("Hull")]
    public List<ChunkLinkHull> Hulls { get; set; }

    public ChunkLink()
    {
        Path = LabURI.Empty;
        Visibility = ChunkLinkVisibility.ThroughLoadWall;
        IsLoadWallActive = true;
        LoadingWall = mat4.Zero.ToTwin();
        ObjectMatrix = mat4.Identity.ToTwin();
        ChunkMatrix = mat4.Identity.ToTwin();
        Hulls = [];
    }

    // The linked chunk is the one of the same version of the game, both have chunks at the same paths
    public ChunkLink(TwinChunkLink link, LabURI package)
    {
        var assetManager = AssetManager.Get();
        LoadsWithoutPlayer = link.LoadsWithoutPlayer;
        Path = assetManager.GetRelatedAssetsOf<LevelChunk>(package).First(c => c.GetChunkPath().Equals(link.Path.Replace('\\', System.IO.Path.DirectorySeparatorChar), StringComparison.InvariantCultureIgnoreCase)).URI;
        Visibility = link.Visibility;
        IsLoadWallActive = link.IsLoadWallActive;
        KeepLoaded = link.KeepLoaded;
        ObjectMatrix = CloneUtils.DeepClone(link.ObjectMatrix);
        ChunkMatrix = CloneUtils.DeepClone(link.ChunkMatrix);
        LoadingWall = CloneUtils.DeepClone(link.LoadingWall);
        Hulls = link.ChunkLinksCollisionData.Select(hull => new ChunkLinkHull(hull.Hull)).ToList();
    }

    public string DocumentName => "Chunk Link";


    private class CanEditLoadWall : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.IsReadOnly = !linkedViewModel.GetValue<bool>();
        }

        public void Linked(PropertyNode listener, PropertyNode linkedViewModel) => DataChanged(listener, linkedViewModel);
    }
}