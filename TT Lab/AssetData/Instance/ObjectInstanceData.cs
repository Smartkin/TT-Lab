using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Assets.Factory;
using TT_Lab.Attributes;
using TT_Lab.Extensions;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels;
using TT_Lab.ViewModels.Editors;
using TT_Lab.ViewModels.Editors.PropertyGraph;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
using ObjectInstance = TT_Lab.Assets.Instance.ObjectInstance;
using OGI = TT_Lab.Rendering.Objects.OGI;
using Path = TT_Lab.Assets.Instance.Path;
using Position = TT_Lab.Assets.Instance.Position;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class ObjectInstanceData : AbstractAssetData
{
    private const Single DegreesPerUnit = 360.0f / 65536.0f;
    // The instance's attachments node keeps 16 linked instances (AttachmentsNode::MostLinked), the waypoints count their keys and
    // paths in a byte
    public const int MaxLinkedInstances = 16;
    public const int MaxWaypoints = 255;
    // What the game puts past the values its class keeps: 7 words it allocates with no check (PropertyExtras)
    public const int MaxExtraProperties = 7;

    private const string PropertyRoom = "The class of the object's type keeps its first tagged values, floats and integers (characters 9, 56 and 3, pickups, generic objects and projectiles 0, 1 and 2, " +
                                        "crates 0, 3 and 2, creatures 1, 6 and 3, grabbables 1, 4 and 2, pay gates 0, 1 and 3, graples 0, 18 and 2) and puts up to 7 more of all three together aside: more overwrite the game's memory";

    // The tagged values, floats and integers the class of each object type keeps (the property holders' counts)
    private static readonly Dictionary<ITwinObject.ObjectType, (int Tagged, int Floats, int Ints)> ClassProperties = new()
    {
        [ITwinObject.ObjectType.Character] = (9, 56, 3),
        [ITwinObject.ObjectType.Pickup] = (0, 1, 2),
        [ITwinObject.ObjectType.Crate] = (0, 3, 2),
        [ITwinObject.ObjectType.Creature] = (1, 6, 3),
        [ITwinObject.ObjectType.GenericObject] = (0, 1, 2),
        [ITwinObject.ObjectType.Grabbable] = (1, 4, 2),
        [ITwinObject.ObjectType.PayGate] = (0, 1, 3),
        [ITwinObject.ObjectType.Graple] = (0, 18, 2),
        [ITwinObject.ObjectType.Projectile] = (0, 1, 2),
    };

    public ObjectInstanceData() : this(null!)
    {
    }

    public ObjectInstanceData(IAsset asset) : base(asset)
    {
        InstancesGrowth = 10;
        PathsGrowth = 10;
        PositionsGrowth = 10;
        Position = new Vector3(0, 0, 0);
        Rotation = new Vector3();
        Instances = new List<LabURI>();
        Positions = new List<LabURI>();
        Paths = new List<LabURI>();
        ObjectId = LabURI.Empty;
        SpawnScript = LabURI.Empty;
        TaggedProperties = new List<TaggedProperty>();
        FloatProperties = new List<Single>();
        IntProperties = new List<Int32>();
    }

    public ObjectInstanceData(IAsset asset, ITwinInstance instance) : this(asset)
    {
        SetTwinItem(instance);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Position { get; set; }
    
    /// <summary>
    /// The turns about X, Y and Z in degrees, the game keeps each as a signed word of 65536ths of a turn
    /// </summary>
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The turns about X, Y and Z in degrees, which the game keeps as signed words of 65536ths of a turn")]
    public Vector3 Rotation { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 InstancesGrowth { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Instances of the chunk linked to this one when the chunk loads, like the scripts link instances (its attachments): the game links 16 at most and leaves out the rest")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(ObjectInstance))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxLinkedInstances)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Instances { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 PositionsGrowth { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Positions of the chunk that are the instance's keys in this order, which its scripts move it to and focus on (its waypoints, counted in a byte)")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Position))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxWaypoints)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Positions { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 PathsGrowth { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "Paths of the chunk its scripts move the instance along, in this order (its waypoints, counted in a byte)")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Path))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [EditorParam(DocumentCollectionViewModel.MaxCount, MaxWaypoints)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Paths { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The object the instance is of: its type, models, behaviours and sounds")]
    public LabURI ObjectId { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The behaviour starters' receiver the instance is (its low byte, -1 none): starters' assigners and the scripts' designators below 0xDE reach it by this index")]
    [EditorParam(TextFieldViewModel.TextFieldNumberRange, new[] { -1, 255 })]
    public Int16 RefListIndex { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The behaviour graph the instance runs whenever its agent starts, made or restarted (the game refers to its starter). Without one its object's first behaviour slot runs")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(BehaviourGraph))]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI SpawnScript { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "What the instance starts as: asleep (bit 0), found by ray casts and the characters' collision (1), drawn (2), casting its shadow (3), told by triggers (8), hitting back characters attacking it (9; 16 always, " +
                     "and then its script never hears of attacks), a target of the characters' target lock (15), bouncing projectiles back (17), put on the ground below when made (18, creatures and characters). " +
                     "Bits 10-14 stop the characters' body slam, slide, spin, twin slam and thrown Cortex; on a crate 10 lets falling crates land on it and 11 makes it unbreakable. " +
                     "5 keeps its previous transform for what stands on it, 6 gives it a persistent flag of its chunk, 7 keeps that flag in the chunk's own store. " +
                     "Bits 4 and 19-31 are the scripts' own (Soft Flag): no engine code reads them, scripts test any bit with SoftFlagSet(n), and the creatures', drones', bats' and penguins' scripts take 19-31 as options of their instances")]
    public Enums.InstanceState StateFlags { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Tagged values", Hint = "Values the scripts read as an int, an angle or a float, or the index of another property. A plain number keeps the value's type, Int(x), Float(x) and Angle(x) change it. " + PropertyRoom)]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<TaggedProperty> TaggedProperties { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Floats", Hint = PropertyRoom)]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<Single> FloatProperties { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Integers", Hint = PropertyRoom)]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<Int32> IntProperties { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Instances.Clear();
        Positions.Clear();
        Paths.Clear();
        TaggedProperties.Clear();
        FloatProperties.Clear();
        IntProperties.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var assetManager = AssetManager.Get();
        var instance = GetTwinItem<ITwinInstance>();
        Position = new Vector3(instance.Position.X, instance.Position.Y, instance.Position.Z);
        Rotation = new Vector3(instance.RotationX * DegreesPerUnit, instance.RotationY * DegreesPerUnit, instance.RotationZ * DegreesPerUnit);
        InstancesGrowth = instance.InstancesGrowth;
        Instances = new(instance.Instances.Count);
        foreach (var inst in instance.Instances)
        {
            Instances.Add(assetManager.GetUriByTwinId<ObjectInstance>(Owner, inst, layoutId));
        }
        PositionsGrowth = instance.PositionsGrowth;
        Positions = new(instance.Positions.Count);
        foreach (var pos in instance.Positions)
        {
            Positions.Add(assetManager.GetUriByTwinId<Position>(Owner, pos, layoutId));
        }
        PathsGrowth = instance.PathsGrowth;
        Paths = new(instance.Paths.Count);
        foreach (var path in instance.Paths)
        {
            Paths.Add(assetManager.GetUriByTwinId<Path>(Owner, path, layoutId));
        }
        ObjectId = assetManager.GetUriByTwinId<GameObject>(Owner, instance.ObjectId);
        RefListIndex = instance.RefListIndex;
        SpawnScript = assetManager.GetUriByTwinId<BehaviourGraph>(Owner, instance.SpawnScriptId + 1U);
        StateFlags = instance.StateFlags;
        TaggedProperties = instance.TaggedProperties.Select(bits => new TaggedProperty(bits)).ToList();
        FloatProperties = CloneUtils.CloneList(instance.FloatProperties);
        IntProperties = CloneUtils.CloneList(instance.IntProperties);
    }

    private void CheckExtraProperties()
    {
        if (ObjectId == LabURI.Empty || AssetManager.Get().GetAsset(ObjectId)?.GetData<GameObjectData>() is not { } gameObject ||
            !ClassProperties.TryGetValue(gameObject.Type, out var kept))
        {
            return;
        }

        var extras = Math.Max(0, TaggedProperties.Count - kept.Tagged) + Math.Max(0, FloatProperties.Count - kept.Floats) + Math.Max(0, IntProperties.Count - kept.Ints);
        if (extras > MaxExtraProperties)
        {
            throw new InvalidOperationException($"{Owner.Alias} has {extras} property values past the {kept.Tagged} tagged values, {kept.Floats} floats and {kept.Ints} integers its object's type keeps, " +
                                                $"the game keeps {MaxExtraProperties} of them and writes the rest over its memory");
        }
    }

    // Whole 65536ths of a turn, signed like the game's words; the game's angles come back exactly
    private static Int32 AngleUnits(Single degrees)
    {
        return (Int32)MathF.Round(degrees / DegreesPerUnit);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        // The properties' header counts them in bytes
        CheckCount("tagged values", TaggedProperties.Count, InstanceTemplateData.MaxProperties);
        CheckCount("float properties", FloatProperties.Count, InstanceTemplateData.MaxProperties);
        CheckCount("integer properties", IntProperties.Count, InstanceTemplateData.MaxProperties);
        CheckCount("linked instances", Instances.Count, MaxLinkedInstances);
        CheckCount("positions", Positions.Count, MaxWaypoints);
        CheckCount("paths", Paths.Count, MaxWaypoints);
        CheckExtraProperties();
        var assetManager = AssetManager.Get();
        var indexes = LayoutIndexes.Current;
        var layout = Owner.LayoutID ?? 0;
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var position = new Vector4(Position, 1.0f);
        position.Write(writer);
        writer.Write(AngleUnits(Rotation.X));
        writer.Write(AngleUnits(Rotation.Y));
        writer.Write(AngleUnits(Rotation.Z));

        writer.Write(Instances.Count);
        writer.Write(Instances.Count);
        writer.Write(InstancesGrowth);
        foreach (var inst in Instances)
        {
            var instance = assetManager.GetAsset(inst);
            writer.Write((UInt16)(indexes?.InstanceReference(layout, instance) ?? instance.ExportTwinID));
        }

        writer.Write(Positions.Count);
        writer.Write(Positions.Count);
        writer.Write(PositionsGrowth);
        foreach (var pos in Positions)
        {
            var key = assetManager.GetAsset(pos);
            writer.Write((UInt16)(indexes?.PositionReference(layout, key) ?? key.ExportTwinID));
        }

        writer.Write(Paths.Count);
        writer.Write(Paths.Count);
        writer.Write(PathsGrowth);
        foreach (var path in Paths)
        {
            var waypoints = assetManager.GetAsset(path);
            writer.Write((UInt16)(indexes?.PathReference(layout, waypoints) ?? waypoints.ExportTwinID));
        }

        writer.Write((UInt16)assetManager.GetAsset(ObjectId).ExportTwinID);

        writer.Write(RefListIndex);

        writer.Write(SpawnScript == LabURI.Empty ? UInt16.MaxValue : (UInt16)(assetManager.GetAsset(SpawnScript).ExportTwinID - 1));
        writer.Write((Byte)TaggedProperties.Count);
        writer.Write((Byte)FloatProperties.Count);
        writer.Write((Byte)IntProperties.Count);
        writer.Write((Byte)0);
        writer.Write((UInt32)StateFlags);

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
        foreach (var integer in IntProperties)
        {
            writer.Write(integer);
        }

        writer.Flush();
        ms.Position = 0;
        return factory.GenerateInstance(ms);
    }

    public override ITwinItem? ResolveChunkResources(ITwinItemFactory factory, ITwinSection section, uint id,
        int? layoutId = null)
    {
        var assetManager = AssetManager.Get();
        var root = section.GetRoot();
        var codeSection = root.GetItem<ITwinSection>(Constants.LEVEL_CODE_SECTION);
        var objectsSection = codeSection.GetItem<ITwinSection>(Constants.CODE_GAME_OBJECTS_SECTION);
        var behavioursSection = codeSection.GetItem<ITwinSection>(Constants.CODE_BEHAVIOURS_SECTION);

        if (layoutId is Constants.LEVEL_LAYOUT_6_SECTION)
        {
            return base.ResolveChunkResources(factory, section, id, layoutId);
        }

        assetManager.GetAsset(ObjectId).ResolveChunkResources(factory, objectsSection);
        if (SpawnScript != LabURI.Empty)
        {
            assetManager.GetAsset(SpawnScript).ResolveChunkResources(factory, behavioursSection);
        }

        // Positions, paths and instances don't need to be resolved because they are gonna be resolved by themselves anyway

        return base.ResolveChunkResources(factory, section, id, layoutId);
    }

    /// <summary>
    /// The model an instance of the object is drawn with, its first slot's that has one, none for a box
    /// </summary>
    internal static LabURI ModelOf(GameObjectData objectData)
    {
        return objectData.OGISlots.FirstOrDefault(ogiUri => ogiUri != LabURI.Empty) ?? LabURI.Empty;
    }

    /// <summary>
    /// What an instance drawn with the model looks like in a viewport, at the origin: the model, a box without one
    /// </summary>
    internal static EditableObject CreateVisual(RenderContext context, LabURI model, string name)
    {
        Renderable visual;
        vec3 size;
        vec3 offset;
        if (model == LabURI.Empty)
        {
            visual = context.MeshService.GetMesh(LabURI.Box).Model!;
            visual.Scale(vec3.Ones * 0.5f);
            size = vec3.Ones;
            offset = -vec3.Ones * 0.5f;
        }
        else
        {
            var ogiData = AssetManager.Get().GetAssetData<OGIData>(model);
            visual = new OGI(context, context.SkeletonManager, context.MeshService, ogiData);
            (offset, size) = ogiData.GetBounds();
        }

        var editableObject = new EditableObject(context, visual, name, offset, size);
        editableObject.Init();
        return editableObject;
    }

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var assetManager = AssetManager.Get();
        // A chunk's document shows its own version of an object it shares with other chunks
        var objectAsset = property.Find($"[data].AssetData.{nameof(ObjectId)}[data]")?.Target as IAsset ?? assetManager.GetAsset(ObjectId);
        var editableObject = CreateVisual(viewportContext.RenderContext, ModelOf(objectAsset.GetData<GameObjectData>()), Owner.FullDataPath);
        editableObject.SetPosition(Position.ToGlm());
        editableObject.SetRotation(new quat(Rotation.ToRadiansGlm()));
        var objectProperty = property.Find($"[data].AssetData.{nameof(ObjectId)}");
        return [new ViewportObject(editableObject, $"INSTANCE_{property.Path}", property)
        {
            Position = property.Find($"[data].AssetData.{nameof(Position)}"),
            Rotation = property.Find($"[data].AssetData.{nameof(Rotation)}"),
            Category = ViewportObjectCategory.Instances,
            // A different object brings a different model, which is easier to make from scratch
            RenderDependencies = objectProperty == null ? [] : [objectProperty],
            Refresh = () => false,
        }];
    }
}