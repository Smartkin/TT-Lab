using Caliburn.Micro;
using System;
using System.Linq;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Interfaces;

namespace TT_Lab.ViewModels.Editors.Instance.Scenery
{
    public class BaseSceneryViewModel : Conductor<IScreen>.Collection.AllActive, IHaveChildrenEditors
    {
        private BindableCollection<PrimitiveWrapperViewModel<LabURI>> meshes;
        private BindableCollection<PrimitiveWrapperViewModel<LabURI>> lods;
        private BindableCollection<BoundingBoxViewModel> bbs;
        private BindableCollection<Matrix4ViewModel> meshModelMatrices;
        private BindableCollection<Matrix4ViewModel> lodModelMatrices;
        private Vector4ViewModel boundsCenter;
        private Vector4ViewModel boundsMin;
        private Vector4ViewModel boundsMax;
        private Vector4ViewModel boundsHalfSize;
        private BindableCollection<PrimitiveWrapperViewModel<Boolean>> lightsEnabler;
        private bool isDirty;
        private DirtyTracker dirtyTracker;

        public BaseSceneryViewModel(SceneryBaseData data)
        {
            dirtyTracker = new DirtyTracker(this);
            meshes = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
            dirtyTracker.AddBindableCollection(meshes);
            foreach (var m in data.MeshIDs)
            {
                meshes.Add(new PrimitiveWrapperViewModel<LabURI>(m));
            }
            
            lods = new BindableCollection<PrimitiveWrapperViewModel<LabURI>>();
            dirtyTracker.AddBindableCollection(lods);
            foreach (var l in data.LodIDs)
            {
                lods.Add(new PrimitiveWrapperViewModel<LabURI>(l));
            }
            
            bbs = new BindableCollection<BoundingBoxViewModel>();
            dirtyTracker.AddBindableCollection(bbs);
            foreach (var bb in data.BoundingBoxes)
            {
                bbs.Add(new BoundingBoxViewModel(bb));
            }
            meshModelMatrices = new BindableCollection<Matrix4ViewModel>();
            dirtyTracker.AddBindableCollection(meshModelMatrices);
            foreach (var mat in data.MeshModelMatrices)
            {
                meshModelMatrices.Add(new Matrix4ViewModel(mat));
            }
            lodModelMatrices = new BindableCollection<Matrix4ViewModel>();
            dirtyTracker.AddBindableCollection(lodModelMatrices);
            foreach (var mat in data.LodModelMatrices)
            {
                lodModelMatrices.Add(new Matrix4ViewModel(mat));
            }
            boundsCenter = new Vector4ViewModel(data.BoundsCenter);
            boundsMin = new Vector4ViewModel(data.BoundsMin);
            boundsMax = new Vector4ViewModel(data.BoundsMax);
            boundsHalfSize = new Vector4ViewModel(data.BoundsHalfSize);
            dirtyTracker.AddChild(boundsCenter);
            dirtyTracker.AddChild(boundsMin);
            dirtyTracker.AddChild(boundsMax);
            dirtyTracker.AddChild(boundsHalfSize);
            lightsEnabler = new BindableCollection<PrimitiveWrapperViewModel<Boolean>>();
            lightsEnabler.CollectionChanged += (s, e) => { dirtyTracker.MarkDirty(); };
            foreach (var enabler in data.LightsEnabler)
            {
                lightsEnabler.Add(new PrimitiveWrapperViewModel<Boolean>(enabler));
            }
        }
        
        public void ResetDirty()
        {
            DirtyTracker.ResetDirty();
        }

        public BindableCollection<PrimitiveWrapperViewModel<LabURI>> Meshes { get => meshes; private set => meshes = value; }
        public BindableCollection<PrimitiveWrapperViewModel<LabURI>> Lods { get => lods; private set => lods = value; }
        public BindableCollection<BoundingBoxViewModel> Bbs { get => bbs; private set => bbs = value; }
        public BindableCollection<Matrix4ViewModel> MeshModelMatrices { get => meshModelMatrices; private set => meshModelMatrices = value; }
        public BindableCollection<Matrix4ViewModel> LodModelMatrices { get => lodModelMatrices; private set => lodModelMatrices = value; }
        public Vector4ViewModel BoundsCenter { get => boundsCenter; set => boundsCenter = value; }
        public Vector4ViewModel BoundsMin { get => boundsMin; set => boundsMin = value; }
        public Vector4ViewModel BoundsMax { get => boundsMax; set => boundsMax = value; }
        public Vector4ViewModel BoundsHalfSize { get => boundsHalfSize; set => boundsHalfSize = value; }
        public BindableCollection<PrimitiveWrapperViewModel<Boolean>> LightsEnabler { get => lightsEnabler; private set => lightsEnabler = value; }

        public bool IsDirty => DirtyTracker.IsDirty;

        public DirtyTracker DirtyTracker => dirtyTracker;
    }
}
