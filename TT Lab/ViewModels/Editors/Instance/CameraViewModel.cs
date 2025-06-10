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
        private UInt32 cameraHeader;
        private UInt16 unkShort;
        private Single unkFloat1;
        private Vector4ViewModel unkVector1;
        private Vector4ViewModel unkVector2;
        private Single unkFloat2;
        private Single unkFloat3;
        private UInt32 unkInt1;
        private UInt32 unkInt2;
        private UInt32 unkInt3;
        private UInt32 unkInt4;
        private UInt32 unkInt5;
        private UInt32 unkInt6;
        private Single unkFloat4;
        private Single unkFloat5;
        private Single unkFloat6;
        private Single unkFloat7;
        private UInt32 unkInt7;
        private UInt32 unkInt8;
        private Single unkFloat8;
        private Byte unkByte;
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
            data.CameraHeader = CameraHeader;
            data.UnkShort = UnkShort;
            data.UnkFloat1 = UnkFloat1;
            data.UnkFloat2 = UnkFloat2;
            data.UnkFloat3 = UnkFloat3;
            data.UnkFloat4 = UnkFloat4;
            data.UnkFloat5 = UnkFloat5;
            data.UnkFloat6 = UnkFloat6;
            data.UnkFloat7 = UnkFloat7;
            data.UnkFloat8 = UnkFloat8;
            data.UnkVector1 = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnkVector1.X,
                Y = UnkVector1.Y,
                Z = UnkVector1.Z,
                W = UnkVector1.W,
            };
            data.UnkVector2 = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnkVector2.X,
                Y = UnkVector2.Y,
                Z = UnkVector2.Z,
                W = UnkVector2.W,
            };
            data.UnkInt1 = UnkInt1;
            data.UnkInt2 = UnkInt2;
            data.UnkInt3 = UnkInt3;
            data.UnkInt4 = UnkInt4;
            data.UnkInt5 = UnkInt5;
            data.UnkInt6 = UnkInt6;
            data.UnkInt7 = UnkInt7;
            data.UnkInt8 = UnkInt8;
            data.UnkByte = UnkByte;
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
            cameraHeader = data.CameraHeader;
            unkShort = data.UnkShort;
            unkFloat1 = data.UnkFloat1;
            unkVector1 = new Vector4ViewModel(data.UnkVector1);
            unkVector2 = new Vector4ViewModel(data.UnkVector2);
            DirtyTracker.AddChild(unkVector1);
            DirtyTracker.AddChild(unkVector2);
            ActivateItemAsync(unkVector1);
            ActivateItemAsync(unkVector2);
            unkFloat2 = data.UnkFloat2;
            unkFloat3 = data.UnkFloat3;
            unkInt1 = data.UnkInt1;
            unkInt2 = data.UnkInt2;
            unkInt3 = data.UnkInt3;
            unkInt4 = data.UnkInt4;
            unkInt5 = data.UnkInt5;
            unkInt6 = data.UnkInt6;
            unkFloat4 = data.UnkFloat4;
            unkFloat5 = data.UnkFloat5;
            unkFloat6 = data.UnkFloat6;
            unkFloat7 = data.UnkFloat7;
            unkInt7 = data.UnkInt7;
            unkInt8 = data.UnkInt8;
            unkFloat8 = data.UnkFloat8;
            unkByte = data.UnkByte;
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

        public override Vector4ViewModel Position
        {
            get => Trigger.Position;
            set => Trigger.Position = value;
        }

        public override Vector3ViewModel Rotation
        {
            get => Trigger.Rotation;
            set => Trigger.Rotation = value;
        }

        public override Vector3ViewModel Scale
        {
            get => Trigger.Scale;
            set => Trigger.Scale = value;
        }

        public TriggerViewModel Trigger
        {
            get => trigger;
        }

        [MarkDirty]
        public UInt32 CameraHeader
        {
            get => cameraHeader;
            set
            {
                if (cameraHeader != value)
                {
                    cameraHeader = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 UnkShort
        {
            get => unkShort;
            set
            {
                if (unkShort != value)
                {
                    unkShort = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single UnkFloat1
        {
            get => unkFloat1;
            set
            {
                if (unkFloat1 != value)
                {
                    unkFloat1 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        public Vector4ViewModel UnkVector1
        {
            get => unkVector1;
        }

        public Vector4ViewModel UnkVector2
        {
            get => unkVector2;
        }

        [MarkDirty]
        public Single UnkFloat2
        {
            get => unkFloat2;
            set
            {
                if (unkFloat2 != value)
                {
                    unkFloat2 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single UnkFloat3
        {
            get => unkFloat3;
            set
            {
                if (unkFloat3 != value)
                {
                    unkFloat3 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt1
        {
            get => unkInt1;
            set
            {
                if (unkInt1 != value)
                {
                    unkInt1 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt2
        {
            get => unkInt2;
            set
            {
                if (unkInt2 != value)
                {
                    unkInt2 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt3
        {
            get => unkInt3;
            set
            {
                if (unkInt3 != value)
                {
                    unkInt3 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt4
        {
            get => unkInt4;
            set
            {
                if (unkInt4 != value)
                {
                    unkInt4 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt5
        {
            get => unkInt5;
            set
            {
                if (unkInt5 != value)
                {
                    unkInt5 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt6
        {
            get => unkInt6;
            set
            {
                if (unkInt6 != value)
                {
                    unkInt6 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single UnkFloat4
        {
            get => unkFloat4;
            set
            {
                if (unkFloat4 != value)
                {
                    unkFloat4 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single UnkFloat5
        {
            get => unkFloat5;
            set
            {
                if (unkFloat5 != value)
                {
                    unkFloat5 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single UnkFloat6
        {
            get => unkFloat6;
            set
            {
                if (unkFloat6 != value)
                {
                    unkFloat6 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single UnkFloat7
        {
            get => unkFloat7;
            set
            {
                if (unkFloat7 != value)
                {
                    unkFloat7 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt7
        {
            get => unkInt7;
            set
            {
                if (unkInt7 != value)
                {
                    unkInt7 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt32 UnkInt8
        {
            get => unkInt8;
            set
            {
                if (unkInt8 != value)
                {
                    unkInt8 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Single UnkFloat8
        {
            get => unkFloat8;
            set
            {
                if (unkFloat8 != value)
                {
                    unkFloat8 = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Byte UnkByte
        {
            get => unkByte;
            set
            {
                if (unkByte != value)
                {
                    unkByte = value;
                    
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
