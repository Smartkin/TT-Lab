using Caliburn.Micro;
using System;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Composite
{
    public class Vector2ViewModel : Screen, ISaveableViewModel<Vector2>
    {
        private float _x;
        private float _y;
        private bool isDirty;

        public Vector2ViewModel() { }

        public Vector2ViewModel(float x, float y)
        {
            _x = x;
            _y = y;
        }

        public Vector2ViewModel(Vector2 twinVec) : this(twinVec.X, twinVec.Y)
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

        public void Save(Vector2 o)
        {
            o.X = X;
            o.Y = Y;
        }
        
        public String DisplayString => ToString();

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

        public override String ToString()
        {
            return $"({X}; {Y})";
        }
    }
}
