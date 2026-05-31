using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Attributes.EditorParamWrappers;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;
using ObjectInstance = TT_Lab.Assets.Instance.ObjectInstance;
using Trigger = TT_Lab.Assets.Instance.Trigger;

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
        Position = new Vector3(0, 0, 0);
        Rotation = new Vector3(0, 0, 0);
        Scale = new Vector3(1, 1, 1);
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
        Position = new Vector3(trigger.Position.X, trigger.Position.Y, trigger.Position.Z);
        Rotation = trigger.Rotation.ToEulerAngles();
        Scale = new Vector3(trigger.Scale.X, trigger.Scale.Y, trigger.Scale.Z);
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
    [Editable]
    public TriggerActivatorObjects ObjectActivatorMask { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Position { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Rotation { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Scale { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public List<LabURI> Instances { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorLinkedField(typeof(Trigger1HeaderController), nameof(TriggerArgument1Enabled))]
    [EditorLinkedField(typeof(Trigger2HeaderController), nameof(TriggerArgument2Enabled))]
    [EditorLinkedField(typeof(Trigger3HeaderController), nameof(TriggerArgument3Enabled))]
    [EditorLinkedField(typeof(Trigger4HeaderController), nameof(TriggerArgument4Enabled))]
    public UInt32 Header { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 InstanceExtensionValue { get; set; }
    
    [Editable(Caption = "On Enter Once Enabled")]
    [EditorLinkedField(typeof(HeaderTrigger1Controller), nameof(Header))]
    [EditorHiddenIn("Camera")]
    public Boolean TriggerArgument1Enabled { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "On Enter Once")]
    [EditorLinkedField(typeof(TriggerArgumentEnabler), nameof(TriggerArgument1Enabled))]
    [EditorHiddenIn("Camera")]
    public UInt16 TriggerMessage1 { get; set; }
    
    [Editable(Caption = "On Enter Enabled")]
    [EditorLinkedField(typeof(HeaderTrigger2Controller), nameof(Header))]
    [EditorHiddenIn("Camera")]
    public Boolean TriggerArgument2Enabled { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "On Enter")]
    [EditorLinkedField(typeof(TriggerArgumentEnabler), nameof(TriggerArgument2Enabled))]
    [EditorHiddenIn("Camera")]
    public UInt16 TriggerMessage2 { get; set; }
    
    [Editable(Caption = "On Stay Enabled")]
    [EditorLinkedField(typeof(HeaderTrigger3Controller), nameof(Header))]
    [EditorHiddenIn("Camera")]
    public Boolean TriggerArgument3Enabled { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "On Stay")]
    [EditorLinkedField(typeof(TriggerArgumentEnabler), nameof(TriggerArgument3Enabled))]
    [EditorHiddenIn("Camera")]
    public UInt16 TriggerMessage3 { get; set; }
    
    [Editable(Caption = "On Exit Enabled")]
    [EditorLinkedField(typeof(HeaderTrigger4Controller), nameof(Header))]
    [EditorHiddenIn("Camera")]
    public Boolean TriggerArgument4Enabled { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "On Exit")]
    [EditorLinkedField(typeof(TriggerArgumentEnabler), nameof(TriggerArgument4Enabled))]
    [EditorHiddenIn("Camera")]
    public UInt16 TriggerMessage4 { get; set; }
    
    protected override void LoadInternal(string dataPath, JsonSerializerSettings? settings = null)
    {
        base.LoadInternal(dataPath, settings);
        
        TriggerArgument1Enabled = (Header >> 0xB & 0x1) != 0;
        TriggerArgument2Enabled = (Header >> 0x8 & 0x1) != 0;
        TriggerArgument3Enabled = (Header >> 0x9 & 0x1) != 0;
        TriggerArgument4Enabled = (Header >> 0xA & 0x1) != 0;
    }

    protected override void Dispose(Boolean disposing)
    {
        Instances.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var trigger = GetTwinItem<ITwinTrigger>();
        ObjectActivatorMask = trigger.Trigger.ObjectActivatorMask;
        Position = new Vector3(trigger.Trigger.Position.X, trigger.Trigger.Position.Y, trigger.Trigger.Position.Z);
        Rotation = trigger.Trigger.Rotation.ToEulerAngles();
        Scale = new Vector3(trigger.Trigger.Scale.X, trigger.Trigger.Scale.Y, trigger.Trigger.Scale.Z);
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

        var quat = Rotation.ToQuat();
        var trigger = new TwinTrigger
        {
            Header = Header,
            ObjectActivatorMask = ObjectActivatorMask,
            UnkFloat = UnkFloat,
            Position = new Vector4(Position.X, Position.Y, Position.Z, 1.0f),
            Rotation = new Vector4(quat.x, quat.y, quat.z, quat.w),
            Scale = new Vector4(Scale.X, Scale.Y, Scale.Z, 1.0f),
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

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var visual = BufferGeneration.GetCubeBuffer(viewportContext.RenderContext).Model!;
        var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Orange);
        visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        
        var size = vec3.Ones;
        var offset = -vec3.Ones * 0.5f;
        var editableObject = new EditableObject(viewportContext.RenderContext, visual, Owner.FullDataPath, offset, size);
        color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Yellow);
        editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.25f);
        editableObject.UnselectedColor = visual.Diffuse;
        editableObject.SetPosition(Position.ToGlm());
        editableObject.SetRotation(new quat(Rotation.ToRadiansGlm()));
        editableObject.SetScale(Scale.ToGlm());
        editableObject.AddChild(viewportContext.EditingContext.CreateTriggerBillboard());
        
        return [new ViewportObject(editableObject, $"TRIGGER_{property.Path}", property)
        {
            Position = property.Find($"[data].AssetData.{nameof(Position)}"),
            Rotation = property.Find($"[data].AssetData.{nameof(Rotation)}"),
            Scale = property.Find($"[data].AssetData.{nameof(Scale)}"),
        }];
    }

    private class TriggerArgumentEnabler : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.IsReadOnly = !linkedViewModel.GetValue<bool>();
        }
    }

    private class Trigger1HeaderController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = listener.GetValue<UInt32>();
            if (linkedViewModel.GetValue<bool>())
            {
                data |= 1 << 0xB;
            }
            else
            {
                var mask = ~(1 << 0xB);
                data &= (UInt32)mask;
            }
            
            listener.SetValue(data);
        }
    }

    private class HeaderTrigger1Controller : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = linkedViewModel.GetValue<UInt32>();
            listener.SetValue((data >> 0xB & 0x1) != 0);
        }
    }
    
    private class Trigger2HeaderController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = listener.GetValue<UInt32>();
            if (linkedViewModel.GetValue<bool>())
            {
                data |= 1 << 0x8;
            }
            else
            {
                var mask = ~(1 << 0x8);
                data &= (UInt32)mask;
            }
            
            listener.SetValue(data);
        }
    }
    
    private class HeaderTrigger2Controller : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = linkedViewModel.GetValue<UInt32>();
            listener.SetValue((data >> 0x8 & 0x1) != 0);
        }
    }
    
    private class Trigger3HeaderController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = listener.GetValue<UInt32>();
            if (linkedViewModel.GetValue<bool>())
            {
                data |= 1 << 0x9;
            }
            else
            {
                var mask = ~(1 << 0x9);
                data &= (UInt32)mask;
            }
            
            listener.SetValue(data);
        }
    }
    
    private class HeaderTrigger3Controller : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = linkedViewModel.GetValue<UInt32>();
            listener.SetValue((data >> 0x9 & 0x1) != 0);
        }
    }
    
    private class Trigger4HeaderController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = listener.GetValue<UInt32>();
            if (linkedViewModel.GetValue<bool>())
            {
                data |= 1 << 0xA;
            }
            else
            {
                var mask = ~(1 << 0xA);
                data &= (UInt32)mask;
            }
            
            listener.SetValue(data);
        }
    }
    
    private class HeaderTrigger4Controller : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = linkedViewModel.GetValue<UInt32>();
            listener.SetValue((data >> 0xA & 0x1) != 0);
        }
    }
}