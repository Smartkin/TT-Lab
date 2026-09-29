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
    private Boolean loadsWithoutPlayer;
    private LabURI path;
    private ChunkLinkVisibility visibility;
    private Boolean isLoadWallActive;
    private Boolean keepLoaded;
    private Matrix4ViewModel objectMatrix;
    private Matrix4ViewModel chunkMatrix;
    private Matrix4ViewModel? loadingWall;
    private Vector4ViewModel _position;
    private Vector3ViewModel _rotation;
    private BindableCollection<ChunkLinkHullViewModel> hulls;
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
        hulls = [];
        dirtyTracker.AddChild(objectMatrix);
        dirtyTracker.AddChild(chunkMatrix);
        dirtyTracker.AddBindableCollection(hulls);
        
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
        
        loadsWithoutPlayer = link.LoadsWithoutPlayer;
        path = link.Path;
        visibility = link.Visibility;
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
        
        hulls = new BindableCollection<ChunkLinkHullViewModel>();
        dirtyTracker.AddBindableCollection(hulls);
        foreach (var hull in link.Hulls)
        {
            hulls.Add(new ChunkLinkHullViewModel(hull.ToTwin()));
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
        link.LoadsWithoutPlayer = LoadsWithoutPlayer;
        link.Path = Path;
        link.Visibility = Visibility;
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
        link.Hulls.Clear();
        foreach (var hull in Hulls)
        {
            var linkHull = new TwinChunkLinkHull();
            hull.Save(linkHull);
            link.Hulls.Add(new ChunkLinkHull(linkHull.Hull));
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

        foreach (var builder in hulls)
        {
            ActivateItemAsync(builder, cancellationToken);
        }

        return base.OnInitializedAsync(cancellationToken);
    }

    public Vector4ViewModel Position => _position;
    public Vector3ViewModel Rotation => _rotation;

    [MarkDirty]
    public Boolean LoadsWithoutPlayer
    {
        get => loadsWithoutPlayer;
        set
        {
            if (value != loadsWithoutPlayer)
            {
                loadsWithoutPlayer = value;
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
    public ChunkLinkVisibility Visibility
    {
        get => visibility;
        set
        {
            if (value != visibility)
            {
                visibility = value;
                NotifyOfPropertyChange();
            }
        }
    }

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
    
    public AddItemToListCommand<ChunkLinkHullViewModel> AddHullCommand => new(Hulls);
    
    public DeleteItemFromListCommand DeleteHullCommand => new(Hulls);

    public Matrix4ViewModel ObjectMatrix => objectMatrix;

    public Matrix4ViewModel ChunkMatrix => chunkMatrix;

    public Matrix4ViewModel? LoadingWall => loadingWall;

    public BindableCollection<ChunkLinkHullViewModel> Hulls => hulls;

    public DirtyTracker DirtyTracker => dirtyTracker;
}