using System;
using Caliburn.Micro;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using static Twinsanity.TwinsanityInterchange.Enumerations.Enums;

namespace TT_Lab.ViewModels.Editors.Instance
{
    public class CollisionSurfaceViewModel : InstanceSectionResourceEditorViewModel
    {
        private Layouts layId;
        private SurfaceType surfId;
        private SurfaceCollisionFlags _collisionFlags;
        private LabURI stepSoundId1 = LabURI.Empty;
        private LabURI stepSoundId2 = LabURI.Empty;
        private LabURI landSoundId1 = LabURI.Empty;
        private LabURI landSoundId2 = LabURI.Empty;
        private LabURI unkSoundId = LabURI.Empty;
        private UInt16 walkOnParticleSystemId1;
        private UInt16 walkOnParticleSystemId2;
        private UInt16 unkId3;
        private UInt16 landOnParticleSystemId;
        private UInt16 unkId5;
        private BindableCollection<PrimitiveWrapperViewModel<Single>> physicsParameters = new();
        private Vector4ViewModel unkVec = new();
        private BoundingBoxViewModel unkBoundingBox = new();

        public CollisionSurfaceViewModel()
        {
            DirtyTracker.AddChild(unkVec);
            DirtyTracker.AddChild(unkBoundingBox);
            DirtyTracker.AddBindableCollection(physicsParameters);
        }

        protected override void Save()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            asset.LayoutID = (int)LayoutID;
            var data = asset.GetData<CollisionSurfaceData>();
            data.SurfaceID = SurfId;
            data.CollisionMask = CollisionFlags;
            data.StepSoundId1 = StepSoundId1;
            data.StepSoundId2 = StepSoundId2;
            data.LandSoundId1 = LandSoundId1;
            data.LandSoundId2 = LandSoundId2;
            data.UnkSoundId = UnkSoundId;
            data.WalkOnParticleSystemId1 = WalkOnParticleSystemId1;
            data.WalkOnParticleSystemId2 = WalkOnParticleSystemId2;
            data.UnkId3 = UnkId3;
            data.LandOnParticleSystemId = LandOnParticleSystemId;
            data.PhysicsParameters = new Single[10];
            for (var i = 0; i < 10; i++)
            {
                data.PhysicsParameters[i] = PhysicsParameters[i].Value;
            }
            data.UnkVec = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnkVec.X,
                Y = UnkVec.Y,
                Z = UnkVec.Z,
                W = UnkVec.W
            };
            data.UnkBoundingBox[0] = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnkBoundingBox.TopLeft.X,
                Y = UnkBoundingBox.TopLeft.Y,
                Z = UnkBoundingBox.TopLeft.Z,
                W = UnkBoundingBox.TopLeft.W
            };
            data.UnkBoundingBox[1] = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnkBoundingBox.BottomRight.X,
                Y = UnkBoundingBox.BottomRight.Y,
                Z = UnkBoundingBox.BottomRight.Z,
                W = UnkBoundingBox.BottomRight.W
            };
            
            base.Save();
        }

        public override void LoadData()
        {
            var asset = AssetManager.Get().GetAsset(EditableResource);
            var surfData = asset.GetData<CollisionSurfaceData>();
            surfId = surfData.SurfaceID;
            _collisionFlags = surfData.CollisionMask;
            foreach (var parameter in surfData.PhysicsParameters)
            {
                physicsParameters.Add(new PrimitiveWrapperViewModel<Single>(parameter));
            }
            stepSoundId1 = surfData.StepSoundId1;
            stepSoundId2 = surfData.StepSoundId2;
            landSoundId1 = surfData.LandSoundId1;
            landSoundId2 = surfData.LandSoundId2;
            unkSoundId = surfData.UnkSoundId;
            walkOnParticleSystemId1 = surfData.WalkOnParticleSystemId1;
            walkOnParticleSystemId2 = surfData.WalkOnParticleSystemId2;
            unkId3 = surfData.UnkId3;
            landOnParticleSystemId = surfData.LandOnParticleSystemId;
            DirtyTracker.RemoveChild(unkVec);
            DirtyTracker.RemoveChild(unkBoundingBox);
            unkVec = new Vector4ViewModel(surfData.UnkVec);
            unkBoundingBox = new BoundingBoxViewModel(surfData.UnkBoundingBox);
            DirtyTracker.AddChild(unkVec);
            DirtyTracker.AddChild(unkBoundingBox);
            layId = MiscUtils.ConvertEnum<Layouts>(asset.LayoutID!.Value);
        }

        [MarkDirty]
        public Layouts LayoutID
        {
            get => layId;
            set
            {
                if (layId != value)
                {
                    layId = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public SurfaceType SurfId
        {
            get => surfId;
            set
            {
                if (surfId != value)
                {
                    surfId = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public SurfaceCollisionFlags CollisionFlags
        {
            get => _collisionFlags;
            set
            {
                if (_collisionFlags != value)
                {
                    _collisionFlags = value;
                    
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI StepSoundId1
        {
            get => stepSoundId1;
            set
            {
                if (stepSoundId1 != value)
                {
                    stepSoundId1 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI StepSoundId2
        {
            get => stepSoundId2;
            set
            {
                if (stepSoundId2 != value)
                {
                    stepSoundId2 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI LandSoundId1
        {
            get => landSoundId1;
            set
            {
                if (landSoundId1 != value)
                {
                    landSoundId1 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI LandSoundId2
        {
            get => landSoundId2;
            set
            {
                if (landSoundId2 != value)
                {
                    landSoundId2 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI UnkSoundId
        {
            get => unkSoundId;
            set
            {
                if (unkSoundId != value)
                {
                    unkSoundId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 WalkOnParticleSystemId1
        {
            get => walkOnParticleSystemId1;
            set
            {
                if (walkOnParticleSystemId1 != value)
                {
                    walkOnParticleSystemId1 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 WalkOnParticleSystemId2
        {
            get => walkOnParticleSystemId2;
            set
            {
                if (walkOnParticleSystemId2 != value)
                {
                    walkOnParticleSystemId2 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 UnkId3
        {
            get => unkId3;
            set
            {
                if (unkId3 != value)
                {
                    unkId3 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 LandOnParticleSystemId
        {
            get => landOnParticleSystemId;
            set
            {
                if (landOnParticleSystemId != value)
                {
                    landOnParticleSystemId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 UnkId5
        {
            get => unkId5;
            set
            {
                if (unkId5 != value)
                {
                    unkId5 = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public BindableCollection<PrimitiveWrapperViewModel<Single>> PhysicsParameters
        {
            get => physicsParameters;
        }

        public Vector4ViewModel UnkVec
        {
            get => unkVec;
        }

        public BoundingBoxViewModel UnkBoundingBox
        {
            get => unkBoundingBox;
        }
    }
}
