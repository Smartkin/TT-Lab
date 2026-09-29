using Caliburn.Micro;
using System;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common.CameraSubtypes;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.RM.Layout;

namespace TT_Lab.ViewModels.Editors.Instance.Cameras
{
    public class BaseCameraViewModel : Conductor<IScreen>.Collection.AllActive, ISaveableViewModel<CameraSubBase?>, IHaveChildrenEditors
    {
        private UInt32 flags;
        private Single leftover;
        private Single offset;
        private bool isDirty;
        private DirtyTracker dirtyTracker;

        public BaseCameraViewModel()
        {
            dirtyTracker = new DirtyTracker(this);
            leftover = 0;
            offset = 0;
        }

        public BaseCameraViewModel(CameraSubBase baseCam) : this()
        {
            flags = baseCam.Flags;
            CameraType = baseCam.GetCameraType();
            leftover = baseCam.Leftover;
            offset = baseCam.Offset;
        }

        public virtual void ResetDirty()
        {
            dirtyTracker.ResetDirty();
            IsDirty = false;
        }

        public bool IsDirty
        {
            get => isDirty;
            set
            {
                if (isDirty != value)
                {
                    isDirty = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public virtual void Save(CameraSubBase? cam)
        {
            cam.Flags = Flags;
            cam.Leftover = Leftover;
            cam.Offset = Offset;
            
            ResetDirty();
        }

        public ITwinCamera.CameraType CameraType { get; protected set; }

        [MarkDirty]
        public UInt32 Flags
        {
            get => flags;
            set
            {
                if (flags != value)
                {
                    flags = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single Leftover
        {
            get => leftover;
            set
            {
                if (leftover != value)
                {
                    leftover = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single Offset
        {
            get => offset;
            set
            {
                if (offset != value)
                {
                    offset = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public DirtyTracker DirtyTracker => dirtyTracker;
    }
}
