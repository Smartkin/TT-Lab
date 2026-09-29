using System;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance.Cameras
{
    public class CameraPoint2ViewModel : BaseCameraViewModel
    {
        private Vector4ViewModel point;
        private Single distance;
        private Byte mode;

        public CameraPoint2ViewModel()
        {
            CameraType = ITwinCamera.CameraType.CameraPoint2;
            point = new Vector4ViewModel();
            DirtyTracker.AddChild(point);
            distance = 0;
            mode = 0;
        }

        public CameraPoint2ViewModel(CameraSubBase cam) : base(cam)
        {
            var baseCam = (CameraPoint2)cam;
            point = new Vector4ViewModel(baseCam.Point);
            DirtyTracker.AddChild(point);
            distance = baseCam.Distance;
            mode = baseCam.Mode;
        }

        public override void Save(CameraSubBase? cam)
        {
            cam ??= new CameraPoint2();
            var pCam = (CameraPoint2)cam;
            pCam.Point = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = point.X,
                Y = point.Y,
                Z = point.Z,
                W = point.W,
            };
            pCam.Distance = Distance;
            pCam.Mode = Mode;
            base.Save(cam);
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(point, cancellationToken);

            return base.OnInitializeAsync(cancellationToken);
        }

        public Vector4ViewModel Point
        {
            get => point;
        }

        [MarkDirty]
        public Single Distance
        {
            get => distance;
            set
            {
                if (distance != value)
                {
                    distance = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Byte Mode
        {
            get => mode;
            set
            {
                if (mode != value)
                {
                    mode = value;
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
