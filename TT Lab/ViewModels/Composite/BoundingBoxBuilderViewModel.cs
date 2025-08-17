using System;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.Util;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Composite
{
    public class BoundingBoxBuilderViewModel : Conductor<Vector4ViewModel>.Collection.OneActive, ISaveableViewModel<TwinBoundingBoxBuilder>, IHaveChildrenEditors
    {
        private BindableCollection<Vector4ViewModel> bbPoints = new();
        private BindableCollection<Vector4ViewModel> unkVectors1 = new();
        private BindableCollection<Vector4ViewModel> unkVectors2 = new();
        private BindableCollection<Vector4ViewModel> unkVectors3 = new();
        private BindableCollection<PrimitiveWrapperViewModel<UInt16>> unkShorts = new();
        private BindableCollection<PrimitiveWrapperViewModel<Byte>> unkBytes1 = new();
        private BindableCollection<PrimitiveWrapperViewModel<Byte>> unkBytes2 = new();
        private readonly DirtyTracker dirtyTracker;

        public BoundingBoxBuilderViewModel()
        {
            dirtyTracker = new DirtyTracker(this);
            dirtyTracker.AddBindableCollection(bbPoints);
            dirtyTracker.AddBindableCollection(unkVectors1);
            dirtyTracker.AddBindableCollection(unkVectors2);
            dirtyTracker.AddBindableCollection(unkVectors3);
            dirtyTracker.AddBindableCollection(unkShorts);
            dirtyTracker.AddBindableCollection(unkBytes1);
            dirtyTracker.AddBindableCollection(unkBytes2);
            
            bbPoints.AddRange(new Vector4ViewModel[]
            {
                new(0, 0, 0, 1),
                new(0, 0, 1, 1),
                new(0, 1, 0, 1),
                new(0, 1, 1, 1),
                new(1, 0, 0, 1),
                new(1, 0, 1, 1),
                new(1, 1, 0, 1),
                new(1, 1, 1, 1),
            });
        }

        public BoundingBoxBuilderViewModel(TwinBoundingBoxBuilder bb)
        {
            dirtyTracker = new DirtyTracker(this);
            bbPoints = new BindableCollection<Vector4ViewModel>();
            dirtyTracker.AddBindableCollection(bbPoints);
            foreach (var p in bb.BoundingBoxPoints)
            {
                bbPoints.Add(new Vector4ViewModel(p));
            }
            
            unkVectors1 = new BindableCollection<Vector4ViewModel>();
            dirtyTracker.AddBindableCollection(unkVectors1);
            foreach (var unkVec in bb.UnkVectors1)
            {
                unkVectors1.Add(new Vector4ViewModel(unkVec));
            }
            
            unkVectors2 = new BindableCollection<Vector4ViewModel>();
            dirtyTracker.AddBindableCollection(unkVectors2);
            foreach (var unkVec in bb.UnkVectors2)
            {
                unkVectors2.Add(new Vector4ViewModel(unkVec));
            }
            
            unkVectors3 = new BindableCollection<Vector4ViewModel>();
            dirtyTracker.AddBindableCollection(unkVectors3);
            foreach (var unkVec in bb.UnkVectors3)
            {
                unkVectors3.Add(new Vector4ViewModel(unkVec));
            }
            
            unkShorts = new BindableCollection<PrimitiveWrapperViewModel<UInt16>>();
            dirtyTracker.AddBindableCollection(unkShorts);
            foreach (var unkShort in bb.UnkShorts)
            {
                unkShorts.Add(new PrimitiveWrapperViewModel<UInt16>(unkShort));
            }
            
            unkBytes1 = new BindableCollection<PrimitiveWrapperViewModel<Byte>>();
            dirtyTracker.AddBindableCollection(unkBytes1);
            foreach (var unkByte in bb.UnkBytes1)
            {
                unkBytes1.Add(new PrimitiveWrapperViewModel<Byte>(unkByte));
            }
            
            unkBytes2 = new BindableCollection<PrimitiveWrapperViewModel<Byte>>();
            dirtyTracker.AddBindableCollection(unkBytes2);
            foreach (var unkByte in bb.UnkBytes2)
            {
                unkBytes2.Add(new PrimitiveWrapperViewModel<Byte>(unkByte));
            }
        }

        public void ResetDirty()
        {
            dirtyTracker.ResetDirty();
        }

        public bool IsDirty => dirtyTracker.IsDirty;

        public void Save(TwinBoundingBoxBuilder o)
        {
            var builder = o;
            builder.BoundingBoxPoints.Clear();
            foreach (var v in BoundingBoxPoints)
            {
                builder.BoundingBoxPoints.Add(new Vector4
                {
                    X = v.X,
                    Y = v.Y,
                    Z = v.Z,
                    W = v.W,
                });
            }
            
            builder.UnkBytes1.Clear();
            foreach (var v in unkBytes1)
            {
                builder.UnkBytes1.Add(v.Value);
            }
            
            builder.UnkBytes2.Clear();
            foreach (var v in unkBytes2)
            {
                builder.UnkBytes2.Add(v.Value);
            }
            
            builder.UnkShorts.Clear();
            foreach (var v in unkShorts)
            {
                builder.UnkShorts.Add(v.Value);
            }
            
            builder.UnkVectors1.Clear();
            foreach (var v in unkVectors1)
            {
                builder.UnkVectors1.Add(new Vector4
                {
                    X = v.X,
                    Y = v.Y,
                    Z = v.Z,
                    W = v.W,
                });
            }
            
            builder.UnkVectors2.Clear();
            foreach (var v in unkVectors2)
            {
                builder.UnkVectors2.Add(new Vector4
                {
                    X = v.X,
                    Y = v.Y,
                    Z = v.Z,
                    W = v.W,
                });
            }
            
            builder.UnkVectors3.Clear();
            foreach (var v in unkVectors3)
            {
                builder.UnkVectors3.Add(new Vector4
                {
                    X = v.X,
                    Y = v.Y,
                    Z = v.Z,
                    W = v.W,
                });
            }
            
            dirtyTracker.ResetDirty();
        }

        public override String ToString()
        {
            return "Bounding Box Builder";
        }

        public BindableCollection<Vector4ViewModel> BoundingBoxPoints => bbPoints;
        public BindableCollection<Vector4ViewModel> UnkVectors1 => unkVectors1;
        public BindableCollection<Vector4ViewModel> UnkVectors2 => unkVectors2;
        public BindableCollection<Vector4ViewModel> UnkVectors3 => unkVectors3;
        public BindableCollection<PrimitiveWrapperViewModel<UInt16>> UnkShorts => unkShorts;
        public BindableCollection<PrimitiveWrapperViewModel<Byte>> UnkBytes1 => unkBytes1;
        public BindableCollection<PrimitiveWrapperViewModel<Byte>> UnkBytes2 => unkBytes2;

        public DirtyTracker DirtyTracker => dirtyTracker;
    }
}
