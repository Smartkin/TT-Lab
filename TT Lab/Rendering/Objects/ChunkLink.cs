using System.Linq;
using GlmSharp;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.Rendering.Scene;
using Twinsanity.TwinsanityInterchange.Enumerations;

namespace TT_Lab.Rendering.Objects;

public class ChunkLink : EditableObject
{
    public override bool IsSelectable => false;
    
    private readonly AssetData.Instance.ChunkLink _chunkLinkData;

    public ChunkLink(RenderContext context, AssetData.Instance.ChunkLink chunkLinkData, Renderable parentNode, string name, vec3 size = new()) : base(context, parentNode, name, size)
    {
        _chunkLinkData = chunkLinkData;

        SetupScenery();
    }

    protected override void InitSceneTransform()
    {
        Pos = _chunkLinkData.ChunkMatrix.ToGlm().Column3.xyz;
        var angles = _chunkLinkData.ChunkMatrix.ToGlm().ToQuaternion.EulerAngles;
        Rot = new vec3((float)angles.x, (float)angles.y, (float)angles.z);
    }

    private void SetupScenery()
    {
        var assetManager = AssetManager.Get();
        var linkedChunk = assetManager.GetAsset<LevelChunk>(_chunkLinkData.Path);
        var chunkData = linkedChunk.ChunkResources;
        var linkedSceneryUri = chunkData.First(uri => assetManager.GetAsset(uri).Section == Constants.SCENERY_SECENERY_ITEM);
        var linkedScenery = assetManager.GetAssetData<SceneryData>(linkedSceneryUri);
        var linkedSceneryNode = new Node(Context, this);
        var linkedSceneryRender = new Scenery(Context, Context.MeshService, linkedScenery);
        linkedSceneryNode.AddChild(linkedSceneryRender);
        if (_chunkLinkData is { IsAlwaysVisible: false, IsVisibleInCameraFrustum: false })
        {
            linkedSceneryNode.IsVisible = false;
        }
    }
}