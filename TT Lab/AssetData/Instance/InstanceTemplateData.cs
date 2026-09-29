using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Behaviour;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Editors;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Implementations.PS2.Items.RM2.Layout;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance;

// The game reads templates into a table it never looks up again, so what a template holds besides its object, starters and
// properties is the tools' copy of the object's header, made again from the object when it's built
[ReferencesAssets]
public class InstanceTemplateData : AbstractAssetData
{
    // The properties' header counts them in bytes
    public const int MaxProperties = 255;

    public InstanceTemplateData(IAsset asset) : base(asset)
    {
        TemplateName = "New Instance Template";
        ObjectId = LabURI.Empty;
        BehaviourStarters = new List<LabURI>();
        Flags = new List<UInt32>();
        Floats = new List<float>();
        Ints = new List<UInt32>();
    }

    public InstanceTemplateData(IAsset asset, ITwinTemplate template) : this(asset)
    {
        SetTwinItem(template);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public String TemplateName { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(GameObject))]
    public LabURI ObjectId { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Behaviours", Hint = "The graphs whose starters the template runs")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(BehaviourGraph))]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> BehaviourStarters { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Instance state flags")]
    public Enums.InstanceState InstanceStateFlags { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxProperties)]
    public List<UInt32> Flags { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxProperties)]
    public List<Single> Floats { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxProperties)]
    public List<UInt32> Ints { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        BehaviourStarters.Clear();
        Flags.Clear();
        Floats.Clear();
        Ints.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinTemplate template = GetTwinItem<ITwinTemplate>();
        TemplateName = new String(template.Name.ToCharArray());
        ObjectId = AssetManager.Get().GetUriByTwinId<GameObject>(Owner, template.ObjectId);
        BehaviourStarters = new(template.BehaviourStarters.Count);
        // Templates refer to the starters of the graphs
        foreach (var behaviourId in template.BehaviourStarters)
        {
            BehaviourStarters.Add(AssetManager.Get().GetUriByTwinId<BehaviourGraph>(Owner, behaviourId + 1U));
        }
        InstanceStateFlags = template.InstanceStateFlags;
        Flags = CloneUtils.CloneList(template.Flags);
        Floats = CloneUtils.CloneList(template.Floats);
        Ints = CloneUtils.CloneList(template.Ints);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        CheckCount("flag properties", Flags.Count, MaxProperties);
        CheckCount("float properties", Floats.Count, MaxProperties);
        CheckCount("integer properties", Ints.Count, MaxProperties);
        var assetManager = AssetManager.Get();
        var gameObject = assetManager.GetAsset(ObjectId);
        var objectData = gameObject.GetData<GameObjectData>();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        writer.Write(TemplateName.Length);
        writer.Write(TemplateName.ToCharArray());
        writer.Write((UInt16)gameObject.ExportTwinID);
        writer.Write(objectData.SubType);
        writer.Write((Byte)objectData.Type);
        writer.Write(BehaviourStarters.Count);
        writer.Write((UInt32)BehaviourStarters.Count);
        writer.Write(10U);
        foreach (var s in BehaviourStarters)
        {
            writer.Write((UInt16)(assetManager.GetAsset(s).ExportTwinID - 1));
        }
        writer.Write(objectData.ExitPointAmount);
        writer.Write(objectData.CameraReactJointAmount);
        writer.Write(PS2AnyTemplate.PropertiesHeader(Flags.Count, Floats.Count, Ints.Count));
        writer.Write((UInt32)InstanceStateFlags);
        writer.Write(Flags.Count);
        foreach (var flag in Flags)
        {
            writer.Write(flag);
        }
        writer.Write(Floats.Count);
        foreach (var @float in Floats)
        {
            writer.Write(@float);
        }
        writer.Write(Ints.Count);
        foreach (var @int in Ints)
        {
            writer.Write(@int);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateTemplate(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var root = section.GetRoot();
        var codeSection = root.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var objectsSection = codeSection.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        var behavioursSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);
        assetManager.GetAsset(ObjectId).ResolveChunkResources(factory, objectsSection);
        foreach (var behaviour in BehaviourStarters)
        {
            assetManager.GetAsset(behaviour).ResolveChunkResources(factory, behavioursSection);
            assetManager.GetAssetData<BehaviourGraphData>(behaviour).AddStarter(factory, behavioursSection, assetManager.GetAsset(behaviour).ExportTwinID);
        }
        return base.ResolveChunkResources(factory, section, id, layoutId);
    }
}
