using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
[JsonObject(ItemTypeNameHandling = TypeNameHandling.Auto, MemberSerialization = MemberSerialization.OptIn)]
public class CameraData : AbstractAssetData
{
    public CameraData(IAsset asset) : base(asset)
    {
        Trigger = new TriggerData(asset);
        UnkVector1 = new Vector4(0, 0, 0, 1);
        UnkVector2 = new Vector4(0, 0, 0, 1);
    }

    public CameraData(IAsset asset, Type? mainCam1T, Type? mainCam2T) : base(asset)
    {
        if (mainCam1T != null)
        {
            MainCamera1 = (CameraSubBase)Activator.CreateInstance(mainCam1T)!;
        }
        if (mainCam2T != null)
        {
            MainCamera2 = (CameraSubBase)Activator.CreateInstance(mainCam2T)!;
        }
    }

    public CameraData(IAsset asset, ITwinCamera camera) : this(asset)
    {
        SetTwinItem(camera);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public TriggerData Trigger { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 CameraHeader { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt16 UnkShort { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat1 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector4 UnkVector1 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector4 UnkVector2 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat2 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat3 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt1 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt2 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt3 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt4 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt5 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt6 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat4 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat5 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat6 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat7 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt7 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt8 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public UInt32 UnkInt9 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Single UnkFloat8 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Byte UnkByte { get; set; }
    
    [JsonProperty(Required = Required.AllowNull)]
    [Editable(IsConstructible = true)]
    public CameraSubBase? MainCamera1 { get; set; }
    
    [JsonProperty(Required = Required.AllowNull)]
    [Editable(IsConstructible = true)]
    public CameraSubBase? MainCamera2 { get; set; }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        base.LoadInternal(dataPath, settings);

        Trigger.SetOwner(Owner);
    }

    protected override void Dispose(Boolean disposing)
    {
        Trigger.Dispose();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var camera = GetTwinItem<ITwinCamera>();
        Trigger = new TriggerData(Owner, package, variant, camera.CamTrigger, layoutId);
        CameraHeader = camera.CameraHeader;
        UnkShort = camera.UnkShort;
        UnkFloat1 = camera.UnkFloat1;
        UnkVector1 = CloneUtils.Clone(camera.UnkVector1);
        UnkVector2 = CloneUtils.Clone(camera.UnkVector2);
        UnkFloat2 = camera.UnkFloat2;
        UnkFloat3 = camera.UnkFloat3;
        UnkInt1 = camera.UnkInt1;
        UnkInt2 = camera.UnkInt2;
        UnkInt3 = camera.UnkInt3;
        UnkInt4 = camera.UnkInt4;
        UnkInt5 = camera.UnkInt5;
        UnkInt6 = camera.UnkInt6;
        UnkFloat4 = camera.UnkFloat4;
        UnkFloat5 = camera.UnkFloat5;
        UnkFloat6 = camera.UnkFloat6;
        UnkFloat7 = camera.UnkFloat7;
        UnkInt7 = camera.UnkInt7;
        UnkInt8 = camera.UnkInt8;
        UnkInt9 = camera.UnkInt9;
        UnkFloat8 = camera.UnkFloat8;
        UnkByte = camera.UnkByte;
        if (camera.MainCamera1 != null)
        {
            MainCamera1 = (CameraSubBase)CloneUtils.DeepClone(camera.MainCamera1, camera.MainCamera1.GetType());
        }
        if (camera.MainCamera2 != null)
        {
            MainCamera2 = (CameraSubBase)CloneUtils.DeepClone(camera.MainCamera2, camera.MainCamera2.GetType());
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var trigger = Trigger.Export(factory);
        trigger.Write(writer);

        // Reposition to where TriggerScripts start since they do not exist for the camera
        writer.Flush();
        ms.Position -= 2 * 4;

        writer.Write(CameraHeader);
        writer.Write(UnkShort);
        writer.Write(UnkFloat1);
        UnkVector1.Write(writer);
        UnkVector2.Write(writer);
        writer.Write(UnkFloat2);
        writer.Write(UnkFloat3);
        writer.Write(UnkInt1);
        writer.Write(UnkInt2);
        writer.Write(UnkInt3);
        writer.Write(UnkInt4);
        writer.Write(UnkInt5);
        writer.Write(UnkInt6);
        writer.Write(UnkFloat4);
        writer.Write(UnkFloat5);
        writer.Write(UnkFloat6);
        writer.Write(UnkFloat7);
        writer.Write(UnkInt7);
        writer.Write(UnkInt8);
        writer.Write(UnkInt9);
        writer.Write(UnkFloat8);
        writer.Write((UInt32)(MainCamera1?.GetCameraType() ?? ITwinCamera.CameraType.Null));
        writer.Write((UInt32)(MainCamera2?.GetCameraType() ?? ITwinCamera.CameraType.Null));
        writer.Write(UnkByte);
        MainCamera1?.Write(writer);
        MainCamera2?.Write(writer);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateCamera(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var visual = BufferGeneration.GetCubeBuffer(viewportContext.RenderContext).Model!;
        var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
        visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.5f);
        
        var size = vec3.Ones;
        var offset = -vec3.Ones * 0.5f;
        var editableObject = new EditableObject(viewportContext.RenderContext, visual, Owner.FullDataPath, offset, size);
        color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
        editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * 0.25f);
        editableObject.UnselectedColor = visual.Diffuse;
        editableObject.SetPosition(Trigger.Position.ToGlm());
        editableObject.SetRotation(new quat(Trigger.Rotation.ToRadiansGlm()));
        editableObject.SetScale(Trigger.Scale.ToGlm());
        editableObject.AddChild(viewportContext.EditingContext.CreateCameraBillboard());
        
        return [new ViewportObject(editableObject, property.Path, property)
        {
            Position = property.Find($"[data].AssetData.{nameof(Trigger)}.{nameof(Trigger.Position)}"),
            Rotation = property.Find($"[data].AssetData.{nameof(Trigger)}.{nameof(Trigger.Rotation)}"),
            Scale = property.Find($"[data].AssetData.{nameof(Trigger)}.{nameof(Trigger.Scale)}"),
        }];
    }
}