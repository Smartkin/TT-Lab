using Caliburn.Micro;
using System;
using System.Numerics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Vector3 = Twinsanity.TwinsanityInterchange.Common.Vector3;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.ViewModels.Editors.Instance
{
    public class TriggerViewModel : ViewportEditableInstanceViewModel
    {
        private Enums.TriggerActivatorObjects _objActivatorMask;
        private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _instances = new();
        private UInt32 _header;
        private Single _unkFloat;
        private Enums.Layouts _layId;
        private UInt16 _triggerMessage1;
        private UInt16 _triggerMessage2;
        private UInt16 _triggerMessage3;
        private UInt16 _triggerMessage4;
        
        public TriggerViewModel()
        {}

        public TriggerViewModel(Enums.Layouts layoutId, TriggerData data)
        {
            ConstructFromData(layoutId, data);
        }

        protected override void Save()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            asset.LayoutID = (int)LayoutID;
            var data = asset.GetData<TriggerData>();
            Save(data);
            
            base.Save();
        }

        public override void LoadData()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            var data = asset.GetData<TriggerData>();
            ConstructFromData(MiscUtils.ConvertEnum<Enums.Layouts>(asset.LayoutID), data);
        }

        public void Save(TriggerData data)
        {
            data.ObjectActivatorMask = ObjectActivatorMask;
            data.Header = Header;
            data.UnkFloat = UnkFloat;
            Position.Save(data.Position);
            var newScale = new Vector3();
            Scale.Save(newScale);
            data.Scale = new Vector4(newScale.X, newScale.Y, newScale.Z, 1);
            var quat = Quaternion.CreateFromYawPitchRoll(Rotation.X, Rotation.Y, Rotation.Z);
            data.Rotation = new Vector4(quat.X, quat.Y, quat.Z, quat.W);
            data.Instances.Clear();
            foreach (var inst in Instances)
            {
                data.Instances.Add(inst.Value);
            }
            data.TriggerMessage1 = TriggerMessage1;
            data.TriggerMessage2 = TriggerMessage2;
            data.TriggerMessage3 = TriggerMessage3;
            data.TriggerMessage4 = TriggerMessage4;
        }

        private void ConstructFromData(Enums.Layouts layoutId, TriggerData data)
        {
            _objActivatorMask = MiscUtils.ConvertEnum<Enums.TriggerActivatorObjects>(data.ObjectActivatorMask);
            _instances = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
            DirtyTracker.AddBindableCollection(_instances);
            foreach (var inst in data.Instances)
            {
                _instances.Add(new PrimitiveWrapperViewModel<LabURI>(inst));
            }
            Position = new Vector4ViewModel(data.Position);
            Rotation = new Vector3ViewModel(data.Rotation.ToEulerAngles());
            var scaleVector = new Vector3(data.Scale.X, data.Scale.Y, data.Scale.Z);
            Scale = new Vector3ViewModel(scaleVector);
            DirtyTracker.AddChild(Position);
            DirtyTracker.AddChild(Rotation);
            DirtyTracker.AddChild(Scale);
            _header = data.Header;
            _unkFloat = data.UnkFloat;
            _triggerMessage1 = data.TriggerMessage1;
            _triggerMessage2 = data.TriggerMessage2;
            _triggerMessage3 = data.TriggerMessage3;
            _triggerMessage4 = data.TriggerMessage4;
            _layId = layoutId;
            DeleteInstanceFromListCommand = new DeleteItemFromListCommand(_instances);
        }

        public DeleteItemFromListCommand DeleteInstanceFromListCommand { get; private set; }

        [MarkDirty]
        public Enums.Layouts LayoutID
        {
            get => _layId;
            set
            {
                if (_layId != value)
                {
                    _layId = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Enums.TriggerActivatorObjects ObjectActivatorMask
        {
            get => _objActivatorMask;
            set
            {
                if (value != _objActivatorMask)
                {
                    _objActivatorMask = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByPlayableCharacter
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.PlayableCharacter);
            set
            {
                if (value != ActivateByPlayableCharacter)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.PlayableCharacter, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByPickups
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.Pickups);
            set
            {
                if (value != ActivateByPickups)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.Pickups, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByCrates
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.Crates);
            set
            {
                if (value != ActivateByCrates)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.Crates, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByCreatures
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.Creatures);
            set
            {
                if (value != ActivateByCreatures)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.Creatures, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByGenericObjects
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.GenericObjects);
            set
            {
                if (value != ActivateByGenericObjects)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.GenericObjects, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByGrabbables
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.Grabbables);
            set
            {
                if (value != ActivateByGrabbables)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.Grabbables, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByPayGates
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.PayGates);
            set
            {
                if (value != ActivateByPayGates)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.PayGates, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByGraples
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.Graples);
            set
            {
                if (value != ActivateByGraples)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.Graples, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        [MarkDirty]
        public Boolean ActivateByProjectiles
        {
            get => _objActivatorMask.HasFlag(Enums.TriggerActivatorObjects.Projectiles);
            set
            {
                if (value != ActivateByProjectiles)
                {
                    _objActivatorMask = _objActivatorMask.ChangeFlag(Enums.TriggerActivatorObjects.Projectiles, value);
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ObjectActivatorMask));
                }
            }
        }

        public BindableCollection<PrimitiveWrapperViewModel<LabURI>> Instances
        {
            get => _instances;
        }

        [MarkDirty]
        public UInt32 Header
        {
            get => _header;
            set
            {
                if (value != _header)
                {
                    _header = value;
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(TriggerArgument1Enabled));
                    NotifyOfPropertyChange(nameof(TriggerArgument2Enabled));
                    NotifyOfPropertyChange(nameof(TriggerArgument3Enabled));
                    NotifyOfPropertyChange(nameof(TriggerArgument4Enabled));
                }
            }
        }

        [MarkDirty]
        public Boolean TriggerArgument1Enabled
        {
            get => (_header >> 0xB & 0x1) != 0;
            set
            {
                if (value != TriggerArgument1Enabled)
                {
                    if (value)
                    {
                        _header |= 1 << 0xB;
                    }
                    else
                    {
                        var mask = ~(1 << 0xB);
                        _header &= (UInt32)mask;
                    }
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(Header));
                }
            }
        }

        [MarkDirty]
        public Boolean TriggerArgument2Enabled
        {
            get => (_header >> 0x8 & 0x1) != 0;
            set
            {
                if (value != TriggerArgument2Enabled)
                {
                    if (value)
                    {
                        _header |= 1 << 0x8;
                    }
                    else
                    {
                        var mask = ~(1 << 0x8);
                        _header &= (UInt32)mask;
                    }
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(Header));
                }
            }
        }

        [MarkDirty]
        public Boolean TriggerArgument3Enabled
        {
            get => (_header >> 0x9 & 0x1) != 0;
            set
            {
                if (value != TriggerArgument3Enabled)
                {
                    if (value)
                    {
                        _header |= 1 << 0x9;
                    }
                    else
                    {
                        var mask = ~(1 << 0x9);
                        _header &= (UInt32)mask;
                    }
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(Header));
                }
            }
        }

        [MarkDirty]
        public Boolean TriggerArgument4Enabled
        {
            get => (_header >> 0xA & 0x1) != 0;
            set
            {
                if (value != TriggerArgument4Enabled)
                {
                    if (value)
                    {
                        _header |= 1 << 0xA;
                    }
                    else
                    {
                        var mask = ~(1 << 0xA);
                        _header &= (UInt32)mask;
                    }
                    
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(Header));
                }
            }
        }

        [MarkDirty]
        public Single UnkFloat
        {
            get => _unkFloat;
            set
            {
                if (value != _unkFloat)
                {
                    _unkFloat = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 TriggerMessage1
        {
            get => _triggerMessage1;
            set
            {
                if (value != _triggerMessage1)
                {
                    _triggerMessage1 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 TriggerMessage2
        {
            get => _triggerMessage2;
            set
            {
                if (value != _triggerMessage2)
                {
                    _triggerMessage2 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 TriggerMessage3
        {
            get => _triggerMessage3;
            set
            {
                if (value != _triggerMessage3)
                {
                    _triggerMessage3 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 TriggerMessage4
        {
            get => _triggerMessage4;
            set
            {
                if (value != _triggerMessage4)
                {
                    _triggerMessage4 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
