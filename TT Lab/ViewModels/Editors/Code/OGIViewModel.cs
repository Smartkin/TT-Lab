using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Caliburn.Micro;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Services;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Code.OGI;
using Twinsanity.TwinsanityInterchange.Common;

namespace TT_Lab.ViewModels.Editors.Code;

public class OGIViewModel : ResourceEditorViewModel
{
    private BoundingBoxViewModel _boundingBox = new();
    private BindableCollection<JointViewModel> _joints = new();
    private BindableCollection<ExitPointViewModel> _exitPoints = new();
    private BindableCollection<PrimitiveWrapperViewModel<Byte>> _jointIndices = new();
    private BindableCollection<PrimitiveWrapperViewModel<LabURI>> _rigidModelIds = new();
    private BindableCollection<Matrix4ViewModel> _skinInverseMatrices = new();
    private LabURI _skin = LabURI.Empty;
    private LabURI _blendSkin = LabURI.Empty;
    private BindableCollection<BoundingBoxBuilderViewModel> _boundingBoxBuilders = new();
    private BindableCollection<PrimitiveWrapperViewModel<Byte>> _boundingBoxBuilderToJoint = new();
    private Rendering.Objects.OGI? _ogiRender;
    private OGIData _ogiData;

    public OGIViewModel(RenderContext context, TwinSkeletonManager skeletonManager, MeshService meshService)
    {
        OGIScene = IoC.Get<ViewportViewModel>();
        OGIScene.SceneInitializer = (renderer, scene) =>
        {
            _ogiData = AssetManager.Get().GetAssetData<OGIData>(EditableResource);
            _ogiRender = new Rendering.Objects.OGI(context, skeletonManager, meshService, _ogiData);
            scene.AddChild(_ogiRender);
        };
    }

    protected override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await ActivateItemAsync(OGIScene, cancellationToken);
        
        await base.OnActivateAsync(cancellationToken);
    }

    protected override async Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        await DeactivateItemAsync(OGIScene, close, cancellationToken);
        
        await base.OnDeactivateAsync(close, cancellationToken);
    }

    protected override void Save()
    {
        var data = AssetManager.Get().GetAssetData<OGIData>(EditableResource);
        _boundingBox.Save(data.BoundingBox);
        
        data.Joints.Clear();
        foreach (var jointViewModel in _joints)
        {
            var joint = new TwinJoint();
            jointViewModel.Save(joint);
            data.Joints.Add(joint);
        }
        
        data.ExitPoints.Clear();
        foreach (var exitPointViewModel in _exitPoints)
        {
            var exitPoint = new TwinExitPoint();
            exitPointViewModel.Save(exitPoint);
            data.ExitPoints.Add(exitPoint);
        }
        
        data.RigidModelIds.Clear();
        foreach (var rigidModelIdViewModel in _rigidModelIds)
        {
            data.RigidModelIds.Add(rigidModelIdViewModel.Value);
        }

        data.Skin = _skin;
        data.BlendSkin = _blendSkin;
        
        data.BoundingBoxBuilders.Clear();
        foreach (var boundingBoxBuilderViewModel in _boundingBoxBuilders)
        {
            var bbBuilder = new TwinBoundingBoxBuilder();
            boundingBoxBuilderViewModel.Save(bbBuilder);
            data.BoundingBoxBuilders.Add(bbBuilder);
        }
        
        data.BoundingBoxBuilderToJointIndex.Clear();
        foreach (var bbBuilderToJoint in _boundingBoxBuilderToJoint)
        {
            data.BoundingBoxBuilderToJointIndex.Add(bbBuilderToJoint.Value);
        }
        
        base.Save();
    }

    public override void LoadData()
    {
        var data = AssetManager.Get().GetAssetData<OGIData>(EditableResource);
        _boundingBox = new BoundingBoxViewModel(data.BoundingBox);
        DirtyTracker.AddChild(_boundingBox);
        
        foreach (var joint in data.Joints)
        {
            _joints.Add(new JointViewModel(this, joint));
        }
        
        DirtyTracker.AddBindableCollection(_exitPoints);
        foreach (var exitPoint in data.ExitPoints)
        {
            _exitPoints.Add(new ExitPointViewModel(this, exitPoint));
        }
        
        foreach (var jointIndex in data.JointIndices)
        {
            _jointIndices.Add(new PrimitiveWrapperViewModel<Byte>(jointIndex));
        }
        
        DirtyTracker.AddBindableCollection(_rigidModelIds);
        foreach (var rigidModel in data.RigidModelIds)
        {
            _rigidModelIds.Add(new PrimitiveWrapperViewModel<LabURI>(rigidModel));
        }
        
        foreach (var skinMatrix in data.SkinInverseMatrices)
        {
            _skinInverseMatrices.Add(new Matrix4ViewModel(skinMatrix));
        }
        
        _skin = data.Skin;
        _blendSkin = data.BlendSkin;
        
        DirtyTracker.AddBindableCollection(_boundingBoxBuilders);
        foreach (var bbBuilder in data.BoundingBoxBuilders)
        {
            _boundingBoxBuilders.Add(new BoundingBoxBuilderViewModel(bbBuilder));
        }
        
        DirtyTracker.AddBindableCollection(_boundingBoxBuilderToJoint);
        foreach (var bbBuilderToJoint in data.BoundingBoxBuilderToJointIndex)
        {
            _boundingBoxBuilderToJoint.Add(new PrimitiveWrapperViewModel<Byte>(bbBuilderToJoint));
        }
        
        ResetDirty();
    }

    public void ExportOgi()
    {
        using var sfd = new SaveFileDialog();
        sfd.Title = "Export Skeleton";
        sfd.Filter = "GLB file (*.glb)|*.glb";
        var result = sfd.ShowDialog();
        if (result == DialogResult.OK)
        {
            var path = sfd.FileName;
            var ogiData = AssetManager.Get().GetAssetData<OGIData>(EditableResource);
            ogiData.ExportGltf(path);
        }
    }

    public ViewportViewModel OGIScene { get; }
    public BoundingBoxViewModel BoundingBox => _boundingBox;
    public BindableCollection<JointViewModel> Joints => _joints;
    public BindableCollection<ExitPointViewModel> ExitPoints => _exitPoints;
    public BindableCollection<PrimitiveWrapperViewModel<Byte>> JointIndices => _jointIndices;
    public BindableCollection<PrimitiveWrapperViewModel<LabURI>> RigidModels => _rigidModelIds;
    public BindableCollection<Matrix4ViewModel> SkinInverseMatrices => _skinInverseMatrices;
    public BindableCollection<BoundingBoxBuilderViewModel> BoundingBoxBuilders => _boundingBoxBuilders;
    public BindableCollection<PrimitiveWrapperViewModel<Byte>> BoundingBoxBuilderToJoints => _boundingBoxBuilderToJoint;

    [MarkDirty]
    public LabURI Skin
    {
        get => _skin;
        set
        {
            if (_skin != value)
            {
                _skin = value;
                // OGIScene.ResetScene();
                NotifyOfPropertyChange();
            }
        }
    }

    [MarkDirty]
    public LabURI BlendSkin
    {
        get => _blendSkin;
        set
        {
            if (_blendSkin != value)
            {
                _blendSkin = value;
                // OGIScene.ResetScene();
                NotifyOfPropertyChange();
            }
        }
    }
}