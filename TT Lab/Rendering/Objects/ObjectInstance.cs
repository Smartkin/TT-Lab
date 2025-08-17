using GlmSharp;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using TT_Lab.AssetData.Code;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Project;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Services;
using TT_Lab.Util;

namespace TT_Lab.Rendering.Objects;

public sealed class ObjectInstance : EditableObject
{
    private OGI skeleton;
    private readonly TwinSkeletonManager _skeletonManager;
    private readonly MeshService _meshService;
    private readonly ObjectInstanceData _instanceData;

    public ObjectInstance(RenderContext context, TwinSkeletonManager skeletonManager, MeshService meshService, string name, ObjectInstanceData instance, vec3 size) : base(context, name, size)
    {
        _skeletonManager = skeletonManager;
        _meshService = meshService;
        _instanceData = instance;
        var objURI = _instanceData.ObjectId;
        SetupModelBuffer(context, objURI);
    }

    protected override void InitSceneTransform()
    {
        Pos = new vec3(_instanceData.Position.X, _instanceData.Position.Y, _instanceData.Position.Z);
        Rot = new vec3(_instanceData.RotationX.GetRotation(), _instanceData.RotationY.GetRotation(), _instanceData.RotationZ.GetRotation());
    }

    private void SetupModelBuffer(RenderContext context, LabURI uri)
    {
        var assetManager = AssetManager.Get();
        var objData = assetManager.GetAssetData<GameObjectData>(uri);
        if (objData.OGISlots.All(ogiUri => ogiUri == LabURI.Empty))
        {
            skeleton = new OGI(Context, _skeletonManager, _meshService, assetManager.GetAssetData<OGIData>(IoC.Get<ProjectManager>().OpenedProject!.Ps2Package.URI, nameof(Assets.Code.OGI), null, 0));
            return;
        }
        
        var ogiURI = objData.OGISlots.First(ogiUri => ogiUri != LabURI.Empty);
        var ogiData = assetManager.GetAssetData<OGIData>(ogiURI);
        skeleton = new OGI(context, _skeletonManager, _meshService, ogiData);
        AddChild(skeleton);
    }
}