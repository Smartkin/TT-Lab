using System;
using Caliburn.Micro;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;
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
        private LabURI impactSoundId = LabURI.Empty;
        private LabURI landSoundId = LabURI.Empty;
        private LabURI scrapeSoundId = LabURI.Empty;
        private UInt16 impactParticleSystemId;
        private UInt16 hardImpactParticleSystemId;
        private LabURI hardImpactSoundId = LabURI.Empty;
        private UInt16 stepParticleSystemId;
        private UInt16 unkId5;
        private BindableCollection<PrimitiveWrapperViewModel<Single>> physicsParameters = new();
        private Vector4ViewModel unusedVector = new();
        private BoundingBoxViewModel contactMessage = new();

        public CollisionSurfaceViewModel()
        {
            DirtyTracker.AddChild(unusedVector);
            DirtyTracker.AddChild(contactMessage);
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
            data.ImpactSoundId = ImpactSoundId;
            data.LandSoundId = LandSoundId;
            data.ScrapeSoundId = ScrapeSoundId;
            data.ImpactParticleSystemId = ImpactParticleSystemId;
            data.HardImpactParticleSystemId = HardImpactParticleSystemId;
            data.HardImpactSoundId = HardImpactSoundId;
            data.StepParticleSystemId = StepParticleSystemId;
            data.PhysicsParameters = new Single[SurfacePhysics.Count];
            for (var i = 0; i < SurfacePhysics.Count; i++)
            {
                data.PhysicsParameters[i] = PhysicsParameters[i].Value;
            }
            data.UnusedVector = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = UnusedVector.X,
                Y = UnusedVector.Y,
                Z = UnusedVector.Z,
                W = UnusedVector.W
            };
            data.ContactMessage[0] = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = ContactMessage.TopLeft.X,
                Y = ContactMessage.TopLeft.Y,
                Z = ContactMessage.TopLeft.Z,
                W = ContactMessage.TopLeft.W
            };
            data.ContactMessage[1] = new Twinsanity.TwinsanityInterchange.Common.Vector4
            {
                X = ContactMessage.BottomRight.X,
                Y = ContactMessage.BottomRight.Y,
                Z = ContactMessage.BottomRight.Z,
                W = ContactMessage.BottomRight.W
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
            impactSoundId = surfData.ImpactSoundId;
            landSoundId = surfData.LandSoundId;
            scrapeSoundId = surfData.ScrapeSoundId;
            impactParticleSystemId = surfData.ImpactParticleSystemId;
            hardImpactParticleSystemId = surfData.HardImpactParticleSystemId;
            hardImpactSoundId = surfData.HardImpactSoundId;
            stepParticleSystemId = surfData.StepParticleSystemId;
            DirtyTracker.RemoveChild(unusedVector);
            DirtyTracker.RemoveChild(contactMessage);
            unusedVector = new Vector4ViewModel(surfData.UnusedVector);
            contactMessage = new BoundingBoxViewModel(surfData.ContactMessage);
            DirtyTracker.AddChild(unusedVector);
            DirtyTracker.AddChild(contactMessage);
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
        public LabURI ImpactSoundId
        {
            get => impactSoundId;
            set
            {
                if (impactSoundId != value)
                {
                    impactSoundId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI LandSoundId
        {
            get => landSoundId;
            set
            {
                if (landSoundId != value)
                {
                    landSoundId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI ScrapeSoundId
        {
            get => scrapeSoundId;
            set
            {
                if (scrapeSoundId != value)
                {
                    scrapeSoundId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 ImpactParticleSystemId
        {
            get => impactParticleSystemId;
            set
            {
                if (impactParticleSystemId != value)
                {
                    impactParticleSystemId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 HardImpactParticleSystemId
        {
            get => hardImpactParticleSystemId;
            set
            {
                if (hardImpactParticleSystemId != value)
                {
                    hardImpactParticleSystemId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public LabURI HardImpactSoundId
        {
            get => hardImpactSoundId;
            set
            {
                if (hardImpactSoundId != value)
                {
                    hardImpactSoundId = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public UInt16 StepParticleSystemId
        {
            get => stepParticleSystemId;
            set
            {
                if (stepParticleSystemId != value)
                {
                    stepParticleSystemId = value;
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

        public Vector4ViewModel UnusedVector
        {
            get => unusedVector;
        }

        public BoundingBoxViewModel ContactMessage
        {
            get => contactMessage;
        }
    }
}
