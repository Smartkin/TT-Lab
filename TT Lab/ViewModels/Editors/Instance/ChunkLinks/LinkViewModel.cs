using Caliburn.Micro;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors.Instance.ChunkLinks
{
    public class LinkViewModel : Conductor<IScreen>.Collection.AllActive, ISaveableViewModel<ChunkLink>, IHaveChildrenEditors
    {
        private Boolean unkFlag;
        private PrimitiveWrapperViewModel<LabURI> path;
        private Boolean isRendered;
        private Byte unkNum;
        private Boolean isLoadWallActive;
        private Boolean keepLoaded;
        private Matrix4ViewModel objectMatrix;
        private Matrix4ViewModel chunkMatrix;
        private Matrix4ViewModel? loadingWall;
        private BindableCollection<ChunkLinkBoundingBoxBuilderViewModel> boundingBoxBuilders;
        private bool isDirty;
        private DirtyTracker dirtyTracker;

        public LinkViewModel()
        {
            dirtyTracker = new DirtyTracker(this);
            var assetManager = AssetManager.Get();
            var chunkPathUri = assetManager.GetAllAssetsOf<ChunkFolder>().First(c => c.Variation.Contains("levels\\earth\\hub\\beach", StringComparison.InvariantCultureIgnoreCase)).URI;
            path = new PrimitiveWrapperViewModel<LabURI>(chunkPathUri);
            objectMatrix = new Matrix4ViewModel();
            chunkMatrix = new Matrix4ViewModel();
            boundingBoxBuilders = [];
            dirtyTracker.AddChild(objectMatrix);
            dirtyTracker.AddChild(chunkMatrix);
            dirtyTracker.AddBindableCollection(boundingBoxBuilders);
            
            boundingBoxBuilders.Add(new ChunkLinkBoundingBoxBuilderViewModel());
        }

        public LinkViewModel(ChunkLink link)
        {
            dirtyTracker = new DirtyTracker(this);
            
            unkFlag = link.UnkFlag;
            path = new PrimitiveWrapperViewModel<LabURI>(link.Path);
            isRendered = link.IsRendered;
            unkNum = link.UnkNum;
            isLoadWallActive = link.IsLoadWallActive;
            keepLoaded = link.KeepLoaded;
            objectMatrix = new Matrix4ViewModel(link.ObjectMatrix);
            chunkMatrix = new Matrix4ViewModel(link.ChunkMatrix);
            dirtyTracker.AddChild(objectMatrix);
            dirtyTracker.AddChild(chunkMatrix);
            if (link.LoadingWall != null)
            {
                loadingWall = new Matrix4ViewModel(link.LoadingWall);
                dirtyTracker.AddChild(loadingWall);
            }
            boundingBoxBuilders = new BindableCollection<ChunkLinkBoundingBoxBuilderViewModel>();
            dirtyTracker.AddBindableCollection(boundingBoxBuilders);
            foreach (var builder in link.ChunkLinksCollisionData)
            {
                boundingBoxBuilders.Add(new ChunkLinkBoundingBoxBuilderViewModel(builder));
            }
        }

        public void ResetDirty()
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

        public void Save(ChunkLink link)
        {
            link.UnkFlag = UnkFlag;
            link.Path = Path.Value;
            link.IsRendered = IsRendered;
            link.UnkNum = UnkNum;
            link.IsLoadWallActive = IsLoadWallActive;
            link.KeepLoaded = KeepLoaded;
            link.ObjectMatrix = new Matrix4();
            link.ChunkMatrix = new Matrix4();
            ObjectMatrix.Save(link.ObjectMatrix);
            ChunkMatrix.Save(link.ChunkMatrix);
            link.LoadingWall = null;
            if (LoadingWall != null)
            {
                link.LoadingWall = new Matrix4();
                LoadingWall.Save(link.LoadingWall);
            }
            link.ChunkLinksCollisionData.Clear();
            foreach (var builder in BoundingBoxBuilders)
            {
                var bbb = new TwinChunkLinkBoundingBoxBuilder();
                builder.Save(bbb);
                link.ChunkLinksCollisionData.Add(bbb);
            }
            
            ResetDirty();
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            ActivateItemAsync(objectMatrix, cancellationToken);
            ActivateItemAsync(chunkMatrix, cancellationToken);

            if (loadingWall != null)
            {
                ActivateItemAsync(loadingWall, cancellationToken);
            }

            foreach (var builder in boundingBoxBuilders)
            {
                ActivateItemAsync(builder, cancellationToken);
            }

            return base.OnInitializeAsync(cancellationToken);
        }

        [MarkDirty]
        public Boolean UnkFlag
        {
            get => unkFlag;
            set
            {
                if (value != unkFlag)
                {
                    unkFlag = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public PrimitiveWrapperViewModel<LabURI> Path
        {
            get
            {
                return path;
            }
            set
            {
                if (value != path)
                {
                    path = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Boolean IsRendered
        {
            get
            {
                return isRendered;
            }
            set
            {
                if (value != isRendered)
                {
                    isRendered = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Byte UnkNum
        {
            get
            {
                return unkNum;
            }
            set
            {
                if (value != unkNum)
                {
                    unkNum = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Boolean IsLoadWallActive
        {
            get
            {
                return isLoadWallActive;
            }
            set
            {
                if (value != isLoadWallActive)
                {
                    isLoadWallActive = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Boolean KeepLoaded
        {
            get
            {
                return keepLoaded;
            }
            set
            {
                if (value != keepLoaded)
                {
                    keepLoaded = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public Matrix4ViewModel ObjectMatrix
        {
            get => objectMatrix;
        }

        public Matrix4ViewModel ChunkMatrix
        {
            get => chunkMatrix;
        }

        public Matrix4ViewModel? LoadingWall
        {
            get => loadingWall;
        }

        public BindableCollection<ChunkLinkBoundingBoxBuilderViewModel> BoundingBoxBuilders
        {
            get => boundingBoxBuilders;
        }

        public DirtyTracker DirtyTracker => dirtyTracker;
    }
}
