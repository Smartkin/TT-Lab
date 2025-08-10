using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Caliburn.Micro;
using org.ogre;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Assets.Code;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Services;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Code.Behaviour;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Code;

namespace TT_Lab.ViewModels.Editors.Code;

public class GameObjectViewModel : ResourceEditorViewModel
{
    private readonly RenderContext _context;
    private readonly TwinSkeletonManager _skeletonManager;
    private readonly MeshService _meshService;
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
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _refObjects;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _refOgis;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _refAnimations;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _refBehaviourCommandSequences;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _refBehaviours;
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _refSounds;
    private BehaviourCommandPackViewModel _commandPack;
    private int _selectedAnimationOgiPairSlot;
    private ICommand _behaviourFilterCommand;

    public GameObjectViewModel(RenderContext context, TwinSkeletonManager skeletonManager, MeshService meshService)
    {
        _context = context;
        _skeletonManager = skeletonManager;
        _meshService = meshService;
        ObjectScene = IoC.Get<ViewportViewModel>();
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
            var ogiRender = new Rendering.Objects.OGI(_context, _skeletonManager, _meshService, ogiData);
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
        
        _refObjects = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _refOgis = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _refAnimations = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _refBehaviourCommandSequences = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _refBehaviours = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        _refSounds = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
        DirtyTracker.AddBindableCollection(_refObjects);
        DirtyTracker.AddBindableCollection(_refOgis);
        DirtyTracker.AddBindableCollection(_refAnimations);
        DirtyTracker.AddBindableCollection(_refBehaviourCommandSequences);
        DirtyTracker.AddBindableCollection(_refBehaviours);
        DirtyTracker.AddBindableCollection(_refSounds);
        foreach (var refObject in data.RefObjects)
        {
            _refObjects.Add(new PrimitiveWrapperViewModel<LabURI>(refObject, true));
        }
        foreach (var refOgi in data.RefOGIs)
        {
            _refOgis.Add(new PrimitiveWrapperViewModel<LabURI>(refOgi, true));
        }
        foreach (var refAnimation in data.RefAnimations)
        {
            _refAnimations.Add(new PrimitiveWrapperViewModel<LabURI>(refAnimation, true));
        }
        foreach (var refCommandSequence in data.RefBehaviourCommandsSequences)
        {
            _refBehaviourCommandSequences.Add(new PrimitiveWrapperViewModel<LabURI>(refCommandSequence, true));
        }
        foreach (var refBehaviour in data.RefBehaviours)
        {
            _refBehaviours.Add(new PrimitiveWrapperViewModel<LabURI>(refBehaviour, true));
        }
        foreach (var refSound in data.RefSounds)
        {
            _refSounds.Add(new PrimitiveWrapperViewModel<LabURI>(refSound, true));
        }
    }

    protected override void Save()
    {
        var data = AssetManager.Get().GetAssetData<GameObjectData>(EditableResource);
        data.Name = _name;
        data.UnkTypeValue = _unkTypeValue;
        data.CameraReactJointAmount = _cameraReactJointAmount;
        data.ExitPointAmount = _exitPointAmount;
        data.InstanceStateFlags = (Enums.InstanceState)_instanceStateFlags.StateFlags;
        _commandPack.Save(data.BehaviourPack);
        
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

        var assetManager = AssetManager.Get();
        var refObjects = ObjectSlots.Distinct().Select(b => b.Value != LabURI.Empty);
        var refOgis = OgiSlots.Distinct().Select(b => b.Value != LabURI.Empty);
        var refAnimations = AnimationSlots.Distinct().Select(b => b.Value != LabURI.Empty);
        var refSounds = SoundSlots.Distinct().Select(b => b.Value != LabURI.Empty);
        var refBehaviourSequences = BehaviourSlots.Select(b => assetManager.GetAsset(b.Value) is BehaviourCommandsSequence);
        
        data.RefObjects.Clear();
        data.RefOGIs.Clear();
        data.RefAnimations.Clear();
        data.RefBehaviours.Clear();
        data.RefSounds.Clear();
        foreach (var refObject in _refObjects)
        {
            data.RefObjects.Add(refObject.Value);
        }
        foreach (var refObject in _refOgis)
        {
            data.RefOGIs.Add(refObject.Value);
        }
        foreach (var refObject in _refAnimations)
        {
            data.RefAnimations.Add(refObject.Value);
        }
        foreach (var refObject in _refBehaviours)
        {
            data.RefBehaviours.Add(refObject.Value);
        }
        foreach (var refObject in _refSounds)
        {
            data.RefSounds.Add(refObject.Value);
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
                                       For Pickup type objects this value must by 16 or 17
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

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RefObjects => _refObjects;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RefOgis => _refOgis;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RefAnimations => _refAnimations;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RefBehaviourCommandSequences => _refBehaviourCommandSequences;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RefBehaviours => _refBehaviours;

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RefAllBehaviours
    {
        get
        {
            var collection = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>(_refBehaviours);
            collection.AddRange(_refBehaviourCommandSequences);
            return collection;
        }
    }

    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RefSounds => _refSounds;

    public BehaviourCommandPackViewModel CommandPack => _commandPack;
}