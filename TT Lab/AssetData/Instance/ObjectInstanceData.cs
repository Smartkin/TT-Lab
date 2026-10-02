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
    [Editable]
    public Vector3 Rotation { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 InstancesGrowth { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(ObjectInstance))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Instances { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 PositionsGrowth { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Position))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Positions { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 PathsGrowth { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Path))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Paths { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public LabURI ObjectId { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Int16 RefListIndex { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Hint = "The behaviour graph run when the instance spawns, the game refers to its starter")]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(BehaviourGraph))]
    [OnReferenceDeleted(DeletedReferenceAction.Clear)]
    public LabURI SpawnScript { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Enums.InstanceState StateFlags { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Tagged values", Hint = "Values the scripts read as an int, an angle or a float, or the index of another property: the class of the object's type keeps as many as it has room for. " +
                                                "A plain number keeps the value's type, Int(x), Float(x) and Angle(x) change it")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<TaggedProperty> TaggedProperties { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Floats")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<Single> FloatProperties { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Integers")]
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
        var assetManager = AssetManager.Get();
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
            writer.Write((UInt16)assetManager.GetAsset(inst).ExportTwinID);
        }

        writer.Write(Positions.Count);
        writer.Write(Positions.Count);
        writer.Write(PositionsGrowth);
        foreach (var pos in Positions)
        {
            writer.Write((UInt16)assetManager.GetAsset(pos).ExportTwinID);
        }

        writer.Write(Paths.Count);
        writer.Write(Paths.Count);
        writer.Write(PathsGrowth);
        foreach (var path in Paths)
        {
            writer.Write((UInt16)assetManager.GetAsset(path).ExportTwinID);
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

    public override List<ViewportObject> GetViewportObjects(ViewportContext viewportContext,
        PropertyNode property)
    {
        var assetManager = AssetManager.Get();
        // A chunk's document shows its own version of an object it shares with other chunks
        var objectAsset = property.Find($"[data].AssetData.{nameof(ObjectId)}[data]")?.Target as IAsset ?? assetManager.GetAsset(ObjectId);
        var objData = objectAsset.GetData<GameObjectData>();
        Renderable visual;
        var size = vec3.Ones * 0.5f;
        var offset = -vec3.Ones * 0.25f;
        if (objData.OGISlots.All(ogiUri => ogiUri == LabURI.Empty))
        {
            visual = viewportContext.RenderContext.MeshService.GetMesh(LabURI.Box).Model!;
            visual.Scale(vec3.Ones * 0.5f);
            size = vec3.Ones;
            offset = -vec3.Ones * 0.5f;
        }
        else
        {
            var ogiUri = objData.OGISlots.First(ogiUri => ogiUri != LabURI.Empty);
            var ogiData = assetManager.GetAssetData<OGIData>(ogiUri);
            visual = new OGI(viewportContext.RenderContext, viewportContext.RenderContext.SkeletonManager, viewportContext.RenderContext.MeshService, ogiData);
            (offset, size) = ogiData.GetBounds();
        }

        var editableObject = new EditableObject(viewportContext.RenderContext, visual, Owner.FullDataPath, offset, size);
        editableObject.Init();
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