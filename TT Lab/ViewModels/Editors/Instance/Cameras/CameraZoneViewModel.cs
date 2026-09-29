using System.Linq;
using Caliburn.Micro;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance.Cameras
{
    public class CameraZoneViewModel : BaseCameraViewModel
    {
        private BindableCollection<Vector4ViewModel> cameraBoxVectors;
        private BindableCollection<Vector4ViewModel> targetBoxVectors;

        public CameraZoneViewModel()
        {
            CameraType = ITwinCamera.CameraType.CameraZone;
            cameraBoxVectors = new BindableCollection<Vector4ViewModel>();
            targetBoxVectors = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(cameraBoxVectors);
            DirtyTracker.AddBindableCollection(targetBoxVectors);
            for (var i = 0; i < 5; ++i)
            {
                cameraBoxVectors.Add(new Vector4ViewModel());
                targetBoxVectors.Add(new Vector4ViewModel());
            }
        }

        public CameraZoneViewModel(CameraSubBase cam) : base(cam)
        {
            var baseCam = (CameraZone)cam;
            cameraBoxVectors = new BindableCollection<Vector4ViewModel>();
            targetBoxVectors = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(cameraBoxVectors);
            DirtyTracker.AddBindableCollection(targetBoxVectors);
            for (var i = 0; i < 5; ++i)
            {
                cameraBoxVectors.Add(new Vector4ViewModel(baseCam.CameraBox[i]));
                targetBoxVectors.Add(new Vector4ViewModel(baseCam.TargetBox[i]));
            }
        }

        public override void Save(CameraSubBase? cam)
        {
            cam ??= new CameraZone();
            var zoneCam = (CameraZone)cam;
            for (var i = 0; i < 5; ++i)
            {
                zoneCam.CameraBox[i] = new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = CameraBoxVectors[i].X,
                    Y = CameraBoxVectors[i].Y,
                    Z = CameraBoxVectors[i].Z,
                    W = CameraBoxVectors[i].W,
                };
                zoneCam.TargetBox[i] = new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = TargetBoxVectors[i].X,
                    Y = TargetBoxVectors[i].Y,
                    Z = TargetBoxVectors[i].Z,
                    W = TargetBoxVectors[i].W,
                };
            }
            base.Save(cam);
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            for (var i = 0; i < 5; ++i)
            {
                ActivateItemAsync(cameraBoxVectors[i], cancellationToken);
                ActivateItemAsync(targetBoxVectors[i], cancellationToken);
            }

            return base.OnInitializeAsync(cancellationToken);
        }

        public BindableCollection<Vector4ViewModel> CameraBoxVectors
        {
            get => cameraBoxVectors;
        }

        public BindableCollection<Vector4ViewModel> TargetBoxVectors
        {
            get => targetBoxVectors;
        }
    }
}
