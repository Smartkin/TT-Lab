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
    public ObjectInstanceData() : this(null!)
    {
    }

    public ObjectInstanceData(IAsset asset) : base(asset)
    {
        InstancesRelated = 10;
        PathsRelated = 10;
        PositionsRelated = 10;
        Position = new Vector3(0, 0, 0);
        Rotation = new Vector3();
        Instances = new List<LabURI>();
        Positions = new List<LabURI>();
        Paths = new List<LabURI>();
        ObjectId = LabURI.Empty;
        OnSpawnScriptId = LabURI.Empty;
        ParamList1 = new List<UInt32>();
        ParamList2 = new List<float>();
        ParamList3 = new List<UInt32>();
    }

    public ObjectInstanceData(IAsset asset, ITwinInstance instance) : this(asset)
    {
        SetTwinItem(instance);
    }

    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Position { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Vector3 Rotation { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 InstancesRelated { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(ObjectInstance))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Instances { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 PositionsRelated { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    [EditorParam(UriLinkViewModel.BrowseType, typeof(Position))]
    [EditorParam(UriLinkViewModel.BrowseScope, UriLinkViewModel.Scope.Chunk)]
    [OnReferenceDeleted(DeletedReferenceAction.Remove)]
    public List<LabURI> Positions { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    public UInt32 PathsRelated { get; set; }
    
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
    public LabURI OnSpawnScriptId { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable]
    public Enums.InstanceState StateFlags { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Flags")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<UInt32> ParamList1 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Floats")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<Single> ParamList2 { get; set; }
    
    [JsonProperty(Required = Required.Always)]
    [Editable(Caption = "Integers")]
    [EditorParam(DocumentCollectionViewModel.IsCollectionEditable, false)]
    public List<UInt32> ParamList3 { get; set; }

    protected override void Dispose(Boolean disposing)
    {
        Instances.Clear();
        Positions.Clear();
        Paths.Clear();
        ParamList1.Clear();
        ParamList2.Clear();
        ParamList3.Clear();
    }

    public override void Import(LabURI package, String? variant, Int32? layoutId)
    {
        var assetManager = AssetManager.Get();
        var instance = GetTwinItem<ITwinInstance>();
        Position = new Vector3(instance.Position.X, instance.Position.Y, instance.Position.Z);
        Rotation = new Vector3(instance.RotationX.GetRotation(), instance.RotationY.GetRotation(),
            instance.RotationZ.GetRotation());
        InstancesRelated = instance.InstancesRelated;
        Instances = new(instance.Instances.Count);
        foreach (var inst in instance.Instances)
        {
            Instances.Add(assetManager.GetUriByTwinId<ObjectInstance>(Owner, inst, layoutId));
        }
        PositionsRelated = instance.PositionsRelated;
        Positions = new(instance.Positions.Count);
        foreach (var pos in instance.Positions)
        {
            Positions.Add(assetManager.GetUriByTwinId<Position>(Owner, pos, layoutId));
        }
        PathsRelated = instance.PathsRelated;
        Paths = new(instance.Paths.Count);
        foreach (var path in instance.Paths)
        {
            Paths.Add(assetManager.GetUriByTwinId<Path>(Owner, path, layoutId));
        }
        ObjectId = assetManager.GetUriByTwinId<GameObject>(Owner, instance.ObjectId);
        RefListIndex = instance.RefListIndex;
        OnSpawnScriptId = assetManager.GetUriByTwinId<BehaviourGraph>(Owner, instance.OnSpawnHeaderScriptID + 1U);
        StateFlags = (Enums.InstanceState)instance.StateFlags;
        ParamList1 = CloneUtils.CloneList(instance.ParamList1);
        ParamList2 = CloneUtils.CloneList(instance.ParamList2);
        ParamList3 = CloneUtils.CloneList(instance.ParamList3);
    }

    public override ITwinItem Export(ITwinItemFactory factory)
    {
        // The properties' header counts them in bytes
        CheckCount("flag properties", ParamList1.Count, InstanceTemplateData.MaxProperties);
        CheckCount("float properties", ParamList2.Count, InstanceTemplateData.MaxProperties);
        CheckCount("integer properties", ParamList3.Count, InstanceTemplateData.MaxProperties);
        var assetManager = AssetManager.Get();
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var position = new Vector4(Position, 1.0f);
        position.Write(writer);
        var twinRotationX = new TwinIntegerRotation();
        twinRotationX.SetRotation(Rotation.X);
        twinRotationX.Write(writer);
        var twinRotationY = new TwinIntegerRotation();
        twinRotationY.SetRotation(Rotation.Y);
        twinRotationY.Write(writer);
        var twinRotationZ = new TwinIntegerRotation();
        twinRotationZ.SetRotation(Rotation.Z);
        twinRotationZ.Write(writer);

        writer.Write(Instances.Count);
        writer.Write(Instances.Count);
        writer.Write(InstancesRelated);
        foreach (var inst in Instances)
        {
            writer.Write((UInt16)assetManager.GetAsset(inst).ExportTwinID);
        }

        writer.Write(Positions.Count);
        writer.Write(Positions.Count);
        writer.Write(PositionsRelated);
        foreach (var pos in Positions)
        {
            writer.Write((UInt16)assetManager.GetAsset(pos).ExportTwinID);
        }

        writer.Write(Paths.Count);
        writer.Write(Paths.Count);
        writer.Write(PathsRelated);
        foreach (var path in Paths)
        {
            writer.Write((UInt16)assetManager.GetAsset(path).ExportTwinID);
        }

        writer.Write((UInt16)assetManager.GetAsset(ObjectId).ExportTwinID);

        writer.Write(RefListIndex);

        writer.Write(OnSpawnScriptId == LabURI.Empty ? UInt16.MaxValue : (UInt16)(assetManager.GetAsset(OnSpawnScriptId).ExportTwinID - 1));
        writer.Write((Byte)ParamList1.Count);
        writer.Write((Byte)ParamList2.Count);
        writer.Write((Byte)ParamList3.Count);
        writer.Write((Byte)0);
        writer.Write((UInt32)StateFlags);

        writer.Write(ParamList1.Count);
        foreach (var flag in ParamList1)
        {
            writer.Write(flag);
        }

        writer.Write(ParamList2.Count);
        foreach (var @float in ParamList2)
        {
            writer.Write(@float);
        }

        writer.Write(ParamList3.Count);
        foreach (var param in ParamList3)
        {
            writer.Write(param);
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
        if (OnSpawnScriptId != LabURI.Empty)
        {
            assetManager.GetAsset(OnSpawnScriptId).ResolveChunkResources(factory, behavioursSection);
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