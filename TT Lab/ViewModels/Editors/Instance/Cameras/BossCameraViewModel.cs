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
        private Matrix4ViewModel worldToArena;
        private Matrix4ViewModel arenaToWorld;
        private Vector4ViewModel unkVec;
        private Boolean usesDistanceCurves;
        private Single radiusBlend;
        private Single nearHeightOffset;
        private Single farHeightOffset;
        private Single maxTurnRate;
        private Boolean distanceIncludesHeight;

        public BossCameraViewModel()
        {
            CameraType = ITwinCamera.CameraType.BossCamera;
            worldToArena = new Matrix4ViewModel();
            arenaToWorld = new Matrix4ViewModel();
            unkVec = new Vector4ViewModel();
            DirtyTracker.AddChild(worldToArena);
            DirtyTracker.AddChild(arenaToWorld);
            DirtyTracker.AddChild(unkVec);
            usesDistanceCurves = false;
            distanceIncludesHeight = false;
            radiusBlend = 0;
            nearHeightOffset = 0;
            farHeightOffset = 0;
            maxTurnRate = 0;
        }

        public BossCameraViewModel(CameraSubBase cam) : base(cam)
        {
            var bossCam = (BossCamera)cam;
            worldToArena = new Matrix4ViewModel(bossCam.WorldToArena);
            arenaToWorld = new Matrix4ViewModel(bossCam.ArenaToWorld);
            unkVec = new Vector4ViewModel(bossCam.Orbit);
            DirtyTracker.AddChild(worldToArena);
            DirtyTracker.AddChild(arenaToWorld);
            DirtyTracker.AddChild(unkVec);
            usesDistanceCurves = bossCam.UsesDistanceCurves;
            distanceIncludesHeight = bossCam.DistanceIncludesHeight;
            radiusBlend = bossCam.RadiusBlend;
            nearHeightOffset = bossCam.NearHeightOffset;
            farHeightOffset = bossCam.FarHeightOffset;
            maxTurnRate = bossCam.MaxTurnRate;
        }

        public override void Save(CameraSubBase? cam)
        {
            cam ??= new BossCamera();
            var bossCam = (BossCamera)cam;
            for (var i = 0; i < 4; ++i)
            {
                bossCam.WorldToArena[i] = new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = WorldToArena[i].X,
                    Y = WorldToArena[i].Y,
                    Z = WorldToArena[i].Z,
                    W = WorldToArena[i].W,
                };
                bossCam.ArenaToWorld[i] = new Twinsanity.TwinsanityInterchange.Common.Vector4
                {
                    X = ArenaToWorld[i].X,
                    Y = ArenaToWorld[i].Y,
                    Z = ArenaToWorld[i].Z,
                    W = ArenaToWorld[i].W,
                };
            }
            bossCam.Orbit = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnkVec.X,
                Y = UnkVec.Y,
                Z = UnkVec.Z,
                W = UnkVec.W,
            };
            bossCam.UsesDistanceCurves = UsesDistanceCurves;
            bossCam.DistanceIncludesHeight = DistanceIncludesHeight;
            bossCam.RadiusBlend = RadiusBlend;
            bossCam.NearHeightOffset = NearHeightOffset;
            bossCam.FarHeightOffset = FarHeightOffset;
            bossCam.MaxTurnRate = MaxTurnRate;
            base.Save(cam);
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(worldToArena, cancellationToken);
            ActivateItemAsync(arenaToWorld, cancellationToken);
            ActivateItemAsync(unkVec, cancellationToken);

            return base.OnInitializeAsync(cancellationToken);
        }

        public Matrix4ViewModel WorldToArena
        {
            get => worldToArena;
        }

        public Matrix4ViewModel ArenaToWorld
        {
            get => arenaToWorld;
        }

        public Vector4ViewModel UnkVec
        {
            get => unkVec;
        }

        [MarkDirty]
        public Boolean UsesDistanceCurves
        {
            get => usesDistanceCurves;
            set
            {
                if (usesDistanceCurves != value)
                {
                    usesDistanceCurves = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single RadiusBlend
        {
            get => radiusBlend;
            set
            {
                if (radiusBlend != value)
                {
                    radiusBlend = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single NearHeightOffset
        {
            get => nearHeightOffset;
            set
            {
                if (nearHeightOffset != value)
                {
                    nearHeightOffset = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single FarHeightOffset
        {
            get => farHeightOffset;
            set
            {
                if (farHeightOffset != value)
                {
                    farHeightOffset = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single MaxTurnRate
        {
            get => maxTurnRate;
            set
            {
                if (maxTurnRate != value)
                {
                    maxTurnRate = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        
        [MarkDirty]
        public Boolean DistanceIncludesHeight
        {
            get => distanceIncludesHeight;
            set
            {
                if (distanceIncludesHeight != value)
                {
                    distanceIncludesHeight = value;
                    NotifyOfPropertyChange();
                }
            }
        }
    }
}
