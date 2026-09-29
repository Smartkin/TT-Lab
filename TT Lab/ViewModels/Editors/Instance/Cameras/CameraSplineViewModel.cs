using Caliburn.Micro;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance.Cameras
{
    public class CameraSplineViewModel : BaseCameraViewModel
    {
        private Single stepLength;
        private BindableCollection<Vector4ViewModel> pathPoints;
        private BindableCollection<Vector4ViewModel> tangents;
        private BindableCollection<Vector2ViewModel> parameters;
        private UInt16 splineFlags;

        public CameraSplineViewModel()
        {
            CameraType = ITwinCamera.CameraType.CameraSpline;
            pathPoints = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(pathPoints);
            
            tangents = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(tangents);

            parameters = new BindableCollection<Vector2ViewModel>();
            DirtyTracker.AddBindableCollection(parameters);
            splineFlags = 0;
        }

        public CameraSplineViewModel(CameraSubBase cam) : base(cam)
        {
            var baseCam = (CameraSpline)cam;
            stepLength = baseCam.StepLength;
            pathPoints = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(pathPoints);
            foreach (var v in baseCam.PathPoints)
            {
                pathPoints.Add(new Vector4ViewModel(v));
            }
            tangents = new BindableCollection<Vector4ViewModel>();
            DirtyTracker.AddBindableCollection(tangents);
            foreach (var v in baseCam.Tangents)
            {
                tangents.Add(new Vector4ViewModel(v));
            }
            parameters = new BindableCollection<Vector2ViewModel>();
            DirtyTracker.AddBindableCollection(parameters);
            foreach (var d in baseCam.Parameters)
            {
                parameters.Add(new Vector2ViewModel(d));
            }
            splineFlags = baseCam.SplineFlags;
        }

        public override void Save(CameraSubBase? cam)
        {
            cam ??= new CameraSpline();
            var splineCam = (CameraSpline)cam;
            splineCam.StepLength = StepLength;
            splineCam.SplineFlags = SplineFlags;
            splineCam.PathPoints.Clear();
            foreach (var p in PathPoints)
            {
                splineCam.PathPoints.Add(new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = p.X,
                    Y = p.Y,
                    Z = p.Z,
                    W = p.W,
                });
            }
            splineCam.Tangents.Clear();
            foreach (var ip in Tangents)
            {
                splineCam.Tangents.Add(new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = ip.X,
                    Y = ip.Y,
                    Z = ip.Z,
                    W = ip.W,
                });
            }
            splineCam.Parameters.Clear();
            foreach (var d in Parameters)
            {
                splineCam.Parameters.Add(new Twinsanity.TwinsanityInterchange.Common.Vector2
                {
                    X = d.X,
                    Y = d.Y
                });
            }
            base.Save(cam);
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            foreach (var point in pathPoints)
            {
                ActivateItemAsync(point, cancellationToken);
            }

            foreach (var interPoint in tangents)
            {
                ActivateItemAsync(interPoint, cancellationToken);
            }

            foreach (var data in parameters)
            {
                ActivateItemAsync(data, cancellationToken);
            }

            return base.OnInitializeAsync(cancellationToken);
        }

        [MarkDirty]
        public Single StepLength
        {
            get => stepLength;
            set
            {
                if (stepLength != value)
                {
                    stepLength = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public BindableCollection<Vector4ViewModel> PathPoints
        {
            get => pathPoints;
        }

        public BindableCollection<Vector4ViewModel> Tangents
        {
            get => tangents;
        }

        public BindableCollection<Vector2ViewModel> Parameters
        {
            get => parameters;
        }

        [MarkDirty]
        public UInt16 SplineFlags
        {
            get => splineFlags;
            set
            {
                if (splineFlags != value)
                {
                    splineFlags = value;
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
