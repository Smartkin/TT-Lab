using Newtonsoft.Json;
using System;
using System.Linq;
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
        TaggedProperties = new List<TaggedProperty>();
        FloatProperties = new List<Single>();
        IntProperties = new List<Int32>();
    }

    public InstanceTemplateData(IAsset asset, ITwinTemplate template) : this(asset)
    {
        SetTwinItem(template);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The tools' name of the template. The game reads templates into a table it never looks up, so nothing of a template does anything in the game")]
    public String TemplateName { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The object the tools made instances of the template of. The copy of its header the template keeps (exit points, react joints, subtype and type) is written again from it")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(GameObject))]
    public LabURI ObjectId { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Behaviours", Hint = "The graphs whose starters the template lists, which the game never runs from it")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(BehaviourGraph))]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> BehaviourStarters { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Instance state flags", Hint = "The state flags the tools gave the template's instances")]
    public Enums.InstanceState InstanceStateFlags { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Tagged values", Hint = "Values the scripts read as an int, an angle or a float, or the index of another property. A plain number keeps the value's type, Int(x), Float(x) and Angle(x) change it")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxProperties)]
    public List<TaggedProperty> TaggedProperties { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Floats")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxProperties)]
    public List<Single> FloatProperties { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Integers")]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxProperties)]
    public List<Int32> IntProperties { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        BehaviourStarters.Clear();
        TaggedProperties.Clear();
        FloatProperties.Clear();
        IntProperties.Clear();
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
        TaggedProperties = template.TaggedProperties.Select(bits => new TaggedProperty(bits)).ToList();
        FloatProperties = CloneUtils.CloneList(template.FloatProperties);
        IntProperties = CloneUtils.CloneList(template.IntProperties);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        CheckCount("tagged values", TaggedProperties.Count, MaxProperties);
        CheckCount("float properties", FloatProperties.Count, MaxProperties);
        CheckCount("integer properties", IntProperties.Count, MaxProperties);
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
        writer.Write(PS2AnyTemplate.PropertiesHeader(TaggedProperties.Count, FloatProperties.Count, IntProperties.Count));
        writer.Write((UInt32)InstanceStateFlags);
        writer.Write(TaggedProperties.Count);
        foreach (var tagged in TaggedProperties)
        {
            writer.Write(tagged.Bits);
        }
        writer.Write(FloatProperties.Count);
        foreach (var @float in FloatProperties)
        {
            writer.Write(@float);
        }
        writer.Write(IntProperties.Count);
        foreach (var @int in IntProperties)
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
