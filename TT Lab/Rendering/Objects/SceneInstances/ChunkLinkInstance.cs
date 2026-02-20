using GlmSharp;
using TT_Lab.AssetData;
using TT_Lab.AssetData.Instance;
using TT_Lab.Assets;
using TT_Lab.Extensions;
using TT_Lab.ViewModels.ResourceTree;

namespace TT_Lab.Rendering.Objects.SceneInstances;

public class ChunkLinkInstance : SceneInstance
{
    public ChunkLinkInstance(EditingContext editingContext, AssetData.Instance.ChunkLink data, IAsset attachedAsset) : base(editingContext, attachedAsset, data)
    {
        Position = data.ChunkMatrix.ToGlm().Column3.xyz;
        var angles = data.ChunkMatrix.ToGlm().ToQuaternion.EulerAngles;
        Rotation = new vec3((float)angles.x, (float)angles.y, (float)angles.z);
        
        Size = vec3.Ones * 0.5f;
        Offset = -vec3.Ones * 0.25f;
    }

    protected override void CreateEditableObject(Renderable? parentNode = null)
    {
        var chunkPath = AssetManager.Get().GetAsset<LevelChunk>(GetUserDataAs<AssetData.Instance.ChunkLink>().Path)
            .AdditionalPath!;
        AttachedEditableObject = new ChunkLink(EditingContext.GetRenderContext(), GetUserDataAs<AssetData.Instance.ChunkLink>(), parentNode!, $"{GetHashCode()} Link to {chunkPath}");
    }
}