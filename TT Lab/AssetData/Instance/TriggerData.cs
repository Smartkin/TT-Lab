using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
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

    // What the game's triggers have: kind 50 (0 makes a plain box the sound code tests the player against) and checks every 0.3 seconds
    private const UInt32 NewHeader = 0x32;
    private const Single NewCheckInterval = 0.3f;

    public TriggerData(IAsset asset) : base(asset)
    {
        Position = new Vector3(0, 0, 0);
        Rotation = new Vector3(0, 0, 0);
        Scale = new Vector3(1, 1, 1);
        Instances = new List<LabURI>();
        InstancesGrowth = 10;
        Header = NewHeader;
        CheckInterval = NewCheckInterval;
        DeriveFromHeader();
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
        DeriveFromHeader();
        CheckInterval = trigger.CheckInterval;
        InstancesGrowth = trigger.InstancesGrowth;
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
    [EditorParam(UriLinkViewModel.BrowseType, typeof(ObjectInstance))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Instances { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Header (raw)", Hint = "The kind byte, the message bits and the polling bit as one word, edited through the fields below")]
    [EditorLinkedField(typeof(Trigger1HeaderController), nameof(TriggerArgument1Enabled))]
    [EditorLinkedField(typeof(Trigger2HeaderController), nameof(TriggerArgument2Enabled))]
    [EditorLinkedField(typeof(Trigger3HeaderController), nameof(TriggerArgument3Enabled))]
    [EditorLinkedField(typeof(Trigger4HeaderController), nameof(TriggerArgument4Enabled))]
    [EditorLinkedField(typeof(KindHeaderController), nameof(Kind))]
    [EditorLinkedField(typeof(NotPolledHeaderController), nameof(NotPolled))]
    public UInt32 Header { get; set; }

    [Editable(Hint = "The kind the game's tools gave the trigger, 50 on most. 0 makes it a plain box the sound code tests the player against instead of a trigger, nothing else of it is read")]
    [EditorLinkedField(typeof(HeaderKindController), nameof(Header))]
    public Byte Kind { get; set; }

    [Editable(Caption = "Not polled", Hint = "The trigger never checks its box, no trigger of the game's levels has it")]
    [EditorLinkedField(typeof(HeaderNotPolledController), nameof(Header))]
    public Boolean NotPolled { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Check interval", Hint = "Seconds between two checks of what's inside the box, 0.3 on nearly every trigger of the game's levels")]
    public Single CheckInterval { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The instance list's growth step from the tools, 10 everywhere and never read")]
    public UInt32 InstancesGrowth { get; set; }
    
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
        DeriveFromHeader();
    }

    // Cameras keep their trigger in their own file, which never went through loading the trigger's
    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => DeriveFromHeader();

    /// <summary>
    /// Sets the header with the fields shown of it
    /// </summary>
    internal void SetHeader(UInt32 header)
    {
        Header = header;
        DeriveFromHeader();
    }

    // The fields the editor shows of the header, which aren't stored: the header is what the game has
    private void DeriveFromHeader()
    {
        TriggerArgument1Enabled = (Header >> 0xB & 0x1) != 0;
        TriggerArgument2Enabled = (Header >> 0x8 & 0x1) != 0;
        TriggerArgument3Enabled = (Header >> 0x9 & 0x1) != 0;
        TriggerArgument4Enabled = (Header >> 0xA & 0x1) != 0;
        Kind = (Byte)Header;
        NotPolled = (Header >> 0xC & 0x1) != 0;
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
        DeriveFromHeader();
        CheckInterval = trigger.Trigger.CheckInterval;
        InstancesGrowth = trigger.Trigger.InstancesGrowth;
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
            CheckInterval = CheckInterval,
            Position = new Vector4(Position.X, Position.Y, Position.Z, 1.0f),
            Rotation = new Vector4(quat.x, quat.y, quat.z, quat.w),
            Scale = new Vector4(Scale.X, Scale.Y, Scale.Z, 1.0f),
            InstancesGrowth = InstancesGrowth
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
        var visual = BufferGeneration.GetVolumeBuffer(viewportContext.RenderContext).Model!;
        var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Orange);
        visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * BufferGeneration.VolumeOpacity);
        
        // The cube goes from -1 to 1 before the trigger's scale
        var size = vec3.Ones * 2.0f;
        var offset = -vec3.Ones;
        var editableObject = new EditableObject(viewportContext.RenderContext, visual, Owner.FullDataPath, offset, size);
        color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Yellow);
        editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * BufferGeneration.SelectedVolumeOpacity);
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
            Category = ViewportObjectCategory.Triggers,
        }];
    }

    private class KindHeaderController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.SetValue(listener.GetValue<UInt32>() & 0xFFFFFF00 | linkedViewModel.GetValue<Byte>());
        }
    }

    private class HeaderKindController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.SetValue((Byte)linkedViewModel.GetValue<UInt32>());
        }
    }

    private class NotPolledHeaderController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            var data = listener.GetValue<UInt32>() & ~(1U << 0xC);
            listener.SetValue(linkedViewModel.GetValue<bool>() ? data | 1U << 0xC : data);
        }
    }

    private class HeaderNotPolledController : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.SetValue((linkedViewModel.GetValue<UInt32>() >> 0xC & 0x1) != 0);
        }
    }

    private class TriggerArgumentEnabler : IFieldChange
    {
        public void DataChanged(PropertyNode listener, PropertyNode linkedViewModel)
        {
            listener.IsReadOnly = !linkedViewModel.GetValue<bool>();
        }

        public void Linked(PropertyNode listener, PropertyNode linkedViewModel) => DataChanged(listener, linkedViewModel);
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