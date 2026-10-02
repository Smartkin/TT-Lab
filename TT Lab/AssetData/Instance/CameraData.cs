using Newtonsoft.Json;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using GlmSharp;
using TT_Lab.Assets;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Editors.Descs;
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
    private const string AngleHint = "An angle in degrees (the game keeps 65536ths of a turn).";
    private const string FlagsHint = "What the camera controller takes from the camera: the flags named Sets give it the angles and distance below, No Blend In cuts to it, Values Along Geometry takes the start and end values by where the target is along the line, path or spline. The rest tell the follow camera how to move: Steers keeps its probes on, Gives Target Box and Frames Instances move the point it looks at, Holds Still, Keeps Height and Only Looks At Target hold it back. Bits 1 and 14 are never read";

    // What the derived values (a path's parameters, a spline's tangents and parameters, the boss camera's inverse matrix) were made
    // from, so the game's values are kept until the geometry changes
    private readonly Dictionary<CameraSubBase, CameraGeometry.Shape> _derivedFrom = [];

    // What most of the game's cameras have in their trigger's header (bits the game never reads) and check interval
    private const UInt32 NewTriggerHeader = 0x140000;

    public CameraData(IAsset asset) : base(asset)
    {
        Trigger = new TriggerData(asset) { CheckInterval = 0 };
        Trigger.SetHeader(NewTriggerHeader);
        TargetBoxMin = new Vector4(0, 0, 0, 1);
        TargetBoxMax = new Vector4(0, 0, 0, 1);
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
    [Editable(Hint = FlagsHint)]
    public ITwinCamera.CameraFlags Flags { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "0 on nearly every camera of the game's levels")]
    public ITwinCamera.CameraSwitches Switches { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Blend time", Hint = "Seconds the camera takes to blend in (1, 0.7, 2 and 1.5 in the game's levels)")]
    public Single BlendTime { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "With Gives Target Box, a corner of the box the follow camera looks into: it looks at the followed object's place plus the box's middle, turned with the object unless Target Box Unturned ((0, 1.6, 0) on many cameras). The tools' memory without the flag")]
    public Vector4 TargetBoxMin { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The target box's other corner, with Gives Target Box. The tools' memory without the flag")]
    public Vector4 TargetBoxMax { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "With Frames Instances, the furthest the point the follow camera looks at moves toward the middle of the trigger's instances. The tools' memory without the flag")]
    public Single FramingDistance { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "With Frames Instances, the share of the way from the followed object to the middle of the trigger's instances the point the camera looks at moves. The tools' memory without the flag")]
    public Single FramingShare { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " The first angle blender, probably the field of view, starts here with Sets Fov, or has it at the start of the geometry")]
    public UInt32 FovStart { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " Where the first angle blender goes with Sets Fov, or its value at the end of the geometry")]
    public UInt32 FovEnd { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " The pitch starts here with Sets Pitch (20° in the game's levels)")]
    public UInt32 PitchStart { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " Where the pitch goes with Sets Pitch")]
    public UInt32 PitchEnd { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " The yaw starts here with Sets Yaw (half a turn in the game's levels)")]
    public UInt32 YawStart { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " Where the yaw goes with Sets Yaw")]
    public UInt32 YawEnd { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Units from the target the camera starts at with Sets Distance (5, 10 and 4.5 in the game's levels), or at the start of the geometry")]
    public Single DistanceStart { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Units from the target the camera goes to with Sets Distance, or at the end of the geometry")]
    public Single DistanceEnd { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "With Sets Position Follow Rate, the share of the way a second the camera's place moves to where the camera puts it")]
    public Single PositionFollowRate { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "With Sets Target Follow Rate, the share of the way a second the point the camera looks at moves")]
    public Single TargetFollowRate { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " With Sets Yaw Speed, how fast the yaw turns a second, slowed by the sine of what it has left to turn. 0 in the game's levels")]
    public UInt32 YawSpeed { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " The yaw the camera blends in from with Blends In From Yaw")]
    public UInt32 BlendInYaw { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(EditorDescType = typeof(AngleEditorDesc), Hint = AngleHint + " The pitch the camera blends in from with Blends In From Pitch")]
    public UInt32 BlendInPitch { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The distance the camera blends in from with Blends In From Distance")]
    public Single BlendInDistance { get; set; }

    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Cameras of different groups don't replace each other while both are nonzero, 0 on most cameras")]
    public Byte Group { get; set; }

    [JsonProperty(Required = Required.AllowNull)]
    [Editable(IsConstructible = true, Hint = "What the camera follows, drawn in the viewport where its points, lines, boxes and arena can be dragged")]
    public CameraSubBase? MainCamera1 { get; set; }

    [JsonProperty(Required = Required.AllowNull)]
    [Editable(IsConstructible = true)]
    public CameraSubBase? MainCamera2 { get; set; }

    protected override void LoadInternal(String dataPath, JsonSerializerSettings? settings = null)
    {
        base.LoadInternal(dataPath, settings);
        Trigger.SetOwner(Owner);
        KeepDerivedValues();
    }

    protected override void Dispose(Boolean disposing)
    {
        Trigger.Dispose();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var camera = GetTwinItem<ITwinCamera>();
        Trigger = new TriggerData(Owner, package, variant, camera.CamTrigger, layoutId);
        Flags = camera.Flags;
        Switches = camera.Switches;
        BlendTime = camera.BlendTime;
        TargetBoxMin = CloneUtils.Clone(camera.TargetBoxMin);
        TargetBoxMax = CloneUtils.Clone(camera.TargetBoxMax);
        FramingDistance = camera.FramingDistance;
        FramingShare = camera.FramingShare;
        FovStart = camera.FovStart;
        FovEnd = camera.FovEnd;
        PitchStart = camera.PitchStart;
        PitchEnd = camera.PitchEnd;
        YawStart = camera.YawStart;
        YawEnd = camera.YawEnd;
        DistanceStart = camera.DistanceStart;
        DistanceEnd = camera.DistanceEnd;
        PositionFollowRate = camera.PositionFollowRate;
        TargetFollowRate = camera.TargetFollowRate;
        YawSpeed = camera.YawSpeed;
        BlendInYaw = camera.BlendInYaw;
        BlendInPitch = camera.BlendInPitch;
        BlendInDistance = camera.BlendInDistance;
        Group = camera.Group;
        if (camera.MainCamera1 != null)
        {
            MainCamera1 = (CameraSubBase)CloneUtils.DeepClone(camera.MainCamera1, camera.MainCamera1.GetType());
        }

        if (camera.MainCamera2 != null)
        {
            MainCamera2 = (CameraSubBase)CloneUtils.DeepClone(camera.MainCamera2, camera.MainCamera2.GetType());
        }

        KeepDerivedValues();
    }

    [OnSerializing]
    private void OnSerializing(StreamingContext context) => UpdateDerivedValues();

    private void KeepDerivedValues()
    {
        _derivedFrom.Clear();
        foreach (var camera in new[] { MainCamera1, MainCamera2 }.OfType<CameraSubBase>())
        {
            _derivedFrom[camera] = CameraGeometry.Snapshot(camera);
        }
    }

    /// <summary>
    /// A path's parameters, a spline's tangents and parameters and the boss camera's other matrix follow the geometry edited in the
    /// inspector and the viewport, the game's values are kept until it changes
    /// </summary>
    internal void UpdateDerivedValues()
    {
        foreach (var camera in new[] { MainCamera1, MainCamera2 }.OfType<CameraSubBase>())
        {
            var snapshot = CameraGeometry.Snapshot(camera);
            if (_derivedFrom.TryGetValue(camera, out var previous) && CameraGeometry.SameSnapshot(previous, snapshot))
            {
                continue;
            }

            CameraGeometry.UpdateDerived(camera, previous);
            _derivedFrom[camera] = CameraGeometry.Snapshot(camera);
        }
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        UpdateDerivedValues();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var trigger = Trigger.Export(factory);
        trigger.Write(writer);
        // Reposition to where TriggerScripts start since they do not exist for the camera
        writer.Flush();
        ms.Position -= 2 * 4;
        writer.Write((UInt32)Flags);
        writer.Write((UInt16)Switches);
        writer.Write(BlendTime);
        TargetBoxMin.Write(writer);
        TargetBoxMax.Write(writer);
        writer.Write(FramingDistance);
        writer.Write(FramingShare);
        writer.Write(FovStart);
        writer.Write(FovEnd);
        writer.Write(PitchStart);
        writer.Write(PitchEnd);
        writer.Write(YawStart);
        writer.Write(YawEnd);
        writer.Write(DistanceStart);
        writer.Write(DistanceEnd);
        writer.Write(PositionFollowRate);
        writer.Write(TargetFollowRate);
        writer.Write(YawSpeed);
        writer.Write(BlendInYaw);
        writer.Write(BlendInPitch);
        writer.Write(BlendInDistance);
        writer.Write((UInt32)(MainCamera1?.GetCameraType() ?? ITwinCamera.CameraType.Null));
        writer.Write((UInt32)(MainCamera2?.GetCameraType() ?? ITwinCamera.CameraType.Null));
        writer.Write(Group);
        MainCamera1?.Write(writer);
        MainCamera2?.Write(writer);

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateCamera(ms);
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext, PropertyNode property)
    {
        var visual = BufferGeneration.GetVolumeBuffer(viewportContext.RenderContext).Model!;
        var color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.Blue);
        visual.Diffuse = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * BufferGeneration.VolumeOpacity);
        
        // The cube goes from -1 to 1 before the trigger's scale
        var size = vec3.Ones * 2.0f;
        var offset = -vec3.Ones;
        var editableObject = new EditableObject(viewportContext.RenderContext, visual, Owner.FullDataPath, offset, size);
        color = System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.LightBlue);
        editableObject.SelectedColor = new vec4(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f,  color.A / 255.0f * BufferGeneration.SelectedVolumeOpacity);
        editableObject.UnselectedColor = visual.Diffuse;
        editableObject.SetPosition(Trigger.Position.ToGlm());
        editableObject.SetRotation(new quat(Trigger.Rotation.ToRadiansGlm()));
        editableObject.SetScale(Trigger.Scale.ToGlm());
        editableObject.AddChild(viewportContext.EditingContext.CreateCameraBillboard());
        
        var cameraPaths = new PolylineVisual(viewportContext.RenderContext, $"{Owner.FullDataPath}_PATHS");
        UpdateCameraPaths(cameraPaths);
        var positionProperty = property.Find($"[data].AssetData.{nameof(Trigger)}.{nameof(Trigger.Position)}");
        var camera1Property = property.Find($"[data].AssetData.{nameof(MainCamera1)}");
        var camera2Property = property.Find($"[data].AssetData.{nameof(MainCamera2)}");
        var dependencies = new[] { camera1Property, camera2Property, positionProperty }.OfType<PropertyNode>().ToList();
        var shapes = (Camera1: CameraGeometry.Snapshot(MainCamera1), Camera2: CameraGeometry.Snapshot(MainCamera2));
        var objects = new List<ViewportObject>
        {
            new(editableObject, $"CAMERA_{property.Path}", property)
            {
                Position = positionProperty,
                Rotation = property.Find($"[data].AssetData.{nameof(Trigger)}.{nameof(Trigger.Rotation)}"),
                Scale = property.Find($"[data].AssetData.{nameof(Trigger)}.{nameof(Trigger.Scale)}"),
                Category = ViewportObjectCategory.Cameras,
            },
            new(cameraPaths, $"CAMERA_PATHS_{property.Path}", property)
            {
                Category = ViewportObjectCategory.CameraPaths,
                RenderDependencies = dependencies,
                Refresh = () =>
                {
                    // The handles are objects of their own, they have to be made again when a camera changes its kind or its points
                    if (!CameraGeometry.SameLayout(shapes.Camera1, CameraGeometry.Snapshot(MainCamera1)) || !CameraGeometry.SameLayout(shapes.Camera2, CameraGeometry.Snapshot(MainCamera2)))
                    {
                        return false;
                    }

                    UpdateCameraPaths(cameraPaths);
                    return true;
                }
            }
        };
        if (camera1Property != null)
        {
            objects.AddRange(CameraGeometry.CreateHandles(viewportContext, property, camera1Property, MainCamera1, Owner.FullDataPath + "_CAMERA1", PathGeometry.MainCamera1Color));
        }

        if (camera2Property != null)
        {
            objects.AddRange(CameraGeometry.CreateHandles(viewportContext, property, camera2Property, MainCamera2, Owner.FullDataPath + "_CAMERA2", PathGeometry.MainCamera2Color));
        }

        return objects;
    }

    private void UpdateCameraPaths(PolylineVisual visual)
    {
        var strokes = new List<PolylineStroke>();
        var markers = new List<PolylineMarker>();
        var trigger = Trigger.Position.ToGlm();
        PathGeometry.AddCamera(strokes, markers, MainCamera1, trigger, PathGeometry.MainCamera1Color);
        PathGeometry.AddCamera(strokes, markers, MainCamera2, trigger, PathGeometry.MainCamera2Color);
        visual.SetGeometry(strokes, markers);
    }
}
