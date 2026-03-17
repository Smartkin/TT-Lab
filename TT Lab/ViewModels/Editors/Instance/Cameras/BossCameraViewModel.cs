using System;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance.Cameras
{
    public class BossCameraViewModel : BaseCameraViewModel
    {
        private Matrix4ViewModel unkMatrix1;
        private Matrix4ViewModel unkMatrix2;
        private Vector4ViewModel unkVec;
        private Byte unkByte1;
        private Single unkFloat3;
        private Single unkFloat4;
        private Single unkFloat5;
        private Single unkFloat6;
        private Byte unkByte2;

        public BossCameraViewModel()
        {
            CameraType = ITwinCamera.CameraType.BossCamera;
            unkMatrix1 = new Matrix4ViewModel();
            unkMatrix2 = new Matrix4ViewModel();
            unkVec = new Vector4ViewModel();
            DirtyTracker.AddChild(unkMatrix1);
            DirtyTracker.AddChild(unkMatrix2);
            DirtyTracker.AddChild(unkVec);
            unkByte1 = 0;
            unkByte2 = 0;
            unkFloat3 = 0;
            unkFloat4 = 0;
            unkFloat5 = 0;
            unkFloat6 = 0;
        }

        public BossCameraViewModel(CameraSubBase cam) : base(cam)
        {
            var bossCam = (BossCamera)cam;
            unkMatrix1 = new Matrix4ViewModel(bossCam.UnkMatrix1);
            unkMatrix2 = new Matrix4ViewModel(bossCam.UnkMatrix2);
            unkVec = new Vector4ViewModel(bossCam.UnkVector);
            DirtyTracker.AddChild(unkMatrix1);
            DirtyTracker.AddChild(unkMatrix2);
            DirtyTracker.AddChild(unkVec);
            unkByte1 = bossCam.UnkByte1;
            unkByte2 = bossCam.UnkByte2;
            unkFloat3 = bossCam.UnkFloat3;
            unkFloat4 = bossCam.UnkFloat4;
            unkFloat5 = bossCam.UnkFloat5;
            unkFloat6 = bossCam.UnkFloat6;
        }

        public override void Save(CameraSubBase? cam)
        {
            cam ??= new BossCamera();
            var bossCam = (BossCamera)cam;
            for (var i = 0; i < 4; ++i)
            {
                bossCam.UnkMatrix1[i] = new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = UnkMatrix1[i].X,
                    Y = UnkMatrix1[i].Y,
                    Z = UnkMatrix1[i].Z,
                    W = UnkMatrix1[i].W,
                };
                bossCam.UnkMatrix2[i] = new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = UnkMatrix2[i].X,
                    Y = UnkMatrix2[i].Y,
                    Z = UnkMatrix2[i].Z,
                    W = UnkMatrix2[i].W,
                };
            }
            bossCam.UnkVector = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnkVec.X,
                Y = UnkVec.Y,
                Z = UnkVec.Z,
                W = UnkVec.W,
            };
            bossCam.UnkByte1 = UnkByte1;
            bossCam.UnkByte2 = UnkByte2;
            bossCam.UnkFloat3 = UnkFloat3;
            bossCam.UnkFloat4 = UnkFloat4;
            bossCam.UnkFloat5 = UnkFloat5;
            bossCam.UnkFloat6 = UnkFloat6;
            base.Save(cam);
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(unkMatrix1, cancellationToken);
            ActivateItemAsync(unkMatrix2, cancellationToken);
            ActivateItemAsync(unkVec, cancellationToken);

            return base.OnInitializeAsync(cancellationToken);
        }

        public Matrix4ViewModel UnkMatrix1
        {
            get => unkMatrix1;
        }

        public Matrix4ViewModel UnkMatrix2
        {
            get => unkMatrix2;
        }

        public Vector4ViewModel UnkVec
        {
            get => unkVec;
        }

        [MarkDirty]
        public Byte UnkByte1
        {
            get => unkByte1;
            set
            {
                if (unkByte1 != value)
                {
                    unkByte1 = value;
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
        public Byte UnkByte2
        {
            get => unkByte2;
            set
            {
                if (unkByte2 != value)
                {
                    unkByte2 = value;
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
