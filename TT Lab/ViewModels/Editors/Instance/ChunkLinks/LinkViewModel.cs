using Caliburn.Micro;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Command;
using TT_Lab.Extensions;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Interfaces;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors.Instance.ChunkLinks;

public class LinkViewModel : Conductor<IScreen>.Collection.AllActive, ISaveableViewModel<ChunkLink>, IHaveChildrenEditors
{
    private Boolean unkFlag;
    private LabURI path;
    private Boolean isAlwaysVisible;
    private Boolean isVisibleInCameraFrustum;
    private Byte unkNum;
    private Boolean isLoadWallActive;
    private Boolean keepLoaded;
    private Matrix4ViewModel objectMatrix;
    private Matrix4ViewModel chunkMatrix;
    private Matrix4ViewModel? loadingWall;
    private Vector4ViewModel _position;
    private Vector3ViewModel _rotation;
    private BindableCollection<ChunkLinkBoundingBoxBuilderViewModel> boundingBoxBuilders;
    private bool isDirty;
    private DirtyTracker dirtyTracker;

    public LinkViewModel()
    {
        dirtyTracker = new DirtyTracker(this);
        var assetManager = AssetManager.Get();
        var chunkPathUri = assetManager.GetAllAssetsOf<LevelChunk>().First(c => c.AdditionalPath!.Contains("levels/earth/hub/beach", StringComparison.InvariantCultureIgnoreCase)).URI;
        path = chunkPathUri;
        objectMatrix = new Matrix4ViewModel(mat4.Identity.Inverse.ToTwin());
        chunkMatrix = new Matrix4ViewModel(mat4.Identity.ToTwin());
        chunkMatrix.PropertyChanged += ChunkMatrixOnPropertyChanged;
        boundingBoxBuilders = [];
        dirtyTracker.AddChild(objectMatrix);
        dirtyTracker.AddChild(chunkMatrix);
        dirtyTracker.AddBindableCollection(boundingBoxBuilders);
        
        var rot = chunkMatrix.Rotation;
        _rotation = new Vector3ViewModel(rot.x, rot.y, rot.z);
        dirtyTracker.AddChild(_rotation);

        var pos = chunkMatrix.Position;
        _position = new Vector4ViewModel(pos.x, pos.y, pos.z, pos.w);
        dirtyTracker.AddChild(_position);
        
        ResetDirty();
    }

    public LinkViewModel(ChunkLink link)
    {
        dirtyTracker = new DirtyTracker(this);
        
        unkFlag = link.UnkFlag;
        path = link.Path;
        isAlwaysVisible = link.IsAlwaysVisible;
        isVisibleInCameraFrustum = link.IsVisibleInCameraFrustum;
        unkNum = link.UnkNum;
        isLoadWallActive = link.IsLoadWallActive;
        keepLoaded = link.KeepLoaded;
        objectMatrix = new Matrix4ViewModel(link.ObjectMatrix);
        chunkMatrix = new Matrix4ViewModel(link.ChunkMatrix);
        chunkMatrix.PropertyChanged += ChunkMatrixOnPropertyChanged;
        
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

        var rot = chunkMatrix.Rotation;
        _rotation = new Vector3ViewModel(rot.x, rot.y, rot.z);
        dirtyTracker.AddChild(_rotation);

        var pos = chunkMatrix.Position;
        _position = new Vector4ViewModel(pos.x, pos.y, pos.z, pos.w);
        dirtyTracker.AddChild(_position);
    }

    public void TranslateMatrix(vec3 translation)
    {
        ChunkMatrix.Translate(translation);
    }

    public void RotateMatrix(quat rotation)
    {
        ChunkMatrix.Rotate(rotation);
    }

    private void ChunkMatrixOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var chunkMat = ChunkMatrix.GetMatrix();
        if (Math.Abs(chunkMat.Determinant) > 0.00001f)
        {
            objectMatrix = new Matrix4ViewModel(ChunkMatrix.GetMatrix().Inverse.ToTwin());
            NotifyOfPropertyChange(nameof(ObjectMatrix));
        }
        
        if (e.PropertyName == nameof(Rotation))
        {
            var rot = chunkMatrix.Rotation;
            _rotation.X = glm.Degrees(rot.x);
            _rotation.Y = glm.Degrees(rot.y);
            _rotation.Z = glm.Degrees(rot.z);
        }

        if (e.PropertyName == nameof(Position))
        {
            var pos = chunkMatrix.Position;
            _position.X = pos.x;
            _position.Y = pos.y;
            _position.Z = pos.z;
            _position.W = pos.w;
        }
    }

    public void ResetDirty()
    {
        dirtyTracker.ResetDirty();
    }

    public bool IsDirty => dirtyTracker.IsDirty;

    public void Save(ChunkLink link)
    {
        link.UnkFlag = UnkFlag;
        link.Path = Path;
        link.IsAlwaysVisible = IsAlwaysVisible;
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

    protected override Task OnInitializedAsync(CancellationToken cancellationToken)
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

        return base.OnInitializedAsync(cancellationToken);
    }

    public Vector4ViewModel Position => _position;
    public Vector3ViewModel Rotation => _rotation;

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
    public LabURI Path
    {
        get => path;
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
    public Boolean IsAlwaysVisible
    {
        get => isAlwaysVisible;
        set
        {
            if (value != isAlwaysVisible)
            {
                isAlwaysVisible = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    [MarkDirty]
    public Boolean IsVisibleInCameraFrustum
    {
        get => isVisibleInCameraFrustum;
        set
        {
            if (value != isVisibleInCameraFrustum)
            {
                isVisibleInCameraFrustum = value;
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public Byte UnkNum
    {
        get => unkNum;
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
        get => isLoadWallActive;
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
        get => keepLoaded;
        set
        {
            if (value != keepLoaded)
            {
                keepLoaded = value;
                NotifyOfPropertyChange();
            }
        }
    }
    
    public AddItemToListCommand<ChunkLinkBoundingBoxBuilderViewModel> AddBoxBuilderCommand => new(BoundingBoxBuilders);
    
    public DeleteItemFromListCommand DeleteBoxBuilderCommand => new(BoundingBoxBuilders);

    public Matrix4ViewModel ObjectMatrix => objectMatrix;

    public Matrix4ViewModel ChunkMatrix => chunkMatrix;

    public Matrix4ViewModel? LoadingWall => loadingWall;

    public BindableCollection<ChunkLinkBoundingBoxBuilderViewModel> BoundingBoxBuilders => boundingBoxBuilders;

    public DirtyTracker DirtyTracker => dirtyTracker;
}