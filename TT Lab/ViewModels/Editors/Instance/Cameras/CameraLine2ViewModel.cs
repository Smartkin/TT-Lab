using System;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance.Cameras
{
    public class CameraLine2ViewModel : BaseCameraViewModel
    {
        private Vector4ViewModel lineStart;
        private Vector4ViewModel lineEnd;
        private Single nearDistance;
        private Single farDistance;

        public CameraLine2ViewModel()
        {
            CameraType = ITwinCamera.CameraType.CameraLine2;
            lineStart = new Vector4ViewModel();
            lineEnd = new Vector4ViewModel();
            DirtyTracker.AddChild(lineStart);
            DirtyTracker.AddChild(lineEnd);
            nearDistance = 0;
            farDistance = 0;
        }

        public CameraLine2ViewModel(CameraSubBase cam) : base(cam)
        {
            var baseCam = (CameraLine2)cam;
            lineStart = new Vector4ViewModel(baseCam.LineStart);
            lineEnd = new Vector4ViewModel(baseCam.LineEnd);
            DirtyTracker.AddChild(lineStart);
            DirtyTracker.AddChild(lineEnd);
            nearDistance = baseCam.NearDistance;
            farDistance = baseCam.FarDistance;
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(lineStart, cancellationToken);
            ActivateItemAsync(lineEnd, cancellationToken);

            return base.OnInitializeAsync(cancellationToken);
        }

        public override void Save(CameraSubBase? cam)
        {
            cam ??= new CameraLine2();
            var lineCam = (CameraLine2)cam;
            lineCam.LineStart = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = LineStart.X,
                Y = LineStart.Y,
                Z = LineStart.Z,
                W = LineStart.W,
            };
            lineCam.LineEnd = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = LineEnd.X,
                Y = LineEnd.Y,
                Z = LineEnd.Z,
                W = LineEnd.W,
            };
            lineCam.NearDistance = NearDistance;
            lineCam.FarDistance = FarDistance;
            base.Save(cam);
        }

        public Vector4ViewModel LineStart
        {
            get => lineStart;
        }

        public Vector4ViewModel LineEnd
        {
            get => lineEnd;
        }

        [MarkDirty]
        public Single NearDistance
        {
            get => nearDistance;
            set
            {
                if (nearDistance != value)
                {
                    nearDistance = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single FarDistance
        {
            get => farDistance;
            set
            {
                if (farDistance != value)
                {
                    farDistance = value;
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
