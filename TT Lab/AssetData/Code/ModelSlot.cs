using System;
using Newtonsoft.Json;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.Descs;

namespace TT_Lab.AssetData.Code;

/// <summary>
/// A model a game object can show with the animation it plays on it, which is one of the model's animations
/// </summary>
[ReferencesAssets]
[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class ModelSlot
{
    public const UInt16 NoAnimation = 0xFFFF;

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "OGI")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(OGI))]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI Ogi { get; set; } = LabURI.Empty;

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Animation", EditorDescType = typeof(OgiAnimationEditorDesc))]
    public UInt16 Animation { get; set; } = NoAnimation;
}
