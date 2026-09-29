using Caliburn.Micro;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance.Cameras
{
    public class CameraPathViewModel : BaseCameraViewModel
    {
        private BindableCollection<Vector4ViewModel> pathPoints;
        private BindableCollection<Vector2ViewModel> parameters;

        public CameraPathViewModel()
        {
            CameraType = ITwinCamera.CameraType.CameraPath;
            pathPoints = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(pathPoints);
            parameters = new BindableCollection<Vector2ViewModel>();
            DirtyTracker.AddBindableCollection(parameters);
        }

        public CameraPathViewModel(CameraSubBase cam) : base(cam)
        {
            var baseCam = (CameraPath)cam;
            pathPoints = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(pathPoints);
            foreach (var v in baseCam.PathPoints)
            {
                pathPoints.Add(new Vector4ViewModel(v));
            }
            
            parameters = new BindableCollection<Vector2ViewModel>();
            DirtyTracker.AddBindableCollection(parameters);
            foreach (var d in baseCam.Parameters)
            {
                parameters.Add(new Vector2ViewModel(d));
            }
        }

        public override void Save(CameraSubBase? cam)
        {
            cam ??= new CameraPath();
            var pathCam = (CameraPath)cam;
            pathCam.PathPoints.Clear();
            foreach (var p in PathPoints)
            {
                pathCam.PathPoints.Add(new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = p.X,
                    Y = p.Y,
                    Z = p.Z,
                    W = p.W
                });
            }
            pathCam.Parameters.Clear();
            foreach (var d in Parameters)
            {
                pathCam.Parameters.Add(new Twinsanity.TwinsanityInterchange.Common.Vector2 { X = d.X, Y = d.Y });
            }
            
            base.Save(cam);
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            foreach (var point in pathPoints)
            {
                ActivateItemAsync(point, cancellationToken);
            }

            return base.OnInitializeAsync(cancellationToken);
        }

        public BindableCollection<Vector4ViewModel> PathPoints
        {
            get => pathPoints;
        }

        public BindableCollection<Vector2ViewModel> Parameters
        {
            get => parameters;
        }
    }
}
