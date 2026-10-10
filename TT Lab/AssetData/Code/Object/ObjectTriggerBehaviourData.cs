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
using TT_Lab.ViewModels.Editors.Descs;
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
    [Editable(Caption = "Trigger Message ID", Hint = "The trigger message that runs the behaviour, the game keeps 10 bits of it (0 to 1023)")]
    [EditorParam(TextFieldViewModel.TextFieldNumberRange, new[] { 0, 1023 })]
    public UInt16 MessageID { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Behaviour Runner", EditorDescType = typeof(TriggerMessageRunnerEditorDesc), Hint = "Which of the instance's two behaviour runners the behaviour " +
              "starts on, both run every frame. It takes over its runner while its starter's priority is higher than what runs there, or the same with " +
              "another starter, else the message does nothing. Hover a choice for what it does")]
    [EditorParam(TextFieldViewModel.TextFieldNumberRange, new[] { 0, 1 })]
    public Byte BehaviourCallerIndex { get; set; }

    // An object's node has two behaviour runners (the decomp's ObjectNodeBase::runners, OnTriggerMessage starts the message's starter on
    // the one of this bit), the instance's spawn script runs on the first
    public static readonly IReadOnlyList<NamedChoice> Runners =
    [
        new(0, "Replace Current Behaviour", "The instance's main runner, where its spawn script (or its object's first behaviour slot) runs: the behaviour " +
                                            "takes the place of what the instance is doing"),
        new(1, "Run in parallel", "The instance's second runner: the behaviour runs alongside its main behaviour, which carries on (it takes the place " +
                                  "of another behaviour running there)"),
    ];

    public static NamedChoice FindRunner(int value)
    {
        return Runners.FirstOrDefault(runner => runner.Value == value) ?? new NamedChoice(value, "Not a runner", "The game keeps one bit of it, an instance has two runners");
    }

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