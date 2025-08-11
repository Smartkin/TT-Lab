using System.Collections.Generic;
using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Instance;
using TT_Lab.AssetData.Instance.Scenery;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Buffers;
using TT_Lab.Rendering.Scene;
using TT_Lab.Rendering.Services;
using Twinsanity.TwinsanityInterchange.Interfaces.Items.SM;

namespace TT_Lab.Rendering.Objects;

public class Scenery : Renderable
{
    private readonly RenderContext _context;
    private readonly MeshService _meshService;

    public Scenery(RenderContext context, MeshService meshService, SceneryData sceneryData) : base(context)
    {
        _context = context;
        _meshService = meshService;
        BuildSceneryRenderTree(sceneryData);
    }

    private void BuildSceneryRenderTree(SceneryData sceneryData)
    {
        var root = (SceneryRootData)sceneryData.Sceneries[0];
        var sceneryTree = sceneryData.Sceneries.Skip(1).ToList();
        BuildSceneryRenderTreeForNode(this, root, ref sceneryTree);
        CreateSceneryNodes(this, root);
    }

    private void BuildSceneryRenderTreeForNode(Renderable parent, SceneryNodeData sceneryNode, ref List<SceneryBaseData> sceneryTree)
    {
        foreach (var sceneryType in sceneryNode.SceneryTypes)
        {
            if (sceneryType == ITwinScenery.SceneryType.Node)
            {
                var childNode = new Node(_context, parent);
                var data = (SceneryNodeData)sceneryTree[0];
                sceneryTree = sceneryTree.Skip(1).ToList();
                CreateSceneryNodes(childNode, data);
                BuildSceneryRenderTreeForNode(childNode, data, ref sceneryTree);
            }
            else if (sceneryType == ITwinScenery.SceneryType.Leaf)
            {
                var data = sceneryTree[0];
                sceneryTree = sceneryTree.Skip(1).ToList();
                CreateSceneryNodes(parent, data);
            }
        }
    }
    
    private void CreateSceneryNodes(Renderable sceneNode, SceneryBaseData dataScenery)
    {
        var index = 0;
        foreach (var meshId in dataScenery.MeshIDs)
        {
            var mesh = _meshService.GetMesh(meshId, true);
            var meshMatrix = dataScenery.MeshModelMatrices[index];
            if (mesh.Model != null)
            {
                SetupMeshNode(meshMatrix, mesh.Model);
                sceneNode.AddChild(mesh.Model);
            }

            index++;
        }
        
        index = 0;
        var assetManager = AssetManager.Get();
        foreach (var lodId in dataScenery.LodIDs)
        {
            var lod = assetManager.GetAssetData<LodModelData>(lodId);
            var mesh = _meshService.GetMesh(lod.Meshes[0], true);
            var meshMatrix = dataScenery.LodModelMatrices[index];
            if (mesh.Model != null)
            {
                SetupMeshNode(meshMatrix, mesh.Model);
                sceneNode.AddChild(mesh.Model);
            }

            index++;
        }
    }
    
    private void SetupMeshNode(Twinsanity.TwinsanityInterchange.Common.Matrix4 twinMat, Renderable meshNode)
    {
        var transformMat = twinMat.ToGlm();
        meshNode.SetLocalTransform(transformMat);
    }
}