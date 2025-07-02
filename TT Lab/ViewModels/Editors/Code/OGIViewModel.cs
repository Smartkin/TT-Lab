using System;
using System.Linq;
using System.Windows.Forms;
using Caliburn.Micro;
using org.ogre;
using TT_Lab.AssetData.Code;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Editors.Code.OGI;

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

    public OGIViewModel()
    {
        Scenes.Add(IoC.Get<SceneEditorViewModel>());

        OGIScene.SceneHeaderModel = "OGI/Skeleton Viewer";
        InitOGIScene();
    }

    protected override void Save()
    {
        _ogiRender?.Dispose();
        base.Save();
    }

    public override void LoadData()
    {
        var data = AssetManager.Get().GetAssetData<OGIData>(EditableResource);
        DirtyTracker.AddChild(_boundingBox);
        _boundingBox = new BoundingBoxViewModel(data.BoundingBox);
        
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

    private void InitOGIScene()
    {
        OGIScene.SceneCreator = glControl =>
        {
            var sceneManager = glControl.GetSceneManager();
            var pivot = sceneManager.getRootSceneNode().createChildSceneNode();
            pivot.setPosition(0, 0, 0);
            glControl.SetCameraTarget(pivot);
            glControl.SetCameraStyle(CameraStyle.CS_ORBIT);
            glControl.EnableImgui(true);

            _ogiData = AssetManager.Get().GetAssetData<OGIData>(EditableResource);
            _ogiData.BlendSkin = _blendSkin;
            _ogiData.Skin = _skin;
            _ogiRender = new Rendering.Objects.OGI(EditableResource, sceneManager, _ogiData);
            pivot.addChild(_ogiRender.GetSceneNode());
            pivot.setInheritOrientation(false);
            pivot.setInheritScale(false);
            
            glControl.OnRender += (sender, args) =>
            {
                ImGui.Begin("OGI Information");
                ImGui.Text($"Bones: {_ogiData.Joints.Count}");
                ImGui.Text($"Exit Points: {_ogiData.ExitPoints.Count}");
                ImGui.Text($"Camera affected bones: {_ogiData.Joints.Count(j => j.ReactId != 255)}");
                ImGui.End();
            };
        };
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

    public SceneEditorViewModel OGIScene => Scenes[0];
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
                OGIScene.ResetScene();
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
                OGIScene.ResetScene();
                NotifyOfPropertyChange();
            }
        }
    }
}