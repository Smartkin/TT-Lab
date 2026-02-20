using System;
using System.Collections.Generic;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Rendering.Objects.SceneInstances;
using TT_Lab.Rendering.Services;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering;

public class SceneInstanceFactory
{
    private readonly MeshService _meshService;
    private readonly TwinSkeletonManager _skeletonManager;
    private readonly Dictionary<Type, Func<EditingContext, object, IAsset, SceneInstance>> _sceneInstances = new();

    public SceneInstanceFactory(MeshService meshService, TwinSkeletonManager skeletonManager)
    {
        _meshService = meshService;
        _skeletonManager = skeletonManager;
        _sceneInstances.Add(typeof(ObjectSceneInstance), CreateObjectSceneInstance);
        _sceneInstances.Add(typeof(CameraSceneInstance), CreateInstance<CameraSceneInstance>);
        _sceneInstances.Add(typeof(TriggerSceneInstance), CreateInstance<TriggerSceneInstance>);
        _sceneInstances.Add(typeof(ChunkLinkInstance), CreateInstance<ChunkLinkInstance>);
    }
    
    public SceneInstance CreateSceneInstance(Type type, EditingContext editingContext, object userData, IAsset asset, Renderable? parentNode = null)
    {
        var sceneInstance = _sceneInstances[type](editingContext, userData, asset);
        sceneInstance.Init(parentNode);
        return sceneInstance;
    }

    public SceneInstance CreateSceneInstance<T>(EditingContext editingContext, object instanceData, IAsset asset, Renderable? parentNode = null) where T : SceneInstance
    {
        return CreateSceneInstance(typeof(T), editingContext, instanceData, asset, parentNode);
    }

    private SceneInstance CreateObjectSceneInstance(EditingContext editingContext, object instanceData, IAsset asset)
    {
        return new ObjectSceneInstance(editingContext, _skeletonManager, _meshService, (ObjectInstanceData)instanceData, asset);
    }

    private static SceneInstance CreateInstance<T>(EditingContext editingContext, object instanceData, IAsset asset) where T : SceneInstance
    {
        return (SceneInstance)Activator.CreateInstance(typeof(T), editingContext, instanceData, asset)!;
    }
    
}