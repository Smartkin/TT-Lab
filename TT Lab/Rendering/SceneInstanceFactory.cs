using System;
using System.Collections.Generic;
using org.ogre;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Rendering.Objects.SceneInstances;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering;

public static class SceneInstanceFactory
{
    private static readonly Dictionary<Type, Func<EditingContext, AbstractAssetData, ResourceTreeElementViewModel, SceneInstance>> _sceneInstances = new();

    static SceneInstanceFactory()
    {
        _sceneInstances.Add(typeof(ObjectSceneInstance), CreateInstance<ObjectSceneInstance>);
        _sceneInstances.Add(typeof(CameraSceneInstance), CreateInstance<CameraSceneInstance>);
        _sceneInstances.Add(typeof(TriggerSceneInstance), CreateInstance<TriggerSceneInstance>);
    }
    
    public static SceneInstance CreateSceneInstance(Type type, EditingContext editingContext, AbstractAssetData instanceData, ResourceTreeElementViewModel viewModel, SceneNode? parentNode = null)
    {
        var sceneInstance = _sceneInstances[type](editingContext, instanceData, viewModel);
        sceneInstance.Init(parentNode);
        return sceneInstance;
    }

    public static SceneInstance CreateSceneInstance<T>(EditingContext editingContext, AbstractAssetData instanceData, ResourceTreeElementViewModel viewModel, SceneNode? parentNode = null) where T : SceneInstance
    {
        return CreateSceneInstance(typeof(T), editingContext, instanceData, viewModel, parentNode);
    }

    private static SceneInstance CreateInstance<T>(EditingContext editingContext, AbstractAssetData instanceData, ResourceTreeElementViewModel viewModel) where T : SceneInstance
    {
        return (SceneInstance)Activator.CreateInstance(typeof(T), editingContext, instanceData, viewModel)!;
    }
    
}