using System;
using Caliburn.Micro;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Composite
{
    public class Vector3ViewModel : Screen, ISaveableViewModel<Vector3>
    {
        private float _x;
        private float _y;
        private float _z;
        private bool isDirty;

        public Vector3ViewModel() { }

        public Vector3ViewModel(float x, float y, float z)
        {
            _x = x;
            _y = y;
            _z = z;
        }

        public Vector3ViewModel(Vector3 twinVec) : this(twinVec.X, twinVec.Y, twinVec.Z)
        {
        }

        public void ResetDirty()
        {
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

        public void Save(Vector3 o)
        {
            o.X = X;
            o.Y = Y;
            o.Z = Z;
        }
        
        public String DisplayString => ToString();

        public override String ToString()
        {
            return $"({X}; {Y}; {Z})";
        }

        [MarkDirty]
        public float X
        {
            get
            {
                return _x;
            }
            set
            {
                _x = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(DisplayString));
            }
        }

        [MarkDirty]
        public float Y
        {
            get
            {
                return _y;
            }
            set
            {
                _y = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(DisplayString));
            }
        }

        [MarkDirty]
        public float Z
        {
            get
            {
                return _z;
            }
            set
            {
                _z = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(DisplayString));
            }
        }
    }
}
