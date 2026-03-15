using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.AgentLab;

namespace TT_Lab.AssetData.Code.Object;

[ReferencesAssets]
[EditorParam(DocumentModelViewModel.EditorExplicitOrder, 0)]
public class ObjectTriggerBehaviourData : IDocumentModel
{
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "What behaviour will run when the set MessageID is obtained")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(BehaviourGraph))]
    public LabURI TriggerBehaviour { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Trigger Message ID")]
    public UInt16 MessageID { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(TextFieldViewModel.TextFieldNumberRange, new[] { 0, 1 })]
    public Byte BehaviourCallerIndex { get; set; }

    public ObjectTriggerBehaviourData()
    {
        TriggerBehaviour = LabURI.Empty;
        MessageID = 0;
    }

    public ObjectTriggerBehaviourData(IAsset owner, TwinObjectTriggerBehaviour triggerBehaviour, Dictionary<string, TwinBehaviourStarter> starterMap)
    {
        var starter = starterMap.Values.First(s => s.GetID() == triggerBehaviour.TriggerBehaviour);
        TriggerBehaviour = AssetManager.Get().GetUriByTwinId<BehaviourGraph>(owner, (uint)(starter.Assigners[0].Behaviour - 1));
        Debug.Assert(TriggerBehaviour != LabURI.Empty, "Trigger behaviour must not link to an empty behaviour");
        MessageID = triggerBehaviour.MessageID;
        BehaviourCallerIndex = triggerBehaviour.BehaviourCallerIndex;
    }

    public string DocumentName => "Object Trigger Behaviour";
}