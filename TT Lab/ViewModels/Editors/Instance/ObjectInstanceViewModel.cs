using Caliburn.Micro;
using System;
using GlmSharp;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Project.Messages;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.ViewModels.Editors.Instance;

public sealed class ObjectInstanceViewModel : ViewportEditableInstanceViewModel
{
    private Enums.Layouts layoutId;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> instances = [];
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> paths = [];
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> positions = [];
    private Vector4ViewModel _position;
    private Vector3ViewModel _rotation;
    private Boolean useOnSpawnScript;
    private LabURI objectId = LabURI.Empty;
    private Int16 refListIndex;
    private LabURI onSpawnScriptId = LabURI.Empty;
    private InstanceStateFlagsViewModel stateFlags = new(Enums.InstanceState.CollisionActive | Enums.InstanceState.Visible);
    private BindableCollection<PrimitiveWrapperViewModel<UInt32>> flagParams = [];
    private BindableCollection<PrimitiveWrapperViewModel<Single>> floatParams = [];
    private BindableCollection<PrimitiveWrapperViewModel<UInt32>> intParams = [];
    private Int32 _flagIndex;
    private Int32 _floatIndex;
    private Int32 _intIndex;
    private BindableCollection<LabURI> behaviours = new();
    private BindableCollection<LabURI> objects = new();
        
    public ObjectInstanceViewModel()
    {
        DirtyTracker.AddBindableCollection(instances);
        DirtyTracker.AddBindableCollection(paths);
        DirtyTracker.AddBindableCollection(positions);
        DirtyTracker.AddBindableCollection(flagParams);
        DirtyTracker.AddBindableCollection(floatParams);
        DirtyTracker.AddBindableCollection(intParams);
        DirtyTracker.AddChild(Position);
        DirtyTracker.AddChild(Rotation);
        DirtyTracker.AddChild(stateFlags);

        AddLinkedInstanceCommand = new AddItemToListCommand<PrimitiveWrapperViewModel<LabURI>>(Instances, () => new PrimitiveWrapperViewModel<LabURI>(LabURI.Empty), 10);
        AddLinkedPathCommand = new AddItemToListCommand<PrimitiveWrapperViewModel<LabURI>>(Paths, () => new PrimitiveWrapperViewModel<LabURI>(LabURI.Empty), 10);
        AddLinkedPositionCommand = new AddItemToListCommand<PrimitiveWrapperViewModel<LabURI>>(Positions, () => new PrimitiveWrapperViewModel<LabURI>(LabURI.Empty), 10);
        DeleteLinkedInstanceCommand = new DeleteItemFromListCommand(Instances);
        DeleteLinkedPathCommand = new DeleteItemFromListCommand(Paths);
        DeleteLinkedPositionCommand = new DeleteItemFromListCommand(Positions);
    }

    protected override void Save()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<ObjectInstanceData>();
        Position.Save(data.Position);
        data.RotationX.SetRotation(Rotation.X);
        data.RotationY.SetRotation(Rotation.Y);
        data.RotationZ.SetRotation(Rotation.Z);
        data.Instances.Clear();
        foreach (var i in Instances)
        {
            data.Instances.Add(i.Value);
        }
        data.Positions.Clear();
        foreach (var p in Positions)
        {
            data.Positions.Add(p.Value);
        }
        data.Paths.Clear();
        foreach (var p in Paths)
        {
            data.Paths.Add(p.Value);
        }
        data.ObjectId = objectId;
        data.RefListIndex = RefListIndex;
        data.OnSpawnScriptId = LabURI.Empty;
        if (UseOnSpawnScript)
        {
            data.OnSpawnScriptId = onSpawnScriptId;
        }
        data.StateFlags = stateFlags.StateFlags;
        data.ParamList1.Clear();
        foreach (var f in FlagParams)
        {
            data.ParamList1.Add(f.Value);
        }
        data.ParamList2.Clear();
        foreach (var s in FloatParams)
        {
            data.ParamList2.Add(s.Value);
        }
        data.ParamList3.Clear();
        foreach (var i in IntParams)
        {
            data.ParamList3.Add(i.Value);
        }
            
        base.Save();
    }

    public override void LoadData()
    {
        var asset = AssetManager.Get().GetAsset(EditableResource);
        var data = asset.GetData<ObjectInstanceData>();
        DirtyTracker.RemoveChild(Position);
        _position = new Vector4ViewModel(data.Position);
        DirtyTracker.AddChild(Position);
        var rotX = data.RotationX.GetRotation();
        var rotY = data.RotationY.GetRotation();
        var rotZ = data.RotationZ.GetRotation();
        DirtyTracker.RemoveChild(Rotation);
        _rotation = new Vector3ViewModel(rotX, rotY, rotZ);
        DirtyTracker.AddChild(Rotation);
        instances.Clear();
        foreach (var i in data.Instances)
        {
            instances.Add(new PrimitiveWrapperViewModel<LabURI>(i));
        }

        paths.Clear();
        foreach (var p in data.Paths)
        {
            paths.Add(new PrimitiveWrapperViewModel<LabURI>(p));
        }

        positions.Clear();
        foreach (var p in data.Positions)
        {
            positions.Add(new PrimitiveWrapperViewModel<LabURI>(p));
        }
            
        objectId = data.ObjectId;
        refListIndex = data.RefListIndex;
        onSpawnScriptId = data.OnSpawnScriptId;
        useOnSpawnScript = onSpawnScriptId != LabURI.Empty;
        DirtyTracker.RemoveChild(stateFlags);
        stateFlags = new InstanceStateFlagsViewModel(MiscUtils.ConvertEnum<Enums.InstanceState>(data.StateFlags));
        DirtyTracker.AddChild(stateFlags);

        flagParams.Clear();
        foreach (var f in data.ParamList1)
        {
            flagParams.Add(new PrimitiveWrapperViewModel<UInt32>(f));
        }
        floatParams.Clear();
        foreach (var s in data.ParamList2)
        {
            floatParams.Add(new PrimitiveWrapperViewModel<Single>(s));
        }
        intParams.Clear();
        foreach (var i in data.ParamList3)
        {
            intParams.Add(new PrimitiveWrapperViewModel<UInt32>(i));
        }
        
        layoutId = MiscUtils.ConvertEnum<Enums.Layouts>(asset.LayoutID!.Value);

        behaviours = [];
        var behaviourAssets = AssetManager.Get().GetAllAssetsOf<BehaviourGraph>();
        foreach (var behaviour in behaviourAssets)
        {
            behaviours.Add(behaviour.URI);
        }

        objects = [];
        var objectAssets = AssetManager.Get().GetAllAssetsOf<GameObject>();
        foreach (var gameObject in objectAssets)
        {
            objects.Add(gameObject.URI);
        }
        
        DirtyTracker.ResetDirty();

        // IoC.Get<IEventAggregator>().PublishOnUIThreadAsync(new ChangeRenderCameraPositionMessage
        //     { NewCameraPosition = new vec3(-Position.X, Position.Y, Position.Z) });
    }

    private void UpdateParamsList()
    {
        if (objectId == LabURI.Empty)
        {
            return;
        }
        
        var objectData = AssetManager.Get().GetAssetData<GameObjectData>(objectId);
        var flagsAmount = 0;
        var floatsAmount = 0;
        var intsAmount = 0;
        switch (objectData.Type)
        {
            case ITwinObject.ObjectType.Character:
                flagsAmount = 9;
                floatsAmount = 56;
                intsAmount = 3;
                break;
            case ITwinObject.ObjectType.Pickup:
                flagsAmount = 0;
                floatsAmount = 1;
                intsAmount = 2;
                break;
            case ITwinObject.ObjectType.Crate:
                flagsAmount = 0;
                floatsAmount = 3;
                intsAmount = 2;
                break;
            case ITwinObject.ObjectType.Creature:
                flagsAmount = 1;
                floatsAmount = 6;
                intsAmount = 3;
                break;
            case ITwinObject.ObjectType.GenericObject:
                flagsAmount = 0;
                floatsAmount = 1;
                intsAmount = 2;
                break;
            case ITwinObject.ObjectType.Grabbable:
                flagsAmount = 1;
                floatsAmount = 4;
                intsAmount = 2;
                break;
            case ITwinObject.ObjectType.PayGate:
                flagsAmount = 0;
                floatsAmount = 1;
                intsAmount = 3;
                break;
            case ITwinObject.ObjectType.Graple:
                flagsAmount = 0;
                floatsAmount = 18;
                intsAmount = 2;
                break;
            case ITwinObject.ObjectType.Projectile:
                flagsAmount = 0;
                floatsAmount = 1;
                intsAmount = 2;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        while (FlagParams.Count > flagsAmount)
        {
            FlagParams.RemoveAt(FlagParams.Count - 1);
        }

        while (FlagParams.Count < flagsAmount)
        {
            FlagParams.Add(new PrimitiveWrapperViewModel<uint>(0));
        }

        while (FloatParams.Count > floatsAmount)
        {
            FloatParams.RemoveAt(FloatParams.Count - 1);
        }

        while (FloatParams.Count < floatsAmount)
        {
            FloatParams.Add(new PrimitiveWrapperViewModel<float>(0.0f));
        }

        while (IntParams.Count > intsAmount)
        {
            IntParams.RemoveAt(IntParams.Count - 1);
        }

        while (IntParams.Count < intsAmount)
        {
            IntParams.Add(new PrimitiveWrapperViewModel<uint>(0));
        }
    }
    
    public AddItemToListCommand<PrimitiveWrapperViewModel<LabURI>> AddLinkedInstanceCommand { get; private set; }
    public AddItemToListCommand<PrimitiveWrapperViewModel<LabURI>> AddLinkedPositionCommand { get; private set; }
    public AddItemToListCommand<PrimitiveWrapperViewModel<LabURI>> AddLinkedPathCommand { get; private set; }
    public DeleteItemFromListCommand DeleteLinkedInstanceCommand { get; private set; }
    public DeleteItemFromListCommand DeleteLinkedPositionCommand { get; private set; }
    public DeleteItemFromListCommand DeleteLinkedPathCommand { get; private set; }
        
    public BindableCollection<LabURI> Behaviours => behaviours;
    public BindableCollection<LabURI> Objects => objects;

    public string Name
    {
        get
        {
            var obj = AssetManager.Get().GetAsset(objectId);
            var asset = AssetManager.Get().GetAsset(EditableResource);
            return $"Instance {asset.ID} - {obj.Alias}";
        }
    }

    public override Vector4ViewModel Position => _position;
    public override Vector3ViewModel Rotation => _rotation;

    [MarkDirty]
    public Enums.Layouts LayoutID
    {
        get => layoutId;
        set
        {
            if (value != layoutId)
            {
                layoutId = value;
                    
                NotifyOfPropertyChange();
            }
        }
    }
        
    [MarkDirty]
    public LabURI InstanceObject
    {
        get => objectId;
        set
        {
            if (value != objectId)
            {
                objectId = value;
                UpdateParamsList();
                NotifyOfPropertyChange();
            }
        }
    }
    
    [MarkDirty]
    public LabURI OnSpawnScript
    {
        get => onSpawnScriptId;
        set
        {
            if (value != onSpawnScriptId)
            {
                onSpawnScriptId = value;
                
                NotifyOfPropertyChange();
            }
        }
    }
        
    [MarkDirty]
    public Boolean UseOnSpawnScript
    {
        get => useOnSpawnScript;
        set
        {
            if (value != useOnSpawnScript)
            {
                useOnSpawnScript = value;
                    
                NotifyOfPropertyChange();
            }
        }
    }
    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> Instances => instances;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> Positions => positions;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> Paths => paths;

    [MarkDirty]
    public Int16 RefListIndex
    {
        get => refListIndex;
        set
        {
            if (value != refListIndex)
            {
                refListIndex = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public InstanceStateFlagsViewModel StateFlags => stateFlags;
    public BindableCollection<PrimitiveWrapperViewModel<UInt32>> FlagParams => flagParams;

    public Int32 FlagIndex
    {
        get => _flagIndex;
        set
        {
            _flagIndex = value;
            NotifyOfPropertyChange(nameof(SelectedFlag));
        }
    }
    public UInt32 SelectedFlag
    {
        get
        {
            if (FlagParams.Count == 0) return 0;

            return FlagParams[_flagIndex].Value;
        }
        set
        {
            if (_flagIndex == -1) return;
            FlagParams[_flagIndex].Value = value;
            
            NotifyOfPropertyChange();
        }
    }
        
    public BindableCollection<PrimitiveWrapperViewModel<Single>> FloatParams => floatParams;
    public Int32 FloatIndex
    {
        get => _floatIndex;
        set
        {
            _floatIndex = value;
            NotifyOfPropertyChange(nameof(SelectedFloat));
        }
    }
    public Single SelectedFloat
    {
        get
        {
            if (FloatParams.Count == 0) return 0;
            return FloatParams[_floatIndex].Value;
        }
        set
        {
            if (_floatIndex == -1) return;
            FloatParams[_floatIndex].Value = value;
            
            NotifyOfPropertyChange();
        }
    }
        
    public BindableCollection<PrimitiveWrapperViewModel<UInt32>> IntParams => intParams;
    public Int32 IntIndex
    {
        get => _intIndex;
        set
        {
            _intIndex = value;
            NotifyOfPropertyChange(nameof(SelectedInt));
        }
    }
    public UInt32 SelectedInt
    {
        get => IntParams.Count == 0 ? 0 : IntParams[_intIndex].Value;
        set
        {
            if (_intIndex == -1) return;
            IntParams[_intIndex].Value = value;
            
            NotifyOfPropertyChange();
        }
    }
}