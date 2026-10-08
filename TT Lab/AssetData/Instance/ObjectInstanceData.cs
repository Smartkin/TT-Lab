using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Code.Object;
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
using TT_Lab.Views.Editors;

namespace TT_Lab.AssetData.Instance;

[ReferencesAssets]
public class ObjectInstanceData : AbstractAssetData
{
    private const Single DegreesPerUnit = 360.0f / 65536.0f;
    // The instance's attachments node keeps 16 linked instances (AttachmentsNode::MostLinked), the waypoints count their keys and
    // paths in a byte
    public const int MaxLinkedInstances = 16;
    public const int MaxWaypoints = 255;

    private const string PropertyRoom = "The class of the object's type keeps its first tagged values, floats and integers (characters 9, 56 and 3, pickups, generic objects and projectiles 0, 1 and 2, " +
                                        "crates 0, 3 and 2, creatures 1, 6 and 3, grabbables 1, 4 and 2, pay gates 0, 1 and 3, graples 0, 18 and 2), what it lacks of them is whatever the game's memory had, " +
                                        "and puts up to 7 more of all three together aside: more overwrite the game's memory, and so does any kind past its share while another is short. " +
                                        "Every instance's second integer is how far it's seen before its updates get thinned out (0 always, 255 never), a character's first the playable " +
                                        "character it is. Linking the instance to another object fills them up for its type";

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
        // No behaviour starter's receiver, an instance a script reaches gets its index set
        RefListIndex = -1;
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
    [Editable(Hint = "The object the instance is of: its type, models, behaviours and sounds. Linking another one fills the properties up to what its type keeps, with its " +
                     "template values (all of them and its state for an instance without any), and takes the positions and paths out when its type follows none")]
    [EditorLinkedField(typeof(ObjectChange), nameof(ObjectId))]
    public LabURI ObjectId { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The behaviour starters' receiver the instance is (its low byte, -1 none): starters' assigners and the scripts' designators below 0xDE reach it by this index")]
    [EditorParam(TextFieldViewModel.TextFieldNumberRange, new[] { -1, 255 })]
    public Int16 RefListIndex { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The behaviour graph the instance runs whenever its agent starts, made or restarted (the game refers to its starter). Without one its object's first behaviour slot runs")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(BehaviourGraph))]
    [EditorParam(UriLinkViewModel.IncludeEmpty, true)]
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

    private GameObjectData? ObjectData => ObjectId == LabURI.Empty ? null : AssetManager.Get().GetAsset(ObjectId)?.GetData<GameObjectData>();

    /// <summary>
    /// The object's template values and state an instance of it starts with, the type's when the object has none: what the game gives an
    /// instance its scripts spawn
    /// </summary>
    public void TakeValuesOf(GameObjectData gameObject)
    {
        var template = gameObject.TaggedProperties.Count > 0 || gameObject.FloatProperties.Count > 0 || gameObject.IntProperties.Count > 0;
        var (tagged, floats, ints) = ObjectTypes.Fit(gameObject.Type, [], [], [], gameObject.TaggedProperties.Select(value => value.Bits).ToList(),
            gameObject.FloatProperties, gameObject.IntProperties);
        TaggedProperties = tagged.Select(bits => new TaggedProperty(bits)).ToList();
        FloatProperties = floats;
        IntProperties = ints;
        StateFlags = template ? gameObject.InstanceStateFlags : ObjectTypes.Of(gameObject.Type)?.State ?? StateFlags;
    }

    // The game makes every instance's nodes by its object's type (ObjectTypes), what it can't take is refused
    private void CheckForItsObject(GameObjectData gameObject, int tagged, int floats, IReadOnlyList<Int32> ints)
    {
        if (ObjectTypes.Of(gameObject.Type) is not { } rules)
        {
            return;
        }

        if (ObjectTypes.PropertyProblem(gameObject.Type, tagged, floats, ints.Count) is { } problem)
        {
            throw new InvalidOperationException($"{Owner.Alias} {problem}");
        }

        if (!rules.HasWaypoints && (Positions.Count > 0 || Paths.Count > 0))
        {
            throw new InvalidOperationException($"{Owner.Alias} has positions or paths, but its object is a {gameObject.Type}, which the game makes no waypoints for: " +
                                                "it adds them to nothing and writes over its memory. Take them out of the instance");
        }

        if (gameObject.Type != ITwinObject.ObjectType.Character)
        {
            return;
        }

        var kind = ints.Count > ObjectTypes.CharacterKindProperty ? ints[ObjectTypes.CharacterKindProperty] : -1;
        if (!ObjectTypes.IsPlayableCharacter(kind))
        {
            throw new InvalidOperationException($"{Owner.Alias} is a character whose first integer ({(kind == -1 ? "none" : kind)}) isn't a playable character (0 to 5): " +
                                                "the game puts the instance into its table of characters at it with no check");
        }

        var (exitPoints, jointIds) = ObjectTypes.CharacterNeeds(kind);
        if (gameObject.ExitPointAmount < exitPoints || gameObject.CameraReactJointAmount < jointIds)
        {
            throw new InvalidOperationException($"{Owner.Alias} is {ObjectTypes.PlayableCharacters[kind].Name} of an object with {gameObject.ExitPointAmount} exit points and " +
                                                $"{gameObject.CameraReactJointAmount} joint IDs: the character code reads {exitPoints} exit points and {jointIds} joint IDs with no check");
        }

        var model = gameObject.OGISlots.Count > 0 ? gameObject.OGISlots[0] : LabURI.Empty;
        if (model == LabURI.Empty || !AssetManager.Get().DoesAssetExist(model) || !AssetManager.Get().GetAssetData<OGIData>(model).GetsAnimator)
        {
            throw new InvalidOperationException($"{Owner.Alias} is a character whose object's first model the game doesn't animate (none, or one joint without exit points): " +
                                                "the character code moves it by its animator");
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
        var taggedProperties = TaggedProperties.Select(value => value.Bits).ToList();
        var floatProperties = FloatProperties;
        var intProperties = IntProperties;
        if (ObjectData is { } gameObject)
        {
            // The game reads every instance's second integer with no check, an instance without one (made in TT Lab before instances took their
            // object's values) gets its object's
            if (intProperties.Count <= ObjectTypes.NearDistanceProperty)
            {
                (taggedProperties, floatProperties, intProperties) = ObjectTypes.Fit(gameObject.Type, taggedProperties, floatProperties, intProperties,
                    gameObject.TaggedProperties.Select(value => value.Bits).ToList(), gameObject.FloatProperties, gameObject.IntProperties);
                Log.WriteLine($"{Owner.Alias} has no near distance (its second integer), it's built with its object's properties filled in", Log.LogType.Warning);
            }

            CheckForItsObject(gameObject, taggedProperties.Count, floatProperties.Count, intProperties);
        }

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
        writer.Write((Byte)taggedProperties.Count);
        writer.Write((Byte)floatProperties.Count);
        writer.Write((Byte)intProperties.Count);
        writer.Write((Byte)0);
        writer.Write((UInt32)StateFlags);

        writer.Write(taggedProperties.Count);
        foreach (var tagged in taggedProperties)
        {
            writer.Write(tagged);
        }

        writer.Write(floatProperties.Count);
        foreach (var @float in floatProperties)
        {
            writer.Write(@float);
        }

        writer.Write(intProperties.Count);
        foreach (var integer in intProperties)
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

    /// <summary>
    /// What an instance of another object needs: its properties filled up to what the new object's type keeps with the object's template
    /// values (all of them and its state for an instance without any), a character's first integer a playable character, the positions and
    /// paths taken out for a type that follows none. Part of the link's step
    /// </summary>
    private sealed class ObjectChange : IFieldChange
    {
        // The object each instance's node had: a node's Changed also comes when a value above it gets replaced, nothing changes then
        private static readonly ConditionalWeakTable<PropertyNode, LabURI> Objects = new();

        public void Linked(PropertyNode listeningNode, PropertyNode changedNode)
        {
            Objects.AddOrUpdate(listeningNode, changedNode.GetValue<LabURI>() ?? LabURI.Empty);
        }

        public void DataChanged(PropertyNode listeningNode, PropertyNode changedNode)
        {
            var linked = changedNode.GetValue<LabURI>() ?? LabURI.Empty;
            var previous = Objects.TryGetValue(listeningNode, out var known) ? known : null;
            Objects.AddOrUpdate(listeningNode, linked);
            if (previous == linked || listeningNode.Target is not ObjectInstanceData data || data.ObjectId == LabURI.Empty || listeningNode.Parent is not { } owner)
            {
                return;
            }

            // A chunk's document links its own version of an object it shares with other chunks
            var objectAsset = listeningNode.Find("[data]")?.Target as IAsset ?? AssetManager.Get().GetAsset(data.ObjectId);
            if (objectAsset?.GetData<GameObjectData>() is not { } gameObject || ObjectTypes.Of(gameObject.Type) is not { } rules)
            {
                return;
            }

            PropertyNode? Node(string name) => owner.Children.FirstOrDefault(child => child.Name == name);
            var hadNone = data.TaggedProperties.Count == 0 && data.FloatProperties.Count == 0 && data.IntProperties.Count == 0;
            var template = gameObject.TaggedProperties.Select(value => value.Bits).ToList();
            var (tagged, floats, ints) = ObjectTypes.Fit(gameObject.Type, data.TaggedProperties.Select(value => value.Bits).ToList(), data.FloatProperties,
                data.IntProperties, template, gameObject.FloatProperties, gameObject.IntProperties);
            if (gameObject.Type == ITwinObject.ObjectType.Character && !ObjectTypes.IsPlayableCharacter(ints[ObjectTypes.CharacterKindProperty]))
            {
                var kind = gameObject.IntProperties.Count > ObjectTypes.CharacterKindProperty ? gameObject.IntProperties[ObjectTypes.CharacterKindProperty] : -1;
                ints[ObjectTypes.CharacterKindProperty] = ObjectTypes.IsPlayableCharacter(kind) ? kind : ObjectTypes.CharacterNone;
            }

            ObjectTypes.SetElements(Node(nameof(TaggedProperties)), tagged.Select(bits => new TaggedProperty(bits)).ToList());
            ObjectTypes.SetElements(Node(nameof(FloatProperties)), floats);
            ObjectTypes.SetElements(Node(nameof(IntProperties)), ints);
            if (hadNone)
            {
                var hasTemplate = template.Count > 0 || gameObject.FloatProperties.Count > 0 || gameObject.IntProperties.Count > 0;
                Node(nameof(StateFlags))?.SetValue(hasTemplate ? gameObject.InstanceStateFlags : rules.State);
            }

            if (!rules.HasWaypoints)
            {
                ObjectTypes.SetElements(Node(nameof(Positions)), Array.Empty<LabURI>());
                ObjectTypes.SetElements(Node(nameof(Paths)), Array.Empty<LabURI>());
            }
        }
    }
}