using GlmSharp;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private Renderable _skeleton;
    private readonly TwinSkeletonManager _skeletonManager;
    private readonly MeshService _meshService;
    private readonly ObjectInstanceData _instanceData;

    public ObjectInstance(RenderContext context, TwinSkeletonManager skeletonManager, MeshService meshService, string name, ObjectInstanceData instance, vec3 size) : base(context, null, name, size)
    {
        _skeletonManager = skeletonManager;
        _meshService = meshService;
        _instanceData = instance;
        var objUri = _instanceData.ObjectId;
        SetupModelBuffer(context, objUri);
    }

    protected override void InitSceneTransform()
    {
        Pos = new vec3(_instanceData.Position.X, _instanceData.Position.Y, _instanceData.Position.Z);
        Rot = new vec3(glm.Radians(_instanceData.Rotation.X), glm.Radians(_instanceData.Rotation.Y), glm.Radians(_instanceData.Rotation.Z));
    }

    [MemberNotNull(nameof(_skeleton))]
    private void SetupModelBuffer(RenderContext context, LabURI uri)
    {
        var assetManager = AssetManager.Get();
        var objData = assetManager.GetAssetData<GameObjectData>(uri);
        if (objData.OGISlots.All(ogiUri => ogiUri == LabURI.Empty))
        {
            _skeleton = _meshService.GetMesh(LabURI.Box).Model!;
            _skeleton.Scale(vec3.Ones * 0.5f);
            AddChild(_skeleton);
            return;
        }
        
        var ogiUri = objData.OGISlots.First(ogiUri => ogiUri != LabURI.Empty);
        var ogiData = assetManager.GetAssetData<OGIData>(ogiUri);
        _skeleton = new OGI(context, _skeletonManager, _meshService, ogiData);
        AddChild(_skeleton);
    }
}