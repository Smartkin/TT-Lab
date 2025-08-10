using System;
using System.Collections.Generic;
using org.ogre;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Rendering.Objects.SceneInstances;
using TT_Lab.Rendering.Services;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering;

public class SceneInstanceFactory
{
    private readonly MeshService _meshService;
    private readonly TwinSkeletonManager _skeletonManager;
    private readonly Dictionary<Type, Func<EditingContext, AbstractAssetData, ResourceTreeElementViewModel, SceneInstance>> _sceneInstances = new();

    public SceneInstanceFactory(MeshService meshService, TwinSkeletonManager skeletonManager)
    {
        _meshService = meshService;
        _skeletonManager = skeletonManager;
        _sceneInstances.Add(typeof(ObjectSceneInstance), CreateObjectSceneInstance);
        _sceneInstances.Add(typeof(CameraSceneInstance), CreateInstance<CameraSceneInstance>);
        _sceneInstances.Add(typeof(TriggerSceneInstance), CreateInstance<TriggerSceneInstance>);
    }
    
    public SceneInstance CreateSceneInstance(Type type, EditingContext editingContext, AbstractAssetData instanceData, ResourceTreeElementViewModel viewModel, Renderable? parentNode = null)
    {
        var sceneInstance = _sceneInstances[type](editingContext, instanceData, viewModel);
        sceneInstance.Init(parentNode);
        return sceneInstance;
    }

    public SceneInstance CreateSceneInstance<T>(EditingContext editingContext, AbstractAssetData instanceData, ResourceTreeElementViewModel viewModel, Renderable? parentNode = null) where T : SceneInstance
    {
        return CreateSceneInstance(typeof(T), editingContext, instanceData, viewModel, parentNode);
    }

    private SceneInstance CreateObjectSceneInstance(EditingContext editingContext, AbstractAssetData instanceData, ResourceTreeElementViewModel viewModel)
    {
        return new ObjectSceneInstance(editingContext, _skeletonManager, _meshService, (ObjectInstanceData)instanceData, viewModel);
    }

    private static SceneInstance CreateInstance<T>(EditingContext editingContext, AbstractAssetData instanceData, ResourceTreeElementViewModel viewModel) where T : SceneInstance
    {
        return (SceneInstance)Activator.CreateInstance(typeof(T), editingContext, instanceData, viewModel)!;
    }
    
}