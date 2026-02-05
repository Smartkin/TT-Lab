using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Assets.Instance;
using TT_Lab.Attributes;
using TT_Lab.Util;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class TriggerData : AbstractAssetData
{
    [JsonConstructor]
    internal TriggerData() : base(null)
    {
    }

    public TriggerData(IAsset asset) : base(asset)
    {
        Position = new Vector4(0, 0, 0, 1);
        Rotation = new Vector4(0, 0, 0, 1);
        Scale = new Vector4(1, 1, 1, 1);
        Instances = new List<LabURI>();
        InstanceExtensionValue = 10;
    }

    public TriggerData(IAsset asset, ITwinTrigger trigger) : this(asset)
    {
        SetTwinItem(trigger);
    }

    public TriggerData(IAsset asset, LabURI package, String? variant, TwinTrigger trigger, Int32? layoutId) : this(asset)
    {
        ObjectActivatorMask = trigger.ObjectActivatorMask;
        Position = CloneUtils.Clone(trigger.Position);
        Rotation = CloneUtils.Clone(trigger.Rotation);
        Scale = CloneUtils.Clone(trigger.Scale);
        Instances = new(trigger.Instances.Count);
        foreach (var inst in trigger.Instances)
        {
            Instances.Add(AssetManager.Get().GetUriByTwinId<ObjectInstance>(Owner, inst, layoutId));
        }
        Header = trigger.Header;
        UnkFloat = trigger.UnkFloat;
        InstanceExtensionValue = trigger.InstanceExtensionValue;
        TriggerMessage1 = 0;
        TriggerMessage2 = 0;
        TriggerMessage3 = 0;
        TriggerMessage4 = 0;
    }

    [JsonProperty(Required = Required.Always)]
    public TriggerActivatorObjects ObjectActivatorMask { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Vector4 Position { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Vector4 Rotation { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Vector4 Scale { get; set; }
    [JsonProperty(Required = Required.Always)]
    public List<LabURI> Instances { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt32 Header { get; set; }
    [JsonProperty(Required = Required.Always)]
    public Single UnkFloat { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt32 InstanceExtensionValue { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt16 TriggerMessage1 { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt16 TriggerMessage2 { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt16 TriggerMessage3 { get; set; }
    [JsonProperty(Required = Required.Always)]
    public UInt16 TriggerMessage4 { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Instances.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        ITwinTrigger trigger = GetTwinItem<ITwinTrigger>();
        ObjectActivatorMask = trigger.Trigger.ObjectActivatorMask;
        Position = CloneUtils.Clone(trigger.Trigger.Position);
        Rotation = CloneUtils.Clone(trigger.Trigger.Rotation);
        Scale = CloneUtils.Clone(trigger.Trigger.Scale);
        Instances = new List<LabURI>(trigger.Trigger.Instances.Count);
        foreach (var inst in trigger.Trigger.Instances)
        {
            Instances.Add(AssetManager.Get().GetUriByTwinId<ObjectInstance>(Owner, inst, layoutId));
        }
        Header = trigger.Trigger.Header;
        UnkFloat = trigger.Trigger.UnkFloat;
        InstanceExtensionValue = trigger.Trigger.InstanceExtensionValue;
        TriggerMessage1 = trigger.TriggerMessages[0];
        TriggerMessage2 = trigger.TriggerMessages[1];
        TriggerMessage3 = trigger.TriggerMessages[2];
        TriggerMessage4 = trigger.TriggerMessages[3];
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        var trigger = new TwinTrigger
        {
            Header = Header,
            ObjectActivatorMask = ObjectActivatorMask,
            UnkFloat = UnkFloat,
            Position = Position,
            Rotation = Rotation,
            Scale = Scale,
            InstanceExtensionValue = InstanceExtensionValue
        };
        foreach (var instance in Instances)
        {
            trigger.Instances.Add((UInt16)assetManager.GetAsset(instance).ExportTwinID);
        }
        trigger.Write(writer);
        writer.Write(TriggerMessage1);
        writer.Write(TriggerMessage2);
        writer.Write(TriggerMessage3);
        writer.Write(TriggerMessage4);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateTrigger(ms);
    }
}