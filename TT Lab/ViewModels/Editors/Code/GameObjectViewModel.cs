using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Caliburn.Micro;
using Splat;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Code.Behaviour;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.ViewModels.Editors.Code;

public class GameObjectViewModel : ResourceEditorViewModel
{
    private string _name;
    private ITwinObject.ObjectType _type;
    private byte _unkTypeValue;
    private byte _cameraReactJointAmount;
    private byte _exitPointAmount;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _ogiSlots;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _animationSlots;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _behaviourSlots;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _objectSlots;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _soundSlots;
    private InstanceStateFlagsViewModel _instanceStateFlags = new(Enums.InstanceState.Deactivated);
    private BindableCollection<PrimitiveWrapperViewModel<uint>> _instFlags;
    private BindableCollection<PrimitiveWrapperViewModel<float>> _instFloats;
    private BindableCollection<PrimitiveWrapperViewModel<uint>> _instIntegers;
    private BehaviourCommandPackViewModel _commandPack;
    private int _selectedAnimationOgiPairSlot;
    private ICommand _behaviourFilterCommand;

    public GameObjectViewModel()
    {
        ObjectScene = Locator.Current.GetService<ViewportViewModel>()!;
        InitObjectScene();
        SelectedAnimationOgiPairSlot = 0;
        _behaviourFilterCommand = new CollectionFilterCommand(o =>
        {
            var browserViewModel = (ResourceBrowserViewModel)o;
            browserViewModel.Filter((uri) =>
            {
                if (uri == LabURI.Empty)
                {
                    return true;
                }
                
                var asset = AssetManager.Get().GetAsset(uri);
                return asset is BehaviourGraph or BehaviourCommandsSequence;
            });
        });
    }

    private void InitObjectScene()
    {
        ObjectScene.SceneInitializer = (renderer, scene) =>
        {
            // var sceneManager = window.GetSceneManager();
            // var pivot = sceneManager.getRootSceneNode().createChildSceneNode();
            // pivot.setPosition(0, 0, 0);
            // window.SetCameraTarget(pivot);
            // window.SetCameraStyle(CameraStyle.CS_ORBIT);
            // window.EnableImgui(true);

            if (SelectedAnimationOgiPairSlot == -1 || OgiSlots[SelectedAnimationOgiPairSlot].Value == LabURI.Empty)
            {
                return;
            }
            
            // TODO: Create separate animation player that takes in OGI and animation
            var ogiData = AssetManager.Get().GetAssetData<OGIData>(OgiSlots[SelectedAnimationOgiPairSlot].Value);
            var context = renderer.GetRenderContext();
            var ogiRender = new Rendering.Objects.OGI(context, context.SkeletonManager, context.MeshService, ogiData);
            scene.AddChild(ogiRender);
            // pivot.addChild(ogiRender.GetSceneNode());
            // pivot.setInheritOrientation(false);
            // pivot.setInheritScale(false);
        };
    }

    public override void LoadData()
    {
        var data = AssetManager.Get().GetAssetData<GameObjectData>(EditableResource);
        _name = data.Name;
        _type = data.Type;
        _unkTypeValue = data.UnkTypeValue;
        _cameraReactJointAmount = data.CameraReactJointAmount;
        _exitPointAmount = data.ExitPointAmount;
        _instanceStateFlags = new InstanceStateFlagsViewModel(data.InstanceStateFlags);
        _commandPack = new BehaviourCommandPackViewModel(this, data.BehaviourPack);
        DirtyTracker.AddChild(_commandPack);
        DirtyTracker.AddChild(_instanceStateFlags);
        
        _ogiSlots = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _animationSlots = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _behaviourSlots = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _objectSlots = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _soundSlots = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        DirtyTracker.AddBindableCollection(_ogiSlots);
        DirtyTracker.AddBindableCollection(_animationSlots);
        DirtyTracker.AddBindableCollection(_behaviourSlots);
        DirtyTracker.AddBindableCollection(_objectSlots);
        DirtyTracker.AddBindableCollection(_soundSlots);
        foreach (var ogiSlot in data.OGISlots)
        {
            _ogiSlots.Add(new PrimitiveWrapperViewModel<LabURI>(ogiSlot, true));
        }
        foreach (var animSlot in data.AnimationSlots)
        {
            _animationSlots.Add(new PrimitiveWrapperViewModel<LabURI>(animSlot, true));
        }
        foreach (var behaviourSlot in data.BehaviourSlots)
        {
            _behaviourSlots.Add(new PrimitiveWrapperViewModel<LabURI>(behaviourSlot, true));
        }
        foreach (var objectSlot in data.ObjectSlots)
        {
            _objectSlots.Add(new PrimitiveWrapperViewModel<LabURI>(objectSlot, true));
        }
        foreach (var soundSlot in data.SoundSlots)
        {
            _soundSlots.Add(new PrimitiveWrapperViewModel<LabURI>(soundSlot, true));
        }
        
        _instFlags = new BindableCollection<PrimitiveWrapperViewModel<uint>>();
        _instFloats = new BindableCollection<PrimitiveWrapperViewModel<float>>();
        _instIntegers = new BindableCollection<PrimitiveWrapperViewModel<uint>>();
        DirtyTracker.AddBindableCollection(_instFlags);
        DirtyTracker.AddBindableCollection(_instFloats);
        DirtyTracker.AddBindableCollection(_instIntegers);
        foreach (var instFlags in data.InstFlags)
        {
            _instFlags.Add(new PrimitiveWrapperViewModel<uint>(instFlags, true));
        }
        foreach (var instFlags in data.InstFloats)
        {
            _instFloats.Add(new PrimitiveWrapperViewModel<float>(instFlags, true));
        }
        foreach (var instFlags in data.InstIntegers)
        {
            _instIntegers.Add(new PrimitiveWrapperViewModel<uint>(instFlags, true));
        }
    }

    protected override void Save()
    {
        var assetManager = AssetManager.Get();
        var data = assetManager.GetAssetData<GameObjectData>(EditableResource);
        data.Name = _name;
        data.UnkTypeValue = _unkTypeValue;
        data.CameraReactJointAmount = _cameraReactJointAmount;
        data.ExitPointAmount = _exitPointAmount;
        data.InstanceStateFlags = (Enums.InstanceState)_instanceStateFlags.StateFlags;
        data.BehaviourPack = _commandPack.Code.Text;
        
        data.OGISlots.Clear();
        data.AnimationSlots.Clear();
        data.BehaviourSlots.Clear();
        data.ObjectSlots.Clear();
        data.SoundSlots.Clear();
        foreach (var ogiSlot in _ogiSlots)
        {
            data.OGISlots.Add(ogiSlot.Value);
        }
        foreach (var animSlot in _animationSlots)
        {
            data.AnimationSlots.Add(animSlot.Value);
        }
        foreach (var behaviourSlot in _behaviourSlots)
        {
            data.BehaviourSlots.Add(behaviourSlot.Value);
        }
        foreach (var objectSlot in _objectSlots)
        {
            data.ObjectSlots.Add(objectSlot.Value);
        }
        foreach (var soundSlot in _soundSlots)
        {
            data.SoundSlots.Add(soundSlot.Value);
        }
        
        data.InstFlags.Clear();
        data.InstFloats.Clear();
        data.InstIntegers.Clear();
        foreach (var instFlag in _instFlags)
        {
            data.InstFlags.Add(instFlag.Value);
        }
        foreach (var instFloat in _instFloats)
        {
            data.InstFloats.Add(instFloat.Value);
        }
        foreach (var instInteger in _instIntegers)
        {
            data.InstIntegers.Add(instInteger.Value);
        }
    }

    [MarkDirty]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public ITwinObject.ObjectType Type
    {
        get => _type;
        set
        {
            if (_type != value)
            {
                _type = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public IEnumerable<ITwinObject.ObjectType> ObjectTypes { get; } = Enum.GetValues<ITwinObject.ObjectType>();

    public ICommand BehaviourFilterCommand => _behaviourFilterCommand;

    public ObservableCollection<LabURI> BehaviourReferencesBrowser => new(AssetManager.Get().GetAllAssetUrisOf<Assets.Code.Behaviour>().AddRange(AssetManager.Get().GetAllAssetUrisOf<BehaviourCommandsSequence>()));

    [MarkDirty]
    public byte UnkTypeValue
    {
        get => _unkTypeValue;
        set
        {
            if (_unkTypeValue != value)
            {
                _unkTypeValue = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    public string UnkTypeHintString => """
                                       CHANGE THIS AT YOUR OWN RISK!
                                       For Pickup type objects this value must be 16 or 17
                                       For the rest it is unknown so look at other object types!
                                       Changing this to a bad value could potentially crash the game!
                                       """;

    [MarkDirty]
    public byte CameraReactJointAmount
    {
        get => _cameraReactJointAmount;
        set
        {
            if (_cameraReactJointAmount != value)
            {
                _cameraReactJointAmount = value;
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public byte ExitPointAmount
    {
        get => _exitPointAmount;
        set
        {
            if (_exitPointAmount != value)
            {
                _exitPointAmount = value;
                NotifyOfPropertyChange();
            }
        }
    }

    public InstanceStateFlagsViewModel StateFlags => _instanceStateFlags;

    public ViewportViewModel ObjectScene { get; }

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> OgiSlots => _ogiSlots;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> AnimationSlots => _animationSlots;

    public int SelectedAnimationOgiPairSlot
    {
        get => _selectedAnimationOgiPairSlot;
        set
        {
            if (_selectedAnimationOgiPairSlot != value)
            {
                _selectedAnimationOgiPairSlot = value;
                // ObjectScene.ResetScene();
                NotifyOfPropertyChange();
            }
        }
    }

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> BehaviourSlots => _behaviourSlots;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> ObjectSlots => _objectSlots;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> SoundSlots => _soundSlots;

    public BindableCollection<PrimitiveWrapperViewModel<UInt32>> InstFlags => _instFlags;

    public BindableCollection<PrimitiveWrapperViewModel<Single>> InstFloats => _instFloats;

    public BindableCollection<PrimitiveWrapperViewModel<UInt32>> InstIntegers => _instIntegers;
    public BehaviourCommandPackViewModel CommandPack => _commandPack;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> AllBehaviours
    {
        get
        {
            var allGraphs = AssetManager.Get().GetAllAssetUrisOf<BehaviourGraph>();
            var allSequences = AssetManager.Get().GetAllAssetUrisOf<BehaviourCommandsSequence>();
            return new BindableCollection<PrimitiveWrapperViewModel<LabURI>>(allGraphs.Concat(allSequences).Select(graph => new PrimitiveWrapperViewModel<LabURI>(graph)));
        }
    }
}