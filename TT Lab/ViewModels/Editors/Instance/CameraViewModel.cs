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
        private Vector4ViewModel leftoverVector1;
        private Vector4ViewModel leftoverVector2;
        private Single leftoverFloat1;
        private Single leftoverFloat2;
        private UInt32 fovStart;
        private UInt32 fovEnd;
        private UInt32 pitchStart;
        private UInt32 pitchEnd;
        private UInt32 yawStart;
        private UInt32 yawEnd;
        private Single distanceStart;
        private Single distanceEnd;
        private Single camera2Value;
        private Single camera1Value;
        private UInt32 yawExtra;
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
            data.LeftoverFloat1 = LeftoverFloat1;
            data.LeftoverFloat2 = LeftoverFloat2;
            data.DistanceStart = DistanceStart;
            data.DistanceEnd = DistanceEnd;
            data.Camera2Value = Camera2Value;
            data.Camera1Value = Camera1Value;
            data.BlendInDistance = BlendInDistance;
            data.LeftoverVector1 = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = LeftoverVector1.X,
                Y = LeftoverVector1.Y,
                Z = LeftoverVector1.Z,
                W = LeftoverVector1.W,
            };
            data.LeftoverVector2 = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = LeftoverVector2.X,
                Y = LeftoverVector2.Y,
                Z = LeftoverVector2.Z,
                W = LeftoverVector2.W,
            };
            data.FovStart = FovStart;
            data.FovEnd = FovEnd;
            data.PitchStart = PitchStart;
            data.PitchEnd = PitchEnd;
            data.YawStart = YawStart;
            data.YawEnd = YawEnd;
            data.YawExtra = YawExtra;
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
            leftoverVector1 = new Vector4ViewModel(data.LeftoverVector1);
            leftoverVector2 = new Vector4ViewModel(data.LeftoverVector2);
            DirtyTracker.AddChild(leftoverVector1);
            DirtyTracker.AddChild(leftoverVector2);
            ActivateItemAsync(leftoverVector1);
            ActivateItemAsync(leftoverVector2);
            leftoverFloat1 = data.LeftoverFloat1;
            leftoverFloat2 = data.LeftoverFloat2;
            fovStart = data.FovStart;
            fovEnd = data.FovEnd;
            pitchStart = data.PitchStart;
            pitchEnd = data.PitchEnd;
            yawStart = data.YawStart;
            yawEnd = data.YawEnd;
            distanceStart = data.DistanceStart;
            distanceEnd = data.DistanceEnd;
            camera2Value = data.Camera2Value;
            camera1Value = data.Camera1Value;
            yawExtra = data.YawExtra;
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

        public Vector4ViewModel LeftoverVector1
        {
            get => leftoverVector1;
        }

        public Vector4ViewModel LeftoverVector2
        {
            get => leftoverVector2;
        }

        [MarkDirty]
        public Single LeftoverFloat1
        {
            get => leftoverFloat1;
            set
            {
                if (leftoverFloat1 != value)
                {
                    leftoverFloat1 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single LeftoverFloat2
        {
            get => leftoverFloat2;
            set
            {
                if (leftoverFloat2 != value)
                {
                    leftoverFloat2 = value;
                    
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
        public Single Camera2Value
        {
            get => camera2Value;
            set
            {
                if (camera2Value != value)
                {
                    camera2Value = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single Camera1Value
        {
            get => camera1Value;
            set
            {
                if (camera1Value != value)
                {
                    camera1Value = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 YawExtra
        {
            get => yawExtra;
            set
            {
                if (yawExtra != value)
                {
                    yawExtra = value;
                    
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
