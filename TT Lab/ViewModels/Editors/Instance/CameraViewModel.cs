using System;
using System.Collections.Generic;
using Caliburn.Micro;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Instance.Cameras;
using Twinsanity.TwinsanityInterchange.Enumerations;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance
{
    public class CameraViewModel : ViewportEditableInstanceViewModel
    {
        private static readonly Dictionary<ITwinCamera.CameraType, Type> subIdToCamVM = new Dictionary<ITwinCamera.CameraType, Type>();

        private TriggerViewModel trigger;
        private UInt32 flags;
        private UInt16 switches;
        private Single blendTime;
        private Vector4ViewModel targetBoxMin;
        private Vector4ViewModel targetBoxMax;
        private Single framingDistance;
        private Single framingShare;
        private UInt32 fovStart;
        private UInt32 fovEnd;
        private UInt32 pitchStart;
        private UInt32 pitchEnd;
        private UInt32 yawStart;
        private UInt32 yawEnd;
        private Single distanceStart;
        private Single distanceEnd;
        private Single positionFollowRate;
        private Single targetFollowRate;
        private UInt32 yawSpeed;
        private UInt32 blendInYaw;
        private Single blendInDistance;
        private Byte group;
        private ITwinCamera.CameraType cameraType1 = ITwinCamera.CameraType.Null;
        private ITwinCamera.CameraType cameraType2 = ITwinCamera.CameraType.Null;
        private BaseCameraViewModel? mainCamera1;
        private BaseCameraViewModel? mainCamera2;

        static CameraViewModel()
        {
            subIdToCamVM.Add(ITwinCamera.CameraType.BossCamera, typeof(BossCameraViewModel));
            subIdToCamVM.Add(ITwinCamera.CameraType.CameraPoint, typeof(CameraPointViewModel));
            subIdToCamVM.Add(ITwinCamera.CameraType.CameraLine, typeof(CameraLineViewModel));
            subIdToCamVM.Add(ITwinCamera.CameraType.CameraPath, typeof(CameraPathViewModel));
            subIdToCamVM.Add(ITwinCamera.CameraType.CameraSpline, typeof(CameraSplineViewModel));
            subIdToCamVM.Add(ITwinCamera.CameraType.CameraPoint2, typeof(CameraPoint2ViewModel));
            subIdToCamVM.Add(ITwinCamera.CameraType.CameraLine2, typeof(CameraLine2ViewModel));
            subIdToCamVM.Add(ITwinCamera.CameraType.CameraZone, typeof(CameraZoneViewModel));
        }

        protected override void Save()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            asset.LayoutID = (int)Trigger.LayoutID;
            var data = asset.GetData<CameraData>();
            Trigger.Save(data.Trigger);
            data.Flags = (ITwinCamera.CameraFlags)Flags;
            data.Switches = (ITwinCamera.CameraSwitches)Switches;
            data.BlendTime = BlendTime;
            data.FramingDistance = FramingDistance;
            data.FramingShare = FramingShare;
            data.DistanceStart = DistanceStart;
            data.DistanceEnd = DistanceEnd;
            data.PositionFollowRate = PositionFollowRate;
            data.TargetFollowRate = TargetFollowRate;
            data.BlendInDistance = BlendInDistance;
            data.TargetBoxMin = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = TargetBoxMin.X,
                Y = TargetBoxMin.Y,
                Z = TargetBoxMin.Z,
                W = TargetBoxMin.W,
            };
            data.TargetBoxMax = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = TargetBoxMax.X,
                Y = TargetBoxMax.Y,
                Z = TargetBoxMax.Z,
                W = TargetBoxMax.W,
            };
            data.FovStart = FovStart;
            data.FovEnd = FovEnd;
            data.PitchStart = PitchStart;
            data.PitchEnd = PitchEnd;
            data.YawStart = YawStart;
            data.YawEnd = YawEnd;
            data.YawSpeed = YawSpeed;
            data.BlendInYaw = BlendInYaw;
            data.Group = Group;
            if (MainCamera1 != null)
            {
                MainCamera1.Save(data.MainCamera1);
            }
            else
            {
                data.MainCamera1 = null;
            }
            if (MainCamera2 != null)
            {
                MainCamera2.Save(data.MainCamera2);
            }
            else
            {
                data.MainCamera2 = null;
            }
            
            base.Save();
        }

        public override void LoadData()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            var data = asset.GetData<CameraData>();
            trigger = new TriggerViewModel(MiscUtils.ConvertEnum<Enums.Layouts>(asset.LayoutID!.Value), data.Trigger);
            DirtyTracker.AddChild(trigger);
            flags = (UInt32)data.Flags;
            switches = (UInt16)data.Switches;
            blendTime = data.BlendTime;
            targetBoxMin = new Vector4ViewModel(data.TargetBoxMin);
            targetBoxMax = new Vector4ViewModel(data.TargetBoxMax);
            DirtyTracker.AddChild(targetBoxMin);
            DirtyTracker.AddChild(targetBoxMax);
            ActivateItemAsync(targetBoxMin);
            ActivateItemAsync(targetBoxMax);
            framingDistance = data.FramingDistance;
            framingShare = data.FramingShare;
            fovStart = data.FovStart;
            fovEnd = data.FovEnd;
            pitchStart = data.PitchStart;
            pitchEnd = data.PitchEnd;
            yawStart = data.YawStart;
            yawEnd = data.YawEnd;
            distanceStart = data.DistanceStart;
            distanceEnd = data.DistanceEnd;
            positionFollowRate = data.PositionFollowRate;
            targetFollowRate = data.TargetFollowRate;
            yawSpeed = data.YawSpeed;
            blendInYaw = data.BlendInYaw;
            blendInDistance = data.BlendInDistance;
            group = data.Group;
            CameraType1 = ITwinCamera.CameraType.Null;
            CameraType2 = ITwinCamera.CameraType.Null;
            if (data.MainCamera1 != null && subIdToCamVM.ContainsKey(data.MainCamera1.GetCameraType()))
            {
                CameraType1 = data.MainCamera1.GetCameraType();
                MainCamera1 = (BaseCameraViewModel)Activator.CreateInstance(subIdToCamVM[data.MainCamera1.GetCameraType()], data.MainCamera1)!;
                DirtyTracker.AddChild(MainCamera1);
            }
            if (data.MainCamera2 != null && subIdToCamVM.ContainsKey(data.MainCamera2.GetCameraType()))
            {
                CameraType2 = data.MainCamera2.GetCameraType();
                MainCamera2 = (BaseCameraViewModel)Activator.CreateInstance(subIdToCamVM[data.MainCamera2.GetCameraType()], data.MainCamera2)!;
                DirtyTracker.AddChild(MainCamera2);
            }
        }

        public override Vector4ViewModel Position => Trigger.Position;

        public override Vector3ViewModel Rotation => Trigger.Rotation;

        public override Vector3ViewModel Scale => Trigger.Scale;

        public TriggerViewModel Trigger
        {
            get => trigger;
        }

        [MarkDirty]
        public UInt32 Flags
        {
            get => flags;
            set
            {
                if (flags != value)
                {
                    flags = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 Switches
        {
            get => switches;
            set
            {
                if (switches != value)
                {
                    switches = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single BlendTime
        {
            get => blendTime;
            set
            {
                if (blendTime != value)
                {
                    blendTime = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        public Vector4ViewModel TargetBoxMin
        {
            get => targetBoxMin;
        }

        public Vector4ViewModel TargetBoxMax
        {
            get => targetBoxMax;
        }

        [MarkDirty]
        public Single FramingDistance
        {
            get => framingDistance;
            set
            {
                if (framingDistance != value)
                {
                    framingDistance = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single FramingShare
        {
            get => framingShare;
            set
            {
                if (framingShare != value)
                {
                    framingShare = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 FovStart
        {
            get => fovStart;
            set
            {
                if (fovStart != value)
                {
                    fovStart = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 FovEnd
        {
            get => fovEnd;
            set
            {
                if (fovEnd != value)
                {
                    fovEnd = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 PitchStart
        {
            get => pitchStart;
            set
            {
                if (pitchStart != value)
                {
                    pitchStart = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 PitchEnd
        {
            get => pitchEnd;
            set
            {
                if (pitchEnd != value)
                {
                    pitchEnd = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 YawStart
        {
            get => yawStart;
            set
            {
                if (yawStart != value)
                {
                    yawStart = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 YawEnd
        {
            get => yawEnd;
            set
            {
                if (yawEnd != value)
                {
                    yawEnd = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single DistanceStart
        {
            get => distanceStart;
            set
            {
                if (distanceStart != value)
                {
                    distanceStart = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single DistanceEnd
        {
            get => distanceEnd;
            set
            {
                if (distanceEnd != value)
                {
                    distanceEnd = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single PositionFollowRate
        {
            get => positionFollowRate;
            set
            {
                if (positionFollowRate != value)
                {
                    positionFollowRate = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single TargetFollowRate
        {
            get => targetFollowRate;
            set
            {
                if (targetFollowRate != value)
                {
                    targetFollowRate = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 YawSpeed
        {
            get => yawSpeed;
            set
            {
                if (yawSpeed != value)
                {
                    yawSpeed = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 BlendInYaw
        {
            get => blendInYaw;
            set
            {
                if (blendInYaw != value)
                {
                    blendInYaw = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single BlendInDistance
        {
            get => blendInDistance;
            set
            {
                if (blendInDistance != value)
                {
                    blendInDistance = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Byte Group
        {
            get => group;
            set
            {
                if (group != value)
                {
                    group = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        public BaseCameraViewModel? MainCamera1
        {
            get => mainCamera1;
            set => mainCamera1 = value;
        }

        public BaseCameraViewModel? MainCamera2
        {
            get => mainCamera2;
            set => mainCamera2 = value;
        }
        
        public BindableCollection<object> CameraTypes => ViewModelUtil.CameraTypes;

        public ITwinCamera.CameraType CameraType1
        {
            get => cameraType1;
            set
            {
                if (cameraType1 == value)
                {
                    return;
                }
                
                cameraType1 = value;
                if (IsDataLoaded)
                {
                    UpdateCameraViewModel(ref mainCamera1, nameof(MainCamera1), cameraType1);
                }

                NotifyOfPropertyChange();
            }
        }

        public ITwinCamera.CameraType CameraType2
        {
            get => cameraType2;
            set
            {
                if (cameraType2 == value)
                {
                    return;
                }
                
                cameraType2 = value;
                if (IsDataLoaded)
                {
                    UpdateCameraViewModel(ref mainCamera2, nameof(MainCamera2), cameraType2);
                }

                NotifyOfPropertyChange();
            }
        }

        private void UpdateCameraViewModel(ref BaseCameraViewModel? cameraViewModel, string nameOfCameraProp, ITwinCamera.CameraType cameraType)
        {
            if (subIdToCamVM.TryGetValue(cameraType, out var cameraViewModelType))
            {
                if (cameraViewModel != null)
                {
                    DirtyTracker.RemoveChild(cameraViewModel);
                }
                
                cameraViewModel = (BaseCameraViewModel)Activator.CreateInstance(cameraViewModelType)!;
                DirtyTracker.AddChild(cameraViewModel);
                ActivateItemAsync(cameraViewModel);
                NotifyOfPropertyChange(nameOfCameraProp);
            }
            else if (cameraViewModel != null)
            {
                DeactivateItemAsync(cameraViewModel, true);
                DirtyTracker.RemoveChild(cameraViewModel);
                cameraViewModel = null;
                NotifyOfPropertyChange(nameOfCameraProp);
            }
        }
    }
}
